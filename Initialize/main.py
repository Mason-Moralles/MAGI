import os
import json
from dotenv import load_dotenv

# Загрузка переменных окружения
load_dotenv()
BASE_DIR = os.getenv('BASE_DIR', os.getcwd())
DATA_DIR = os.path.join(BASE_DIR, 'data', 'json')

# Имена файлов
IMAGES_JSON = os.path.join(DATA_DIR, 'images.json')
SCHEDULE_JSON = os.path.join(DATA_DIR, 'schedule.json')
POSTED_JSON = os.path.join(DATA_DIR, 'posted_images.json')

# Имена папок
IMAGES_FOLDER = os.getenv('IMAGES_FOLDER', os.path.join(BASE_DIR, 'New-Images'))
CHECK_FOLDER = os.getenv('CHECK_IMAGES_FOLDER', os.path.join(BASE_DIR, 'Check-Images'))
POST_FOLDER = os.getenv('POST_IMAGES_FOLDER', os.path.join(BASE_DIR, 'Post-Images'))

os.makedirs(IMAGES_FOLDER, exist_ok=True)
os.makedirs(CHECK_FOLDER, exist_ok=True)
os.makedirs(POST_FOLDER, exist_ok=True)
os.makedirs(DATA_DIR, exist_ok=True)

# Функция проверки и создания JSON-файла

def ensure_json(path, default):
    if not os.path.exists(path):
        with open(path, 'w', encoding='utf-8') as f:
            json.dump(default, f, ensure_ascii=False, indent=4)
    else:
        try:
            with open(path, 'r', encoding='utf-8') as f:
                _ = json.load(f)
        except json.JSONDecodeError:
            with open(path, 'w', encoding='utf-8') as f:
                json.dump(default, f, ensure_ascii=False, indent=4)

# Инициализация файлов
ensure_json(IMAGES_JSON, {})
ensure_json(SCHEDULE_JSON, {})
ensure_json(POSTED_JSON, {})

print(f"Initialized folders:\n  New-Images: {IMAGES_FOLDER}\n  Check-Images: {CHECK_FOLDER}\n  Post-Images: {POST_FOLDER}")
print(f"Initialized JSON files:\n  images.json\n  schedule.json\n  posted_images.json")