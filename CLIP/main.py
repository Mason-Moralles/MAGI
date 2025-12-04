import os
import json
import shutil
from pathlib import Path
from dotenv import load_dotenv
import clip
from PIL import Image
import torch
import torchvision.transforms as T
import torchvision

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

IMAGES_FOLDER.mkdir(parents=True, exist_ok=True)
CHECK_FOLDER.mkdir(parents=True, exist_ok=True)
DATA_JSON_DIR.mkdir(parents=True, exist_ok=True)

# ----------------------------------------
# 2. Утилиты для JSON
# ----------------------------------------
def load_images_json() -> dict:
    if not JSON_PATH.exists():
        return {}
    try:
        with open(JSON_PATH, 'r', encoding='utf-8') as f:
            return json.load(f)
    except json.JSONDecodeError:
        return {}

def save_images_json(data: dict):
    with open(JSON_PATH, 'w', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False, indent=4)

# ----------------------------------------
# 3. Подгружаем модели: Faster R-CNN + CLIP
# ----------------------------------------
device = torch.device("cuda" if torch.cuda.is_available() else "cpu")

# 3.1. Faster R-CNN для детекции «person»
from torchvision.models.detection import FasterRCNN_ResNet50_FPN_Weights
weights = FasterRCNN_ResNet50_FPN_Weights.DEFAULT
faster_model = torchvision.models.detection.fasterrcnn_resnet50_fpn(weights=weights)
faster_model.to(device).eval()

# 3.2. CLIP
clip_model, preprocess = clip.load("ViT-B/32", device=device)

# ----------------------------------------
# 4. Класс FeatureBasedCLIP (упрощённая версия)
# ----------------------------------------
from typing import List, Dict, Tuple, Optional
from dataclasses import dataclass
from enum import Enum

class FeatureType(Enum):
    HAIR_COLOR   = "hair_color"
    HAIR_STYLE   = "hair_style"
    EYE_COLOR    = "eye_color"
    FACE_SHAPE   = "face_shape"
    ACCESSORIES  = "accessories"

@dataclass
class CharacterFeature:
    feature_type: FeatureType
    description: str
    weight: float
    negative_examples: List[str]

@dataclass
class Character:
    name: str
    hashtag: str
    features: List[CharacterFeature]

