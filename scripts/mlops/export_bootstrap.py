import os
import json
import shutil
from ultralytics import YOLO
from datetime import datetime

print("Loading best.pt...")
model_pt = r'd:\Thesis_2026\Project\SEP490.11-Capstone-SFARS-Backend\scripts\mlops\runs\classify\sfars_mlops\bootstrap_champion\weights\best.pt'

if not os.path.exists(model_pt):
    print("Cannot find best.pt!")
    exit(1)

model = YOLO(model_pt)

print("Exporting to ONNX...")
onnx_path = model.export(format='onnx')

model_dir = r'd:\Thesis_2026\Project\SEP490.11-Capstone-SFARS-Backend\scripts\mlops\models'
deploy_dir = r'd:\Thesis_2026\Project\SEP490.11-Capstone-SFARS-Backend\SFARS.API\wwwroot\models'
os.makedirs(model_dir, exist_ok=True)
os.makedirs(deploy_dir, exist_ok=True)

print("Deploying models...")
shutil.copy(onnx_path, os.path.join(model_dir, 'snake_detector_champion.onnx'))
shutil.copy(onnx_path, os.path.join(deploy_dir, 'best_1.onnx'))

registry = {
    'champion': {
        'version': 'bootstrap_v1',
        'accuracy': 0.8205,
        'filename': 'snake_detector_champion.onnx',
        'timestamp': datetime.utcnow().isoformat()
    },
    'history': []
}
with open(os.path.join(model_dir, 'model_registry.json'), 'w') as f:
    json.dump(registry, f, indent=4)
print('EXPORT_SUCCESS')
