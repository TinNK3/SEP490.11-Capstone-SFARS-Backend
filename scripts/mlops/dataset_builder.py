"""
Production-grade incremental dataset builder with:
- Frozen test set (from base_dataset, never changes)
- Batch isolation (each retrain batch stored separately for rollback)
- Logical union (accumulated = base + all batches)
- Weighted class distribution tracking in manifest
"""
import json
import os
import random
import hashlib
import shutil
from PIL import Image
from config import (
    logger, GLOBAL_SEED, MIN_SAMPLES_PER_CLASS, MAX_DATASETS_TO_KEEP,
    BASE_DATASET_DIR, BATCHES_DIR, FROZEN_TEST_DIR, DATASET_DIR,
    TEST_RATIO, TRAIN_RATIO, VAL_RATIO
)
from data_fetcher import download_image
from metadata_manager import load_processed_ids, save_processed_ids, load_global_hashes, save_global_hashes


# ── Utilities ───────────────────────────────────────────────────────────────

def get_image_hash(image_path):
    hash_sha256 = hashlib.sha256()
    with open(image_path, "rb") as f:
        for chunk in iter(lambda: f.read(4096), b""):
            hash_sha256.update(chunk)
    return hash_sha256.hexdigest()


def is_valid_image(image_path):
    try:
        with Image.open(image_path) as img:
            img.verify()
        return True
    except Exception:
        return False


def _list_images(directory):
    """List all image files in a directory (non-recursive)."""
    exts = {".jpg", ".jpeg", ".png", ".webp", ".bmp"}
    if not os.path.isdir(directory):
        return []
    return [f for f in os.listdir(directory) if os.path.splitext(f)[1].lower() in exts]


def _collect_pool():
    """
    Build the logical union: base_dataset + all batches.
    Returns dict: {class_name: [absolute_path, ...]}
    """
    pool = {}

    # 1. Base dataset (excluding images that were moved to frozen_test)
    if os.path.isdir(BASE_DATASET_DIR):
        for cls in sorted(os.listdir(BASE_DATASET_DIR)):
            cls_dir = os.path.join(BASE_DATASET_DIR, cls)
            if os.path.isdir(cls_dir):
                pool[cls] = [os.path.join(cls_dir, f) for f in _list_images(cls_dir)]

    # 2. All batches (sorted chronologically)
    if os.path.isdir(BATCHES_DIR):
        for batch_name in sorted(os.listdir(BATCHES_DIR)):
            batch_dir = os.path.join(BATCHES_DIR, batch_name)
            if not os.path.isdir(batch_dir):
                continue
            for cls in os.listdir(batch_dir):
                cls_dir = os.path.join(batch_dir, cls)
                if not os.path.isdir(cls_dir):
                    continue
                if cls not in pool:
                    pool[cls] = []
                pool[cls].extend([os.path.join(cls_dir, f) for f in _list_images(cls_dir)])

    return pool


# ── Step 1: Initialize Frozen Test Set (One-Time) ──────────────────────────

def initialize_frozen_test():
    """
    One-time operation: Split base_dataset into frozen_test + remaining base.
    After this, base_dataset/ only contains train+val images.
    frozen_test/ contains the test split and is NEVER modified again.
    """
    if os.path.isdir(FROZEN_TEST_DIR) and len(os.listdir(FROZEN_TEST_DIR)) > 0:
        logger.info("Frozen test set already exists. Skipping initialization.")
        return True

    if not os.path.isdir(BASE_DATASET_DIR) or len(os.listdir(BASE_DATASET_DIR)) == 0:
        logger.error(f"Base dataset not found at: {BASE_DATASET_DIR}")
        logger.error("Please place your original training images in: mlops_data/base_dataset/{class_name}/")
        return False

    logger.info("=== Initializing Frozen Test Set from Base Dataset ===")
    random.seed(GLOBAL_SEED)

    total_moved = 0
    for cls in sorted(os.listdir(BASE_DATASET_DIR)):
        cls_src = os.path.join(BASE_DATASET_DIR, cls)
        if not os.path.isdir(cls_src):
            continue

        images = _list_images(cls_src)
        if not images:
            continue

        random.shuffle(images)
        test_count = max(1, int(len(images) * TEST_RATIO))
        test_images = images[:test_count]

        # Move test images to frozen_test/
        cls_test_dir = os.path.join(FROZEN_TEST_DIR, cls)
        os.makedirs(cls_test_dir, exist_ok=True)
        for img_name in test_images:
            src = os.path.join(cls_src, img_name)
            dst = os.path.join(cls_test_dir, img_name)
            shutil.move(src, dst)
            total_moved += 1

        logger.info(f"  {cls}: {test_count}/{len(images)} images → frozen_test/")

    logger.info(f"Frozen test set created with {total_moved} total images. This set will NEVER change.")
    return True


# ── Step 2: Ingest New Batch ───────────────────────────────────────────────

