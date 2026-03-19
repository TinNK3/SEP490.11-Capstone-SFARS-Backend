import os
import logging
from datetime import datetime

# Environment Variables
DATASET_EXPORT_API = os.getenv("DATASET_EXPORT_API", "https://localhost:5001/api/admin/mlops/export-by-key")
API_KEY = os.getenv("SFARS_ADMIN_API_KEY", "MOT_CHUOI_MAT_KHAU_BI_MAT_CUA_BAN_123!@#")
EXPORT_DIR = os.getenv("EXPORT_DIR", "./mlops_data")
MODEL_DIR = os.getenv("MODEL_DIR", "./models")

# Path to the deployed backend's ONNX models folder
DEPLOY_MODEL_DIR = os.getenv("DEPLOY_MODEL_DIR", os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "SFARS.API", "wwwroot", "models")))

# ── Dataset Versioning Directories ──────────────────────────────────────────
BASE_DATASET_DIR = os.path.join(EXPORT_DIR, "base_dataset")     # User places original data here (one-time)
BATCHES_DIR = os.path.join(EXPORT_DIR, "batches")               # Each retrain batch stored separately
FROZEN_TEST_DIR = os.path.join(EXPORT_DIR, "frozen_test")       # Test set frozen from base (never changes)
DATASET_DIR = os.path.join(EXPORT_DIR, "datasets")              # Versioned train/val splits

# Splitting Ratios (applied to base_dataset during initialization)
TEST_RATIO = 0.15       # Fraction of base data frozen for test (one-time)
TRAIN_RATIO = 0.80      # Of remaining: 80% train
VAL_RATIO = 0.20        # Of remaining: 20% val

# ── MLOps Metadata Files ────────────────────────────────────────────────────
REGISTRY_FILE = os.path.join(MODEL_DIR, "model_registry.json")
PROCESSED_DATA_FILE = os.path.join(EXPORT_DIR, "processed_inferences.json")
HASH_REGISTRY_FILE = os.path.join(EXPORT_DIR, "global_hashes.json")

# ── Enterprise Constraints ──────────────────────────────────────────────────
ACCURACY_THRESHOLD = 0.01
MIN_SAMPLES_PER_CLASS = 10   # Minimum samples to include a class in training
MAX_DATASETS_TO_KEEP = 5     # Retention policy: keep last 5 versioned splits
GLOBAL_SEED = 42

# ── Training Hyperparameters ────────────────────────────────────────────────
TRAINING_PARAMS = {
    "epochs": 50,
    "imgsz": 224,
    "batch": 16,
    "lr0": 0.01,
    "optimizer": "SGD",
    "patience": 10
}

# Advanced Augmentations
AUGMENTATION_PARAMS = {
    "hsv_h": 0.015,
    "hsv_s": 0.7,
    "hsv_v": 0.4,
    "degrees": 15.0,
    "translate": 0.1,
    "scale": 0.5,
    "flipud": 0.5,
    "fliplr": 0.5,
    "mosaic": 1.0,
    "mixup": 0.1
}

# Monitoring
ENABLE_TENSORBOARD = True
KEEP_CHAMPION_VERSIONS = 10

# ── Venomous Species (for safety monitoring) ────────────────────────────────
# Classes where recall drops MUST trigger warnings
# Based on the user's 15 specific classes:
# bungarus_candidus (Krait), bungarus_fasciatus (Krait), calloselasma_rhodostoma (Pit Viper),
# rhabdophis_subminiatus (Keelback - highly venomous rear-fanged), ophiophagus_hannah (King Cobra),
# naja_kaouthia (Cobra), trimeresurus_vogeli (Pit Viper), azemiops_feae (Fea's Viper),
# trimeresurus_albolabris (Pit Viper).
VENOMOUS_CLASSES = [
    "bungarus_candidus",
    "bungarus_fasciatus",
    "calloselasma_rhodostoma",
    "rhabdophis_subminiatus",
    "ophiophagus_hannah",
    "naja_kaouthia",
    "trimeresurus_vogeli",
    "azemiops_feae",
    "trimeresurus_albolabris"
]

# ── Logging ─────────────────────────────────────────────────────────────────
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger("sfars-mlops")

def validate_config():
    if not API_KEY:
        logger.error("Missing SFARS_ADMIN_API_KEY environment variable")
        return False
    os.makedirs(MODEL_DIR, exist_ok=True)
    os.makedirs(DATASET_DIR, exist_ok=True)
    os.makedirs(BATCHES_DIR, exist_ok=True)
    return True

def get_timestamp_version():
    return datetime.utcnow().strftime("%Y%m%d_%H%M%S")
