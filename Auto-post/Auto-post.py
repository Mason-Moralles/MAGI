import json
import shutil
import asyncio
from datetime import datetime, timedelta
from pathlib import Path
import os
import sys

DELAY_BETWEEN_POST_SEC = 5
ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
sys.path.insert(0, ROOT_DIR)

import pytz
from telethon import TelegramClient

from config.config_loader import load_effective_config

def log(tz, msg, level="info"):
    ts = datetime.now(tz).strftime("%Y-%m-%d %H:%M:%S")
    print(f"[{ts}] [{level.upper():7}] {msg}")


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

#Сохранение JSON-файла с созданием директорий
def save_json(path: Path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)

# Преобразование списка forced-правил в словарь
#[{day, time, tag/caption}] -> {(day,time): value}
def _forced_map_from_list(items: list[dict], value_key: str) -> dict[tuple[str, str], str]:
    out = {}
    for it in items or []:
        day = (it.get("day") or "").strip()
        t = (it.get("time") or "").strip()
        val = (it.get(value_key) or "").strip()
        if day and t and val:
            out[(day, t)] = val
    return out
# ============================================================
# Генерация временных слотов публикаций
# - учитывает расписание по дням недели
# - исключает уже существующие слоты
# - применяет принудительные подписи
# ============================================================
def create_slots(tz, schedule_data, schedule_days, week_template, captions_by_time, forced_captions_map):
    now = datetime.now(tz)
    log(tz, f"Текущее время: {now}", "info")

    start = now.date()
    end = start + timedelta(days=schedule_days)

    slots = []
    d = start
    while d <= end:
        day = d.strftime("%A")  # Monday/Tuesday...
        for t in week_template.get(day, []):
            tm = datetime.strptime(t, "%H:%M").time()
            slot = tz.localize(datetime.combine(d, tm))

            # Пропуск прошедших и уже запланированных слотов
            if slot <= now or slot.isoformat() in schedule_data:
                continue
            # Дефолтная подпись
            caption = captions_by_time.get(t, "")
            # Принудительная подпись (если задана)
            cap_forced = forced_captions_map.get((day, t))
            if cap_forced:
                caption = cap_forced
            # Сортировка по времени
            slots.append((slot, caption))
            d += timedelta(days=1)

    slots.sort(key=lambda x: x[0])
    log(tz, f"Создано слотов: {len(slots)}", "success")
    return slots
# ============================================================
# Выбор арта для конкретного слота
# - сначала forced-правила
# - затем обычный выбор (без повторения персонажа подряд)
# ============================================================
def select_art(tz, images_data, slot_time, last_person, forced_posts_map):
    weekday, time_str = slot_time.strftime("%A"), slot_time.strftime("%H:%M")
    # Принудительный тег персонажа
    forced_tag = forced_posts_map.get((weekday, time_str))
    if forced_tag:
        candidates = [
            f for f, v in images_data.items()
            if v.get("posted") == 0 and v.get("person") == forced_tag
        ]
        if candidates:
            return candidates[0]
        log(tz, f"Нет forced-арта для {forced_tag}", "warning")

    # обычный выбор: не тот же персонаж подряд
    pool = [
        f for f, v in images_data.items()
        if v.get("posted") == 0 and v.get("person") != last_person
    ]
    return pool[0] if pool else None

# ============================================================
# Основной сценарий автопостинга
# ============================================================
async def run_post_flow():
    cfg = load_effective_config()
    tz = pytz.timezone(cfg.time_zone)

    # JSON data
    images_data = load_json(cfg.images_json, {})
    schedule_data = load_json(cfg.schedule_json, {})
    posted_data = load_json(cfg.posted_images_json, {})

    forced_posts_map = _forced_map_from_list(cfg.forced_posts, "tag")
    forced_captions_map = _forced_map_from_list(cfg.forced_captions, "caption")

    slots = create_slots(
        tz=tz,
        schedule_data=schedule_data,
        schedule_days=cfg.schedule_days,
        week_template=cfg.week_template,
        captions_by_time=cfg.captions_by_time,
        forced_captions_map=forced_captions_map
    )

    last_person = None

    client = TelegramClient(str(cfg.session_file), cfg.api_id, cfg.api_hash)

    async with client:
        # Авторизация
        if cfg.telegram_mode == "bot":
            await client.start(bot_token=cfg.bot_token)
        else:
            if not await client.is_user_authorized():
                await client.start()

        # Основной цикл публикации
        for slot, caption in slots:
            art = select_art(tz, images_data, slot, last_person, forced_posts_map)
            if not art:
                log(tz, "Арты закончились", "error")
                break

            src = cfg.check_images_dir / art
            if not src.exists():
                log(tz, f"Файл не найден в check-images: {src}", "error")
                images_data.pop(art, None)
                save_json(cfg.images_json, images_data)
                continue

            # Планирование поста
            await client.send_file(
                cfg.channel_link,
                str(src),
                caption=caption,
                schedule=slot
            )

            log(tz, f"Запланировано: {art} -> {slot}", "success")

            # Обновление БД
            data = images_data.get(art, {})
            data.update({
                "posted": 1,
                "post_time": slot.isoformat(),
                "caption": caption
            })

            schedule_data[slot.isoformat()] = {
                "file": art,
                "caption": caption,
                "person": data.get("person")
            }

            posted_data[art] = data
            images_data.pop(art, None)

            save_json(cfg.images_json, images_data)
            save_json(cfg.schedule_json, schedule_data)
            save_json(cfg.posted_images_json, posted_data)

            # Перемещение арта в архив
            cfg.post_images_dir.mkdir(parents=True, exist_ok=True)
            shutil.move(str(src), str(cfg.post_images_dir / art))

            last_person = data.get("person")

            # Задержка между планированием постов
            await asyncio.sleep(DELAY_BETWEEN_POST_SEC)

    log(tz, f"Готово! Всего запланировано: {len(posted_data)}", "info")

if __name__ == "__main__":
    asyncio.run(run_post_flow())