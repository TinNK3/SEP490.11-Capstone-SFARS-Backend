import os
import cv2
import numpy as np
import onnxruntime as ort
from PIL import Image
from config import logger, DEPLOY_MODEL_DIR

class ImageProcessor:
    def __init__(self):
        self.yolo_model_path = os.path.join(DEPLOY_MODEL_DIR, "yolov8_snake_detector.onnx")
        self.session = None
        
        if os.path.exists(self.yolo_model_path):
            try:
                self.session = ort.InferenceSession(self.yolo_model_path, providers=['CPUExecutionProvider'])
                logger.info(f"Loaded YOLO detector for MLOps auto-cropping from {self.yolo_model_path}")
            except Exception as e:
                logger.error(f"Failed to load YOLO model: {e}")
        else:
            logger.warning(f"YOLO detector not found at {self.yolo_model_path}. Auto-cropping disabled.")

    def process_image(self, image_path, target_size=224, margin_ratio=0.15, confidence_threshold=0.25):
        """
        Loads an image, runs YOLO detection, crops the highest confidence snake,
        applies letterbox padding, and overwrites the image.
        If no snake is found, returns False.
        """
        if not self.session:
            return True # Allow pass-through if model is missing to avoid crashing pipeline

        try:
            # Load image with OpenCV
            img = cv2.imread(image_path)
            if img is None:
                return False
                
            orig_h, orig_w = img.shape[:2]
            
            # YOLO expects NCHW RGB [0-1] Float32 640x640
            input_size = 640
            
            # Letterbox for YOLO
            scale = min(input_size / orig_w, input_size / orig_h)
            new_w = int(orig_w * scale)
            new_h = int(orig_h * scale)
            
            resized = cv2.resize(img, (new_w, new_h), interpolation=cv2.INTER_LINEAR)
            
            dw = (input_size - new_w) / 2
            dh = (input_size - new_h) / 2
            
            top, bottom = int(round(dh - 0.1)), int(round(dh + 0.1))
            left, right = int(round(dw - 0.1)), int(round(dw + 0.1))
            
            img_padded = cv2.copyMakeBorder(resized, top, bottom, left, right, cv2.BORDER_CONSTANT, value=(0, 0, 0))
            
            # RGB & NCHW & Normalize
            img_rgb = cv2.cvtColor(img_padded, cv2.COLOR_BGR2RGB)
            img_transposed = np.transpose(img_rgb, (2, 0, 1))
            img_normalized = img_transposed.astype(np.float32) / 255.0
            img_tensor = np.expand_dims(img_normalized, axis=0) # (1, 3, 640, 640)
            
            # Inference
            input_name = self.session.get_inputs()[0].name
            outputs = self.session.run(None, {input_name: img_tensor})
            
            # Post-processing YOLOv8 output
            # outputs[0] is (1, 84, 8400)
            predictions = np.squeeze(outputs[0])  # (84, 8400)
            predictions = np.transpose(predictions)  # (8400, 84)
            
            best_conf = 0
            best_box = None
            
            for pred in predictions:
                # Assuming first 4 are cx, cy, w, h and the rest are class confidences
                box = pred[:4]
                scores = pred[4:]
                class_id = np.argmax(scores)
                conf = scores[class_id]
                
                if conf > best_conf and conf >= confidence_threshold:
                    best_conf = conf
                    best_box = box
                    
            if best_box is None:
                logger.info(f"YOLO found no snake in {image_path}. Skipping sample.")
                return False
                
            # Remap box back to original image
            cx, cy, w, h = best_box
            cx = (cx - dw) / scale
            cy = (cy - dh) / scale
            w = w / scale
            h = h / scale
            
            xmin = int(cx - w / 2)
            ymin = int(cy - h / 2)
            xmax = int(cx + w / 2)
            ymax = int(cy + h / 2)
            
            # Add margin
            margin_x = int(w * margin_ratio)
            margin_y = int(h * margin_ratio)
            
            xmin = max(0, xmin - margin_x)
            ymin = max(0, ymin - margin_y)
            xmax = min(orig_w, xmax + margin_x)
            ymax = min(orig_h, ymax + margin_y)
            
            # Crop original image
            cropped = img[ymin:ymax, xmin:xmax]
            
            # Letterbox pad cropped image to target size (224)
            crop_h, crop_w = cropped.shape[:2]
            scale_crop = min(target_size / crop_w, target_size / crop_h)
            new_crop_w = int(crop_w * scale_crop)
            new_crop_h = int(crop_h * scale_crop)
            
            crop_resized = cv2.resize(cropped, (new_crop_w, new_crop_h), interpolation=cv2.INTER_LINEAR)
            
            dw_c = (target_size - new_crop_w) // 2
            dh_c = (target_size - new_crop_h) // 2
            
            final_img = cv2.copyMakeBorder(
                crop_resized,
                dh_c, target_size - new_crop_h - dh_c,
                dw_c, target_size - new_crop_w - dw_c,
                cv2.BORDER_CONSTANT, value=(0, 0, 0)
            )
            
            # Save overwritten
            cv2.imwrite(image_path, final_img)
            return True
            
        except Exception as e:
            logger.error(f"Error processing image {image_path}: {e}")
            return False

# Singleton instance
image_processor = ImageProcessor()