class FeatureBasedCLIP:
    """
    Считает «вес» сходства изображения с образом конкретного персонажа
    через CLIP + feature-признаки (цвет волос, форма лица и т. д.).
    """
    def __init__(self):
        self.device = device
        self.model, self.preprocess = clip_model, preprocess

        # Веса для типов признаков
        self.feature_weights = {
            FeatureType.HAIR_COLOR:  0.35,
            FeatureType.HAIR_STYLE:  0.25,
            FeatureType.EYE_COLOR:   0.25,
            FeatureType.FACE_SHAPE:  0.10,
            FeatureType.ACCESSORIES: 0.05
        }

        self.characters = self._initialize_characters()

    def _initialize_characters(self) -> List[Character]:
        chars = []

        # Shinji Ikari
        shinji_features = [
            CharacterFeature(
                FeatureType.HAIR_COLOR,
                "short brown hair",
                self.feature_weights[FeatureType.HAIR_COLOR],
                ["long hair", "black hair", "blonde hair", "orange hair"]
            ),
            CharacterFeature(
                FeatureType.EYE_COLOR,
                "blue eyes",
                self.feature_weights[FeatureType.EYE_COLOR],
                ["red eyes", "brown eyes", "green eyes"]
            )
        ]
        chars.append(Character("Shinji Ikari", "#Shinji_Ikari", shinji_features))

        # Rei Ayanami
        rei_features = [
            CharacterFeature(
                FeatureType.HAIR_COLOR,
                "short pale blue hair",
                self.feature_weights[FeatureType.HAIR_COLOR],
                ["orange hair", "brown hair", "black hair"]
            ),
            CharacterFeature(
                FeatureType.EYE_COLOR,
                "red eyes",
                self.feature_weights[FeatureType.EYE_COLOR],
                ["blue eyes", "brown eyes", "green eyes"]
            )
        ]
        chars.append(Character("Rei Ayanami", "#Rei_Ayanami", rei_features))

        # Asuka Langley
        asuka_features = [
            CharacterFeature(
                FeatureType.HAIR_COLOR,
                "long bright orange hair",
                self.feature_weights[FeatureType.HAIR_COLOR],
                ["blue hair", "pale blue hair", "black hair"]
            ),
            CharacterFeature(
                FeatureType.EYE_COLOR,
                "blue eyes",
                self.feature_weights[FeatureType.EYE_COLOR],
                ["red eyes", "brown eyes", "green eyes"]
            )
        ]
        chars.append(Character("Asuka Langley", "#Asuka_Langley", asuka_features))

        # Misato Katsuragi
        misato_features = [
            CharacterFeature(
                FeatureType.HAIR_COLOR,
                "dark purple hair",
                self.feature_weights[FeatureType.HAIR_COLOR],
                ["orange hair", "blue hair", "brown hair", "black hair"]
            ),
            CharacterFeature(
                FeatureType.ACCESSORIES,
                "silver cross necklace",
                self.feature_weights[FeatureType.ACCESSORIES],
                []
            )
        ]
        chars.append(Character("Misato Katsuragi", "#Misato_Katsuragi", misato_features))

        # Ritsuko Akagi
        ritsuko_features = [
            CharacterFeature(
                FeatureType.HAIR_COLOR,
                "blonde short hair",
                self.feature_weights[FeatureType.HAIR_COLOR],
                ["brown hair", "black hair", "red hair"]
            ),
            CharacterFeature(
                FeatureType.ACCESSORIES,
                "black glasses",
                self.feature_weights[FeatureType.ACCESSORIES],
                []
            )
        ]
        chars.append(Character("Ritsuko Akagi", "#Ritsuko_Akagi", ritsuko_features))

        # Mari Makinami
        mari_features = [
            CharacterFeature(
                FeatureType.HAIR_COLOR,
                "long brown hair with pink highlights",
                self.feature_weights[FeatureType.HAIR_COLOR],
                ["blue hair", "pale hair", "orange hair"]
            ),
            CharacterFeature(
                FeatureType.ACCESSORIES,
                "red-framed glasses",
                self.feature_weights[FeatureType.ACCESSORIES],
                []
            )
        ]
        chars.append(Character("Mari Makinami", "#Mari_Makinami", mari_features))

        # Kaworu Nagisa
        kaworu_features = [
            CharacterFeature(
                FeatureType.HAIR_COLOR,
                "short silver hair",
                self.feature_weights[FeatureType.HAIR_COLOR],
                ["black hair", "orange hair", "blue hair"]
            ),
            CharacterFeature(
                FeatureType.EYE_COLOR,
                "red eyes",
                self.feature_weights[FeatureType.EYE_COLOR],
                ["blue eyes", "brown eyes", "green eyes"]
            )
        ]
        chars.append(Character("Kaworu Nagisa", "#Kaworu_Nagisa", kaworu_features))

        # Hikari Horaki
        hikari_features = [
            CharacterFeature(
                FeatureType.HAIR_COLOR,
                "short brown hair with ribbon",
                self.feature_weights[FeatureType.HAIR_COLOR],
                ["long hair", "orange hair", "blue hair"]
            )
        ]
        chars.append(Character("Hikari Horaki", "#Hikari_Horaki", hikari_features))

        # Ryoji Kaji
        kaji_features = [
            CharacterFeature(
                FeatureType.FACE_SHAPE,
                "stubbled beard",
                self.feature_weights[FeatureType.FACE_SHAPE],
                []
            )
        ]
        chars.append(Character("Ryoji Kaji", "#Ryoji_Kaji", kaji_features))

        return chars

    def _create_prompt(self, feature: CharacterFeature) -> str:
        prompt = f"character with {feature.description}"
        if feature.negative_examples:
            neg = " NOT " + " NOT ".join(feature.negative_examples)
            prompt += neg
        return prompt

    def _feature_score(self, crop: Image.Image, feature: CharacterFeature) -> float:
        image_input = self.preprocess(crop).unsqueeze(0).to(self.device)
        text_input = clip.tokenize([self._create_prompt(feature)]).to(self.device)
        with torch.no_grad():
            im_feat = self.model.encode_image(image_input)
            tx_feat = self.model.encode_text(text_input)
            im_feat = im_feat / im_feat.norm(dim=-1, keepdim=True)
            tx_feat = tx_feat / tx_feat.norm(dim=-1, keepdim=True)
            sim = torch.cosine_similarity(im_feat, tx_feat).item()
        return sim

    def character_score(self, crop: Image.Image, character: Character) -> float:
        total_weight = 0.0
        weighted_sum = 0.0
        for feature in character.features:
            sim = self._feature_score(crop, feature)
            weighted_sum += sim * feature.weight
            total_weight += feature.weight
        return weighted_sum / total_weight if total_weight > 0 else 0.0

