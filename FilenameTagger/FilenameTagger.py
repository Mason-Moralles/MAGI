"""
FilenameTagger v2.0 — тегирование изображений по ключевым словам в имени файла.

Работает через API Gateway:
  1. Получает channel → artsRootPath (где New-Images / Check-Images)
  2. Получает filename-теги из БД (per-channel)
  3. Сканирует New-Images, матчит по ключевым словам
  4. Записывает результат в БД через Gateway
  5. Перемещает файл New-Images → Check-Images
"""

import os
import sys
import shutil
from pathlib import Path

ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
sys.path.insert(0, ROOT_DIR)


def _get_gateway_client():
    """Подключается к API Gateway."""
    try:
        from config.gateway_client import GatewayClient
        gw = GatewayClient()
        if gw.is_gateway_available():
            return gw
    except Exception:
        pass
    return None


def main(channel_id: str = None, arts_root_path: str = None):
    """
    Основная функция тегирования.

    Args:
        channel_id: ID канала (обязателен для получения тегов из БД)
        arts_root_path: Путь к корневой папке артов (если не указан — берётся из канала)
    """
    gw = _get_gateway_client()
    if gw is None:
        print("[TAGGER] ОШИБКА: API Gateway недоступен", flush=True)
        return 0

    print("[TAGGER] Режим: API Gateway (SQLite)", flush=True)

    # ─── Определяем arts_root ───
    if not arts_root_path and channel_id:
        channel = gw.get_channel(channel_id)
        if channel:
            arts_root_path = channel.get("artsRootPath", "")

    if not arts_root_path:
        print("[TAGGER] ОШИБКА: artsRootPath не задан", flush=True)
        return 0

    arts_root = Path(arts_root_path)
    new_images_dir = arts_root / "New-Images"
    check_images_dir = arts_root / "Check-Images"

    # Fallback на lowercase если папки с заглавными не существуют
    if not new_images_dir.exists():
        new_images_dir = arts_root / "new-images"
    if not check_images_dir.exists():
        check_images_dir = arts_root / "check-images"

    new_images_dir.mkdir(parents=True, exist_ok=True)
    check_images_dir.mkdir(parents=True, exist_ok=True)

    print(f"[TAGGER] Arts root: {arts_root}", flush=True)
    print(f"[TAGGER] New-Images: {new_images_dir}", flush=True)
    print(f"[TAGGER] Check-Images: {check_images_dir}", flush=True)

    # ─── Загружаем filename-теги из БД ───
    filename_to_tag = {}

    if channel_id:
        try:
            tags_list = gw.get_filename_tags(channel_id)
            for t in tags_list:
                keyword = t.get("keyword", "").lower().strip()
                tag = t.get("tag", "").strip()
                if keyword and tag:
                    filename_to_tag[keyword] = tag
            print(f"[TAGGER] Загружено тегов из БД: {len(filename_to_tag)}", flush=True)
        except Exception as e:
            print(f"[TAGGER] Ошибка загрузки тегов из БД: {e}", flush=True)

    if not filename_to_tag:
        print("[TAGGER] ПРЕДУПРЕЖДЕНИЕ: Нет тегов для этого канала. Добавьте теги в настройках теггера.", flush=True)
        return 0

    # ─── Получаем уже обработанные файлы ───
    existing_params = {}
    if channel_id:
        existing_params["channel_id"] = channel_id
    existing_images = {img["fileName"] for img in gw.get_images(**existing_params)}

    # Также проверяем глобальный пул (без channel_id)
    all_images = {img["fileName"] for img in gw.get_images()}
    existing_images = existing_images | all_images

    count_files = 0

    for file_path in new_images_dir.iterdir():
        if not file_path.is_file():
            continue

        fname = file_path.name
        if not fname.lower().endswith((".png", ".jpg", ".jpeg", ".webp")):
            continue

        # Уже обработан ранее
        if fname in existing_images:
            # Файл уже в БД, но ещё в New-Images → просто перемещаем
            dst = check_images_dir / fname
            if not dst.exists():
                try:
                    shutil.move(str(file_path), str(dst))
                    print(f"[MOVE] {fname} (уже в БД, перемещён)", flush=True)
                except Exception as e:
                    print(f"[ERROR] Не смог переместить {fname}: {e}", flush=True)
            continue

        fname_lower = fname.lower()

        matched_tag = None
        for keyword, tag in filename_to_tag.items():
            if keyword in fname_lower:
                matched_tag = tag
                break

        if not matched_tag:
            continue

        # Записываем в БД через Gateway
        try:
            gw.add_image(fname, matched_tag, channel_id=channel_id)
        except Exception as e:
            print(f"[ERROR] Не смог записать в Gateway {fname}: {e}", flush=True)
            continue

        print(f"[FILENAME] {fname} -> {matched_tag}", flush=True)
        count_files += 1

        # Перемещаем New-Images → Check-Images
        dst = check_images_dir / fname
        try:
            shutil.move(str(file_path), str(dst))
        except Exception as e:
            print(f"[ERROR] Не смог переместить {fname}: {e}", flush=True)

    print("", flush=True)
    print("=" * 50, flush=True)
    print("ОБРАБОТКА ЗАВЕРШЕНА", flush=True)
    print(f"Общее количество обработанных файлов: {count_files}", flush=True)
    print("Данные сохранены в SQLite через API Gateway", flush=True)

    return count_files


if __name__ == "__main__":
    # Для запуска из командной строки: python FilenameTagger.py <channel_id> [arts_root_path]
    ch_id = sys.argv[1] if len(sys.argv) > 1 else None
    arts_root = sys.argv[2] if len(sys.argv) > 2 else None

    if not ch_id:
        print("Использование: python FilenameTagger.py <channel_id> [arts_root_path]")
        sys.exit(1)

    main(channel_id=ch_id, arts_root_path=arts_root)
