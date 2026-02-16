"""
Auto-post.py  —  MAGI автопостинг в Telegram

Формат schedule.json (единый, v2):
{
  "2026-02-17T07:29:00+03:00": {
    "date":    "2026-02-17",
    "time":    "07:29",
    "status":  "pending" | "scheduled" | "posted" | "error",
    "file":    null | "asuka_langley_0001.jpg",
    "person":  null | "#Asuka_Langley",
    "caption": "Доброе утро!"
  }
}

Формат images.json (v2, только нужные поля):
{
  "asuka_langley_0001.jpg": {
    "person": "#Asuka_Langley",
    "posted": 0
  }
}

Логика:
  1. Читает schedule.json — берёт слоты со status="pending"
  2. Назначает арт каждому слоту (select_art), статус → "scheduled"
  3. Планирует отправку через Telegram (schedule=slot_time)
  4. После успешного планирования:
       - schedule[slot]["status"] = "scheduled", file/person заполнены
       - images.json: запись удаляется (арт переходит в оборот)
       - posted_images.json: запись добавляется
       - файл перемещается в Post-Images
"""

import json
import shutil
import asyncio
from datetime import datetime, timedelta
from pathlib import Path
import os
import sys

ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
sys.path.insert(0, ROOT_DIR)

import pytz
from telethon import TelegramClient

from config.config_loader import load_effective_config

# ─────────────────────────────────────────────
#  Утилиты
# ─────────────────────────────────────────────

def log(tz, msg, level="info"):
    ts = datetime.now(tz).strftime("%Y-%m-%d %H:%M:%S")
    print(f"[{ts}] [{level.upper():9}] {msg}")


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


# ─────────────────────────────────────────────
#  Генерация слотов (панель → schedule.json)
#  Используется ТОЛЬКО если schedule.json пуст
#  или не содержит pending-слотов. Основной
#  генератор — кнопка «Применить правила» в панели.
# ─────────────────────────────────────────────

def generate_pending_slots(tz, rules: list[dict], schedule_days: int,
                            existing_keys: set[str]) -> dict:
    """
    Генерирует pending-слоты из posting_rules.json (v2, rules[])
    для случая когда schedule.json пустой / не заполнен панелью.

    rules = [{"time": "07:29", "days": ["Monday","Friday"], "caption": "..."}]
    """
    now = datetime.now(tz)
    new_slots = {}

    for delta in range(schedule_days):
        day_dt = now.date() + timedelta(days=delta)
        day_name = day_dt.strftime("%A")  # "Monday", …

        for rule in rules:
            if day_name not in rule.get("days", []):
                continue
            time_str = rule.get("time", "")
            try:
                tm = datetime.strptime(time_str, "%H:%M").time()
            except ValueError:
                continue

            slot_dt = tz.localize(datetime.combine(day_dt, tm))
            if slot_dt <= now:
                continue

            iso_key = slot_dt.isoformat()
            if iso_key in existing_keys:
                continue

            new_slots[iso_key] = {
                "date":    day_dt.strftime("%Y-%m-%d"),
                "time":    time_str,
                "status":  "pending",
                "file":    None,
                "person":  None,
                "caption": rule.get("caption", ""),
            }

    return new_slots


# ─────────────────────────────────────────────
#  Выбор арта для слота
#  - forced: по тегу персонажа если задан в правиле
#  - обычный: не тот же персонаж подряд
# ─────────────────────────────────────────────

def select_art(tz, images_data: dict, forced_tag: str | None,
               last_person: str | None) -> str | None:
    # Принудительный тег
    if forced_tag:
        candidates = [
            f for f, v in images_data.items()
            if v.get("posted") == 0 and v.get("person") == forced_tag
        ]
        if candidates:
            return candidates[0]
        log(tz, f"Нет арта для forced_tag={forced_tag}, берём из общего пула", "warning")

    # Общий пул — не тот же персонаж подряд
    pool = [
        f for f, v in images_data.items()
        if v.get("posted") == 0 and v.get("person") != last_person
    ]
    if pool:
        return pool[0]

    # Крайний случай: все оставшиеся арты того же персонажа
    fallback = [f for f, v in images_data.items() if v.get("posted") == 0]
    return fallback[0] if fallback else None


# ─────────────────────────────────────────────
#  Основной сценарий
# ─────────────────────────────────────────────

