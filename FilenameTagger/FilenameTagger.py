import os
import json
import shutil
from dotenv import load_dotenv
from pathlib import Path

# ----------------------------------------
# 1. Загрузка окружения и путей
# ----------------------------------------
load_dotenv()
BASE_DIR            = Path(os.getenv('BASE_DIR', os.getcwd()))
IMAGES_FOLDER       = Path(os.getenv('IMAGES_FOLDER', BASE_DIR / 'New-Images'))
CHECK_FOLDER        = Path(os.getenv('CHECK_IMAGES_FOLDER', BASE_DIR / 'Check-Images'))
DATA_JSON_DIR       = Path(os.getenv('DATA_JSON_DIR', BASE_DIR / 'data' / 'json'))
JSON_PATH           = DATA_JSON_DIR / 'images.json'
DESCRIPTION_DEFAULT = os.getenv('DESCRIPTION_DEFAULT', '#defolt')

# Убедимся, что директория для перемещения существует
CHECK_FOLDER.mkdir(parents=True, exist_ok=True)

# Счетчики
count_files = 0

# ----------------------------------------
# 2. Сопоставление частей имени файла → хештег персонажа
# ----------------------------------------
filename_to_tag = {
    "shinji": "#Shinji_Ikari",
    "gendo": "#Gendo_Ikari",
    "rei": "#Rei_Ayanami",
    "ayanami": "#Rei_Ayanami",
    "asuka": "#Asuka_Langley",
    "langley": "#Asuka_Langley",
    "misato": "#Misato_Katsuragi",
    "katsuragi": "#Misato_Katsuragi",
    "ritsuko": "#Ritsuko_Akagi",
    "akagi": "#Ritsuko_Akagi",
    "mari": "#Mari_Makinami",
    "makinami": "#Mari_Makinami"
}

# Загружаем или создаем JSON
if os.path.exists(JSON_PATH):
    with open(JSON_PATH, 'r', encoding='utf-8') as f:
        images_data = json.load(f)
else:
    images_data = {}

# ----------------------------------------
# 3. Обрабатываем изображения
# ----------------------------------------
for fname in os.listdir(IMAGES_FOLDER):
    if not fname.lower().endswith(('.png', '.jpg', '.jpeg')):
        continue

    if fname in images_data and images_data[fname]["person"]:
        continue  # уже обработан ранее

    fname_lower = fname.lower()

    matched_tag = None
    for keyword, tag in filename_to_tag.items():
        if keyword in fname_lower:
            matched_tag = tag
            break

    if matched_tag:
        images_data[fname] = {
            "person": matched_tag,
            "description": DESCRIPTION_DEFAULT,
            "posted": 0,
            "post_time": None,
            "caption": ""
        }

        print(f"\033[93m[FILENAME]\033[0m {fname} → {matched_tag}")
        count_files += 1
        # Перемещаем файл
        src_path = IMAGES_FOLDER / fname
        dst_path = CHECK_FOLDER / fname
        shutil.move(str(src_path), str(dst_path))

# ----------------------------------------
# 4. Сохраняем JSON
# ----------------------------------------
with open(JSON_PATH, 'w', encoding='utf-8') as f:
    json.dump(images_data, f, ensure_ascii=False, indent=4)

# ----------------------------------------
# 5. Вывод статистики
# ----------------------------------------
print("\n\033[92m" + "="*50)
print(f"ОБРАБОТКА ЗАВЕРШЕНА")
print(f"Общее количество проанализированных файлов: {count_files}")