import json
import os
from config import (
    logger, 
    REGISTRY_FILE, 
    PROCESSED_DATA_FILE, 
    HASH_REGISTRY_FILE
)

def load_registry():
    if os.path.exists(REGISTRY_FILE):
        try:
            with open(REGISTRY_FILE, 'r') as f:
                return json.load(f)
        except Exception as e:
            logger.error(f"Failed to load registry: {e}")
    return {"champion": {"version": "0.0.0", "accuracy": 0.0}, "history": []}

def save_registry(registry):
    try:
        with open(REGISTRY_FILE, 'w') as f:
            json.dump(registry, f, indent=4)
    except Exception as e:
        logger.error(f"Failed to save registry: {e}")

def load_processed_ids():
    if os.path.exists(PROCESSED_DATA_FILE):
        try:
            with open(PROCESSED_DATA_FILE, 'r') as f:
                return set(json.load(f))
        except Exception as e:
            logger.error(f"Failed to load processed IDs: {e}")
    return set()

def save_processed_ids(ids):
    try:
        with open(PROCESSED_DATA_FILE, 'w') as f:
            json.dump(list(ids), f, indent=4)
    except Exception as e:
        logger.error(f"Failed to save processed IDs: {e}")

def load_global_hashes():
    if os.path.exists(HASH_REGISTRY_FILE):
        try:
            with open(HASH_REGISTRY_FILE, 'r') as f:
                return set(json.load(f))
        except Exception as e:
            logger.error(f"Failed to load hash registry: {e}")
    return set()

def save_global_hashes(hashes):
    try:
        with open(HASH_REGISTRY_FILE, 'w') as f:
            json.dump(list(hashes), f, indent=4)
    except Exception as e:
        logger.error(f"Failed to save hash registry: {e}")
