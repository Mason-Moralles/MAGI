import os
import json
import shutil
from dotenv import load_dotenv
from PIL import Image
import torch
import clip

# ================== 1. Загрузка конфигов ==================
load_dotenv()

BASE_DIR      = os.getenv('BASE_DIR', os.getcwd())
IMAGES_FOLDER = os.getenv('IMAGES_FOLDER', os.path.join(BASE_DIR, 'New-Images'))
CHECK_FOLDER  = os.getenv('CHECK_IMAGES_FOLDER', os.path.join(BASE_DIR, 'Check-Images'))
JSON_PATH     = os.path.join(BASE_DIR, 'data', 'json', 'images.json')
DESCRIPTION_DEFAULT = os.getenv('DESCRIPTION_DEFAULT', '#defolt')

os.makedirs(os.path.dirname(JSON_PATH), exist_ok=True)
os.makedirs(CHECK_FOLDER, exist_ok=True)

# ================== 2. Маппинг имени файла → тег ==================
filename_to_tag = {
    "shinji": "#Shinji_Ikari",
    "ikari": "#Shinji_Ikari",
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

def infer_tag_from_filename(fname: str) -> str:
    """Возвращает тег по имени файла или пустую строку."""
    lower = fname.lower()
    for keyword, tag in filename_to_tag.items():
        if keyword in lower:
            return tag
    return ""


# ================== 3. Загрузка / сохранение JSON ==================
def load_images_json() -> dict:
    if not os.path.exists(JSON_PATH):
        return {}
    try:
        with open(JSON_PATH, 'r', encoding='utf-8') as f:
            return json.load(f)
    except json.JSONDecodeError:
        return {}

def save_images_json(data: dict):
    with open(JSON_PATH, 'w', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False, indent=4)


# # ================== 4. Подготовка CLIP ==================
# device = "cuda" if torch.cuda.is_available() else "cpu"
# model, preprocess = clip.load("ViT-B/32", device=device)

# character_labels = {
#     "#Rei_Ayanami":    "Rei Ayanami from Evangelion, short pale blue hair, red eyes, white plugsuit",
#     "#Misato_Katsuragi":"Misato Katsuragi from Evangelion, purple hair, red jacket over black dress, silver cross necklace",
#     "#Asuka_Langley":  "Asuka Langley Soryu from Evangelion, long orange hair, blue eyes, red plugsuit",
#     "#Shinji_Ikari":   "Shinji Ikari from Evangelion, short brown hair, blue and white plugsuit",
#     "#Ritsuko_Akagi":  "Ritsuko Akagi from Evangelion, blonde short hair, white lab coat",
#     "#Mari_Makinami":  "Mari Makinami from Evangelion, brown long hair, red glasses, pink plugsuit"
# }
# text_inputs = torch.cat([clip.tokenize(desc) for desc in character_labels.values()]).to(device)

# Цвета для консоли
YELLOW = "\033[93m"
RESET  = "\033[0m"

# ================== 5. Режим 1: только имя файла ==================
def process_by_filename(images_data: dict) -> dict:
    """
    Обрабатывает только по имени файла.
    Если в имени найден персонаж → создаётся/обновляется запись в JSON и файл переносится в Check-Images.
    Если не найден → файл игнорируется.
    """
    for fname in os.listdir(IMAGES_FOLDER):
        if not fname.lower().endswith(('.png', '.jpg', '.jpeg', '.webp')):
            continue

        full_path = os.path.join(IMAGES_FOLDER, fname)

        # Если уже есть тег person, пропускаем
        if fname in images_data and images_data[fname].get("person"):
            continue

        tag = infer_tag_from_filename(fname)
        if not tag:
            # Ничего не делаем, JSON не создаём
            continue

        # Создаём/обновляем запись
        images_data[fname] = {
            "person": tag,
            "description": DESCRIPTION_DEFAULT,
            "posted": 0,
            "post_time": None,
            "caption": ""
        }

        # Перемещаем файл в Check-Images
        dst_path = os.path.join(CHECK_FOLDER, fname)
        shutil.move(full_path, dst_path)

        print(f"{YELLOW}[FILENAME]{RESET} {fname} → {tag}")

    return images_data


# ================== 6. Режим 2: только CLIP ==================
def process_by_clip(images_data: dict) -> dict:
    """
    Обрабатывает только через CLIP те файлы, которые:
      - лежат в IMAGES_FOLDER
      - ещё не имеют person в images.json
    """
    for fname in os.listdir(IMAGES_FOLDER):
        if not fname.lower().endswith(('.png', '.jpg', '.jpeg', '.webp')):
            continue

        full_path = os.path.join(IMAGES_FOLDER, fname)

        # Если есть запись и уже есть тег, пропускаем
        if fname in images_data and images_data[fname].get("person"):
            continue

        # Если записи ещё нет – создаём "каркас"
        if fname not in images_data:
            images_data[fname] = {
                "person": "",
                "description": "",
                "posted": 0,
                "post_time": None,
                "caption": ""
            }

        # CLIP-анализ
        image = preprocess(Image.open(full_path)).unsqueeze(0).to(device)
        with torch.no_grad():
            image_features = model.encode_image(image)
            text_features = model.encode_text(text_inputs)
            logits = (100.0 * image_features @ text_features.T).softmax(dim=-1)
            value, index = logits[0].max(0)

        chosen = list(character_labels.keys())[index]
        images_data[fname]["person"] = chosen

        if not images_data[fname]["description"]:
            images_data[fname]["description"] = DESCRIPTION_DEFAULT

        # Перемещаем файл
        dst_path = os.path.join(CHECK_FOLDER, fname)
        shutil.move(full_path, dst_path)

        print(f"{YELLOW}[CLIP]{RESET} {fname} → {chosen}, confidence: {float(value):.3f}")

    return images_data


# ================== 7. Режим 3: комбинированный ==================
def process_full(images_data: dict) -> dict:
    """
    Сначала назначает теги по имени файла (process_by_filename),
    затем запускает CLIP для оставшихся файлов (process_by_clip).
    """
    print("=== Шаг 1: разбор по именам файлов ===")
    images_data = process_by_filename(images_data)

    print("=== Шаг 2: анализ CLIP для оставшихся ===")
    images_data = process_by_clip(images_data)

    return images_data


# ================== 8. Точка входа ==================
if __name__ == "__main__":
    images_data = load_images_json()

    print("Выберите режим работы CLIP-микросервиса:")
    print("1 - Только по именам файлов")
    print("2 - Только CLIP-анализ")
    print("3 - Полная проверка (имя файла + CLIP)")
    mode = input("Ваш выбор (1/2/3): ").strip()

    if mode == "1":
        images_data = process_by_filename(images_data)
    elif mode == "2":
        images_data = process_by_clip(images_data)
    elif mode == "3":
        images_data = process_full(images_data)
    else:
        print("Неизвестный режим, выход.")
        exit(1)

    save_images_json(images_data)
    print("Готово. JSON обновлён.")
