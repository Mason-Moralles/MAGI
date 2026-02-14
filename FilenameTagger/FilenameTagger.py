import os
import sys
import json
import shutil
from pathlib import Path

ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
sys.path.insert(0, ROOT_DIR)

from config.config_loader import load_effective_config

def load_json(path: Path, default=None):
    if default is None:
        default = {}
    if not path.exists():
        return default
    try:
        with path.open("r", encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return default

def save_json(path: Path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)

def main():
    cfg = load_effective_config()

    NEW_IMAGES_DIR: Path = cfg.new_images_dir
    CHECK_IMAGES_DIR: Path = cfg.check_images_dir
    IMAGES_JSON: Path = cfg.images_json

    FILENAME_TAGS_JSON: Path = cfg.project_root / "data/json/FilenameTagger/filename_tags.json"
    filename_to_tag = load_json(FILENAME_TAGS_JSON, {})

    NEW_IMAGES_DIR.mkdir(parents=True, exist_ok=True)
    CHECK_IMAGES_DIR.mkdir(parents=True, exist_ok=True)

    images_data = load_json(IMAGES_JSON, {})

    count_files = 0

    for file_path in NEW_IMAGES_DIR.iterdir():
        if not file_path.is_file():
            continue

        fname = file_path.name
        if not fname.lower().endswith((".png", ".jpg", ".jpeg")):
            continue

        # уже обработан ранее
        if fname in images_data and images_data[fname].get("person"):
            continue

        fname_lower = fname.lower()

        matched_tag = None
        for keyword, tag in filename_to_tag.items():
            if keyword in fname_lower:
                matched_tag = tag
                break

        if not matched_tag:
            continue

        images_data[fname] = {
            "person": matched_tag,
            "posted": 0,
            "post_time": None,
            "caption": ""
        }

        print(f"[FILENAME] {fname} -> {matched_tag}")
        count_files += 1

        src = file_path
        dst = CHECK_IMAGES_DIR / fname
        try:
            shutil.move(str(src), str(dst))
        except Exception as e:
            print(f"[ERROR] Не смог переместить {fname}: {e}")

    save_json(IMAGES_JSON, images_data)

    print("\n" + "=" * 50)
    print("ОБРАБОТКА ЗАВЕРШЕНА")
    print(f"Общее количество обработанных файлов: {count_files}")


if __name__ == "__main__":
    main()