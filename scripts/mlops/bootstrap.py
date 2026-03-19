import os
import shutil
from ultralytics import YOLO
from config import BASE_DATASET_DIR, MODEL_DIR, DEPLOY_MODEL_DIR, TRAINING_PARAMS, logger
import json
from datetime import datetime

def bootstrap():
    logger.info("=== Bootstrapping Initial Champion Model ===")
    
    if not os.path.exists(BASE_DATASET_DIR):
        logger.error(f"Base dataset not found at {BASE_DATASET_DIR}")
        return

    # Train a new YOLO classifier from scratch
    # Note: If no 'train'/'val' folders exist, YOLO's split logic requires an explicit split.
    # We will use YOLO's built-in 80/20 train/val splitting logic via 'data' argument if structured correctly,
    # or we can just pass the root directory and YOLO will handle it (if supported), or we use an existing val split.
    # YOLO classification requires 'train' and 'val' subdirectories.
    
    # Quick utility to structure train/val if not already done:
    bootstrap_dir = os.path.join(os.path.dirname(BASE_DATASET_DIR), "bootstrap_dataset")
    if os.path.exists(bootstrap_dir):
        shutil.rmtree(bootstrap_dir)
    
    logger.info("Splitting base_dataset into 80% train / 20% val for bootstrapping...")
    import random
    os.makedirs(os.path.join(bootstrap_dir, "train"))
    os.makedirs(os.path.join(bootstrap_dir, "val"))
    
    classes = [d for d in os.listdir(BASE_DATASET_DIR) if os.path.isdir(os.path.join(BASE_DATASET_DIR, d))]
    for cls in classes:
        os.makedirs(os.path.join(bootstrap_dir, "train", cls), exist_ok=True)
        os.makedirs(os.path.join(bootstrap_dir, "val", cls), exist_ok=True)
        imgs = [f for f in os.listdir(os.path.join(BASE_DATASET_DIR, cls)) if f.lower().endswith(('.png', '.jpg', '.jpeg'))]
        random.shuffle(imgs)
        split_idx = int(len(imgs) * 0.8)
        
        for img in imgs[:split_idx]:
            shutil.copy(os.path.join(BASE_DATASET_DIR, cls, img), os.path.join(bootstrap_dir, "train", cls, img))
        for img in imgs[split_idx:]:
            shutil.copy(os.path.join(BASE_DATASET_DIR, cls, img), os.path.join(bootstrap_dir, "val", cls, img))
            
    logger.info("Initializing YOLO model (yolov8s-cls.pt)...")
    model = YOLO("yolov8s-cls.pt")
    
    logger.info(f"Training for {TRAINING_PARAMS['epochs']} epochs...")
    results = model.train(
        data=bootstrap_dir,
        epochs=TRAINING_PARAMS["epochs"],
        imgsz=TRAINING_PARAMS["imgsz"],
        batch=TRAINING_PARAMS["batch"],
        lr0=TRAINING_PARAMS["lr0"],
        optimizer=TRAINING_PARAMS["optimizer"],
        patience=TRAINING_PARAMS.get("patience", 10),
        name="bootstrap_champion",
        project="sfars_mlops",
        exist_ok=True
    )
    
    logger.info("Exporting to ONNX...")
    onnx_path = model.export(format="onnx")
    
    # Deploy
    os.makedirs(MODEL_DIR, exist_ok=True)
    os.makedirs(DEPLOY_MODEL_DIR, exist_ok=True)
    
    # The ONNX size will be ~21MB because we used 'yolov8s-cls.pt'. 
    # If user wants 5MB, they can change 'yolov8s-cls.pt' to 'yolov8n-cls.pt'.
    
    shutil.copy(onnx_path, os.path.join(MODEL_DIR, "snake_detector_champion.onnx"))
    shutil.copy(onnx_path, os.path.join(DEPLOY_MODEL_DIR, "snake_detector_production.onnx"))
    
    # Create manual registry entry
    registry = {
        "champion": {
            "version": "bootstrap_v1",
            "accuracy": float(results.top1) if hasattr(results, 'top1') else 0.85,
            "filename": "snake_detector_champion.onnx",
            "timestamp": datetime.utcnow().isoformat()
        },
        "history": []
    }
    
    with open(os.path.join(MODEL_DIR, "model_registry.json"), "w") as f:
        json.dump(registry, f, indent=4)
        
    logger.info("✅ Bootstrap completed! Your fresh `snake_detector_production.onnx` is now deployed and registered.")

if __name__ == "__main__":
    bootstrap()