# ----------------------------------------
# 5. Zero-shot-промты для «Evangelion», «Angel», «Scenery»
# ----------------------------------------
zero_shot_labels = {
    "#Evangelion_Mech": "Giant biomechanical humanoid robot from Evangelion, metallic armor, glowing eyes, piloted mecha, NOT human",
    "#Angel":          "Abstract monstrous Alien from Evangelion, glowing core, symmetrical shapes, apocalyptic background",
    "#Scenery":        "Tokyo-3 or GeoFront scenery, buildings, cityscape, sunset or skyline, no visible characters"
}
zs_prompts       = list(zero_shot_labels.values())
zs_tags          = list(zero_shot_labels.keys())
zs_text_inputs   = torch.cat([clip.tokenize(p) for p in zs_prompts]).to(device)

# ----------------------------------------
# 6. Функция детекции «person» (Faster R-CNN)
# ----------------------------------------
def detect_person_boxes(pil_img: Image.Image, threshold: float = 0.7) -> list[tuple]:
    transform = T.Compose([T.ToTensor()])
    img_tensor = transform(pil_img).to(device)
    with torch.no_grad():
        outputs = faster_model([img_tensor])[0]

    boxes = []
    labels = outputs['labels'].cpu().numpy()
    scores = outputs['scores'].cpu().numpy()
    raw_boxes = outputs['boxes'].cpu().numpy()

    for lbl, score, box in zip(labels, scores, raw_boxes):
        if lbl == 1 and score >= threshold:  # COCO: 1 = 'person'
            xmin, ymin, xmax, ymax = box
            boxes.append((int(xmin), int(ymin), int(xmax), int(ymax)))
    return boxes

# ----------------------------------------
# 7. Эвристика по имени файла
# ----------------------------------------
filename_to_tag = {
    "shinji":    "#Shinji_Ikari",
    "gendo":     "#Gendo_Ikari",
    "rei":       "#Rei_Ayanami",
    "ayanami":   "#Rei_Ayanami",
    "asuka":     "#Asuka_Langley",
    "langley":   "#Asuka_Langley",
    "misato":    "#Misato_Katsuragi",
    "katsuragi": "#Misato_Katsuragi",
    "ritsuko":   "#Ritsuko_Akagi",
    "akagi":     "#Ritsuko_Akagi",
    "mari":      "#Mari_Makinami",
    "makinami":  "#Mari_Makinami",
    "kaworu":    "#Kaworu_Nagisa",
    "nagisa":    "#Kaworu_Nagisa",
    "hikari":    "#Hikari_Horaki",
    "horaki":    "#Hikari_Horaki",
    "kaji":      "#Ryoji_Kaji",
    "ryoji":     "#Ryoji_Kaji",
    "angel":     "#Angel",
    "angels":    "#Angel",
    "eva01":     "#Evangelion_Mech",
    "eva-01":    "#Evangelion_Mech",
    "eva00":     "#Evangelion_Mech",
    "eva-00":    "#Evangelion_Mech",
    "eva02":     "#Evangelion_Mech",
    "eva-02":    "#Evangelion_Mech",
    "unit01":    "#Evangelion_Mech",
    "unit-01":   "#Evangelion_Mech",
    "unit00":    "#Evangelion_Mech",
    "unit-00":   "#Evangelion_Mech",
    "unit02":    "#Evangelion_Mech",
    "unit-02":   "#Evangelion_Mech"
}