def ingest_new_batch(samples, version):
    """
    Download new verified images into a separate batch folder.
    Only processes samples not yet in processed_inferences.json.
    Returns (batch_dir, count) or (None, 0) if no new samples.
    """
    processed_ids = load_processed_ids()
    global_hashes = load_global_hashes()

    new_samples = [s for s in samples if s["inferenceId"] not in processed_ids]
    if not new_samples:
        logger.warning("No new samples to ingest.")
        return None, 0

    batch_dir = os.path.join(BATCHES_DIR, f"batch_{version}")
    new_hashes = set()
    ingested_ids = set()
    skipped = 0

    for sample in new_samples:
        cls = sample["confirmedScientificName"].replace(" ", "_")
        cls_dir = os.path.join(batch_dir, cls)
        os.makedirs(cls_dir, exist_ok=True)

        file_id = sample["inferenceId"]
        dest_path = os.path.join(cls_dir, f"{file_id}.jpg")

        if not download_image(sample["imageUrl"], dest_path):
            skipped += 1
            continue

        if not is_valid_image(dest_path):
            logger.warning(f"Corrupted image skipped: {file_id}")
            os.remove(dest_path)
            skipped += 1
            continue

        img_hash = get_image_hash(dest_path)
        if img_hash in global_hashes or img_hash in new_hashes:
            logger.info(f"Duplicate image skipped: {file_id}")
            os.remove(dest_path)
            skipped += 1
            continue

        new_hashes.add(img_hash)
        ingested_ids.add(file_id)

    if not ingested_ids:
        # Clean up empty batch dir
        if os.path.isdir(batch_dir):
            shutil.rmtree(batch_dir)
        logger.warning(f"All {len(new_samples)} samples were duplicates or invalid.")
        return None, 0

    # Save batch manifest for rollback tracking
    batch_manifest = {
        "version": version,
        "inference_ids": list(ingested_ids),
        "total_ingested": len(ingested_ids),
        "total_skipped": skipped,
        "source": "api_verified"
    }
    with open(os.path.join(batch_dir, "manifest.json"), "w") as f:
        json.dump(batch_manifest, f, indent=4)

    # Update global tracking
    save_processed_ids(processed_ids.union(ingested_ids))
    save_global_hashes(global_hashes.union(new_hashes))

    logger.info(f"Batch {version}: ingested {len(ingested_ids)} images, skipped {skipped}.")
    return batch_dir, len(ingested_ids)


# ── Step 3: Build Versioned Dataset ────────────────────────────────────────

def build_versioned_dataset(version):
    """
    Build a versioned train/val dataset from the logical union of base + all batches.
    Copies frozen_test/ as-is into the versioned directory.
    
    Returns (dataset_path, class_distribution) or (None, {}) if insufficient data.
    """
    logger.info(f"Building versioned dataset: {version}")
    random.seed(GLOBAL_SEED)

    pool = _collect_pool()

    if not pool:
        logger.error("No data found in base_dataset or batches.")
        return None, {}

    # Filter out classes with too few samples
    class_distribution = {}
    active_classes = []
    for cls, images in sorted(pool.items()):
        count = len(images)
        if count >= MIN_SAMPLES_PER_CLASS:
            active_classes.append(cls)
            class_distribution[cls] = count
        else:
            logger.info(f"Class '{cls}' has {count} samples (< {MIN_SAMPLES_PER_CLASS}). Skipping.")

    if not active_classes:
        logger.error("No classes met minimum sample threshold.")
        return None, {}

    # Build versioned split directory
    version_dir = os.path.join(DATASET_DIR, f"dataset_{version}")

    for cls in active_classes:
        images = pool[cls]
        random.shuffle(images)

        n = len(images)
        train_end = max(1, int(n * TRAIN_RATIO))
        # val gets the rest
        splits = {
            "train": images[:train_end],
            "val": images[train_end:]
        }

        for split_name, split_images in splits.items():
            split_cls_dir = os.path.join(version_dir, split_name, cls)
            os.makedirs(split_cls_dir, exist_ok=True)
            for img_path in split_images:
                dst = os.path.join(split_cls_dir, os.path.basename(img_path))
                if not os.path.exists(dst):
                    shutil.copy2(img_path, dst)

    # Copy frozen test set as-is
    if os.path.isdir(FROZEN_TEST_DIR):
        test_dest = os.path.join(version_dir, "test")
        if os.path.exists(test_dest):
            shutil.rmtree(test_dest)
        shutil.copytree(FROZEN_TEST_DIR, test_dest)
        logger.info("Frozen test set copied into versioned dataset.")

    # Collect batch sources for manifest
    batch_sources = ["base_dataset"]
    if os.path.isdir(BATCHES_DIR):
        for b in sorted(os.listdir(BATCHES_DIR)):
            if os.path.isdir(os.path.join(BATCHES_DIR, b)):
                batch_sources.append(b)

    # Generate manifest
    total_images = sum(class_distribution.values())
    manifest = {
        "version": version,
        "sources": batch_sources,
        "total_images": total_images,
        "classes": active_classes,
        "class_distribution": class_distribution,
        "frozen_test_classes": sorted(os.listdir(FROZEN_TEST_DIR)) if os.path.isdir(FROZEN_TEST_DIR) else [],
        "config": {
            "min_samples_per_class": MIN_SAMPLES_PER_CLASS,
            "train_ratio": TRAIN_RATIO,
            "val_ratio": VAL_RATIO,
            "seed": GLOBAL_SEED
        }
    }
    with open(os.path.join(version_dir, "dataset_manifest.json"), "w") as f:
        json.dump(manifest, f, indent=4)

    # Retention policy: clean up old versioned splits
    _cleanup_old_datasets()

    logger.info(f"Dataset {version} built: {total_images} images across {len(active_classes)} classes.")
    logger.info(f"Sources: {batch_sources}")
    return version_dir, class_distribution


def _cleanup_old_datasets():
    """Keep only the last N versioned dataset splits."""
    if not os.path.isdir(DATASET_DIR):
        return
    datasets = sorted([
        d for d in os.listdir(DATASET_DIR)
        if os.path.isdir(os.path.join(DATASET_DIR, d)) and d.startswith("dataset_")
    ])
    if len(datasets) > MAX_DATASETS_TO_KEEP:
        for d in datasets[:-MAX_DATASETS_TO_KEEP]:
            shutil.rmtree(os.path.join(DATASET_DIR, d))
            logger.info(f"Retention: Deleted old dataset split {d}")