async def run_post_flow():
    cfg = load_effective_config()
    tz = pytz.timezone(cfg.time_zone)
    now = datetime.now(tz)

    log(tz, "=== MAGI Auto-post запущен ===", "info")
    log(tz, f"Текущее время: {now.strftime('%Y-%m-%d %H:%M:%S %Z')}", "info")

    # Загрузка данных
    images_data  = load_json(cfg.images_json, {})
    schedule_data = load_json(cfg.schedule_json, {})
    posted_data  = load_json(cfg.posted_images_json, {})

    log(tz, f"Артов доступно: {sum(1 for v in images_data.values() if v.get('posted') == 0)}", "info")

    # Если schedule.json не содержит pending-слотов — генерируем из правил
    pending_keys = [k for k, v in schedule_data.items() if v.get("status") == "pending"]
    if not pending_keys:
        log(tz, "Pending-слотов нет — генерируем из posting_rules.json", "info")
        new_slots = generate_pending_slots(
            tz=tz,
            rules=cfg.rules,
            schedule_days=cfg.schedule_days,
            existing_keys=set(schedule_data.keys()),
        )
        if new_slots:
            schedule_data.update(new_slots)
            save_json(cfg.schedule_json, schedule_data)
            log(tz, f"Сгенерировано {len(new_slots)} новых слотов", "info")
        pending_keys = [k for k, v in schedule_data.items() if v.get("status") == "pending"]

    if not pending_keys:
        log(tz, "Нет слотов для планирования. Завершение.", "warning")
        return

    # Сортируем pending-слоты по времени
    pending_slots = sorted(
        [(k, schedule_data[k]) for k in pending_keys],
        key=lambda x: x[0]  # ISO-строка сортируется лексикографически = по времени
    )

    log(tz, f"Pending-слотов для обработки: {len(pending_slots)}", "info")

    # Telegram-клиент
    client = TelegramClient(str(cfg.session_file), cfg.api_id, cfg.api_hash)
    last_person = None
    scheduled_count = 0

    async with client:
        if cfg.telegram_mode == "bot":
            await client.start(bot_token=cfg.bot_token)
        else:
            if not await client.is_user_authorized():
                await client.start()

        for iso_key, slot in pending_slots:
            # Парсим время слота
            try:
                slot_dt = datetime.fromisoformat(iso_key)
            except ValueError:
                log(tz, f"Некорректный ключ слота: {iso_key}", "error")
                continue

            # Слоты в прошлом пропускаем (помечаем как missed)
            if slot_dt <= now:
                log(tz, f"Пропуск прошедшего слота: {iso_key}", "warning")
                schedule_data[iso_key]["status"] = "missed"
                save_json(cfg.schedule_json, schedule_data)
                continue

            # Выбор арта
            forced_tag = slot.get("forced_tag")  # опционально, если панель задала
            art = select_art(tz, images_data, forced_tag, last_person)

            if not art:
                log(tz, "Арты закончились. Остановка.", "error")
                break

            # Проверка наличия файла
            src = cfg.check_images_dir / art
            if not src.exists():
                log(tz, f"Файл не найден: {src} — удаляем запись", "error")
                images_data.pop(art, None)
                save_json(cfg.images_json, images_data)
                continue

            caption = slot.get("caption", "")
            person  = images_data[art].get("person", "")

            # Планируем пост в Telegram
            try:
                await client.send_file(
                    cfg.channel_link,
                    str(src),
                    caption=caption,
                    schedule=slot_dt,
                )
            except Exception as ex:
                log(tz, f"Ошибка отправки {art}: {ex}", "error")
                schedule_data[iso_key]["status"] = "error"
                save_json(cfg.schedule_json, schedule_data)
                continue

            log(tz, f"Запланировано: {art}  →  {slot_dt.strftime('%Y-%m-%d %H:%M %Z')}", "success")

            # ── Обновление schedule.json ──
            schedule_data[iso_key].update({
                "status": "scheduled",
                "file":   art,
                "person": person,
            })
            save_json(cfg.schedule_json, schedule_data)

            # ── Обновление images.json: убираем запись (арт уходит в архив) ──
            images_data.pop(art, None)
            save_json(cfg.images_json, images_data)

            # ── posted_images.json: запись о публикации ──
            posted_data[art] = {
                "person":    person,
                "posted_at": iso_key,
                "caption":   caption,
            }
            save_json(cfg.posted_images_json, posted_data)

            # ── Перемещение файла в Post-Images ──
            cfg.post_images_dir.mkdir(parents=True, exist_ok=True)
            shutil.move(str(src), str(cfg.post_images_dir / art))

            last_person = person
            scheduled_count += 1

            await asyncio.sleep(cfg.delay_between_post_sec)

    log(tz, f"=== Готово! Запланировано постов: {scheduled_count} ===", "info")


if __name__ == "__main__":
    asyncio.run(run_post_flow())