def infer_tag_from_filename(fname: str) -> str:
    lower = fname.lower()
    for key, tag in filename_to_tag.items():
        if key in lower:
            return tag
    return ""

# ANSI-коды цветов
YELLOW = "\033[93m"
RESET  = "\033[0m"

# ----------------------------------------
# 8. Основной цикл обработки с пометками
# ----------------------------------------
def run_clip_with_weights():
    images_data = load_images_json()
    fb_clip = FeatureBasedCLIP()

    for fname in os.listdir(IMAGES_FOLDER):
        if not fname.lower().endswith(('.png', '.jpg', '.jpeg', '.webp')):
            continue

        full_path = IMAGES_FOLDER / fname
        # Если уже есть готовый tag, пропустим
        if fname in images_data and images_data[fname].get("person"):
            continue

        # Задаём первоначальную структуру
        images_data.setdefault(fname, {
            "person": "",
            "description": "",
            "posted": 0,
            "post_time": None,
            "caption": ""
        })

        # 8.1. Эвристическая проверка по имени файла
        tag_from_name = infer_tag_from_filename(fname)
        if tag_from_name:
            images_data[fname]["person"] = tag_from_name
            if not images_data[fname]["description"]:
                images_data[fname]["description"] = DESCRIPTION_DEFAULT

            shutil.move(str(full_path), str(CHECK_FOLDER / fname))
            print(f"[FILENAME] {fname} → assigned tag: {tag_from_name}")
            continue

        # 8.2. Проверяем: есть ли человек? Если да, фокусируемся на FeatureBasedCLIP
        pil_img = Image.open(full_path).convert("RGB")
        person_boxes = detect_person_boxes(pil_img, threshold=0.7)

        best_tag = "#unknown"
        best_weight = 0.0

        if person_boxes:
            # Если найден человек, анализируем каждый «кроп» под персонажей
            for (xmin, ymin, xmax, ymax) in person_boxes:
                crop = pil_img.crop((xmin, ymin, xmax, ymax)).resize((224, 224))
                for character in fb_clip.characters:
                    score = fb_clip.character_score(crop, character)
                    if score > best_weight:
                        best_weight = score
                        best_tag = character.hashtag
            # Вывод в консоль с жёлтым цветом “CLIP”
            print(f"{YELLOW}[CLIP] {fname} → assigned tag: {best_tag}, weight: {best_weight:.3f}{RESET}")

        else:
            # 8.3. Если людей нет → zero-shot на «Evangelion», «Angel», «Scenery»
            whole = pil_img.resize((224, 224))
            image_input = preprocess(whole).unsqueeze(0).to(device)

            with torch.no_grad():
                im_feat = clip_model.encode_image(image_input)
                tx_feat = clip_model.encode_text(zs_text_inputs)
                logits = (100.0 * im_feat @ tx_feat.T).softmax(dim=-1)[0]
                probs = logits.cpu().numpy()

            idx_max = int(probs.argmax())
            best_weight = float(probs[idx_max])
            best_tag = zs_tags[idx_max]

            # Если ни один из zero-shot не прошёл порог 0.2 → "Scenery"
            if best_weight < 0.2:
                best_tag = "#Scenery"
                best_weight = 1.0

            print(f"{YELLOW}[CLIP] {fname} → assigned tag: {best_tag}, confidence: {best_weight:.3f}{RESET}")

        # 8.4. Записываем результат в JSON
        images_data[fname]["person"] = best_tag
        if not images_data[fname]["description"]:
            images_data[fname]["description"] = DESCRIPTION_DEFAULT

        shutil.move(str(full_path), str(CHECK_FOLDER / fname))

    # 8.5. Сохраняем JSON
    save_images_json(images_data)
    print("CLIP-анализ с весами завершён. JSON обновлён.")

if __name__ == "__main__":
    run_clip_with_weights()
