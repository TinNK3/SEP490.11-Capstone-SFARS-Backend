"""
SFARS MLOps Pipeline — Orchestrates the full retrain flow:
1. Initialize frozen test set (one-time from base_dataset)
2. Fetch new verified samples from API
3. Ingest into isolated batch
4. Build versioned dataset (logical union of base + all batches)
5. Train with weighted loss + augmentation
6. Evaluate on frozen test set with per-class monitoring
7. Promote or reject challenger model

Exit codes for C# orchestrator:
  0 = Model promoted (new champion)
  1 = Model not promoted (champion retained)
  2 = Error / insufficient data
"""
from config import logger, validate_config, get_timestamp_version
from data_fetcher import fetch_verified_data
from dataset_builder import initialize_frozen_test, ingest_new_batch, build_versioned_dataset
from trainer import train_model
from evaluator import evaluate_and_promote, export_model
from metadata_manager import load_registry
import sys
import json


def run_pipeline():
    if not validate_config():
        sys.exit(2)

    logger.info("=== Starting SFARS Production MLOps Pipeline ===")

    try:
        # 1. Ensure frozen test set exists (one-time from base_dataset)
        if not initialize_frozen_test():
            logger.error("Cannot proceed without frozen test set. Place base_dataset first.")
            sys.exit(2)

        # 2. Fetch new verified samples from API
        samples = fetch_verified_data()
        if not samples:
            logger.warning("No new verified samples from API. Pipeline halted.")
            sys.exit(2)

        # 3. Ingest into isolated batch (for rollback support)
        version = get_timestamp_version()
        batch_dir, batch_count = ingest_new_batch(samples, version)

        if not batch_dir:
            logger.warning("No new unique data to ingest. Pipeline ended.")
            sys.exit(2)

        # 4. Build versioned dataset (logical union: base + all batches)
        dataset_path, class_distribution = build_versioned_dataset(version)

        if not dataset_path:
            logger.warning("Dataset build failed. Pipeline ended.")
            sys.exit(2)

        # 5. Train with weighted loss + augmentation
        model, class_weights = train_model(dataset_path, version)

        # 6. Get champion accuracy before evaluation
        registry = load_registry()
        champ_acc = registry.get("champion", {}).get("accuracy", 0.0)

        # 7. Evaluate on frozen test set
        is_better, metrics = evaluate_and_promote(model, version, class_weights)

        # Build result JSON for C# orchestrator
        result_json = {
            "status": "success",
            "model_version": version,
            "old_accuracy": champ_acc,
            "new_accuracy": metrics.get("top1", 0.0),
            "samples": batch_count,
            "total_pool_size": sum(class_distribution.values()) if class_distribution else 0,
            "classes": len(class_distribution),
            "per_class_distribution": class_distribution
        }

        if is_better:
            final_path = export_model(model, version, metrics, version)
            logger.info(f"Pipeline complete! New Champion v{version} deployed at: {final_path}")
            print(json.dumps(result_json))
            sys.exit(0)  # 0 = promoted
        else:
            logger.warning("Challenger rejected. Champion remains.")
            print(json.dumps(result_json))
            sys.exit(1)  # 1 = not promoted

    except Exception as e:
        logger.error(f"Pipeline failed: {e}")
        sys.exit(2)


if __name__ == "__main__":
    run_pipeline()