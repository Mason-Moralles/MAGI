"""
Auto-post.py  —  MAGI автопостинг в Telegram

Режимы работы:
  1. Через API Gateway (рекомендуется): читает/пишет данные через HTTP в SQLite
  2. Fallback на JSON: если Gateway недоступен — работает напрямую с JSON-файлами

Логика:
  1. Читает pending-слоты из расписания
  2. Назначает арт каждому слоту (select_art), статус → "scheduled"
  3. Планирует отправку через Telegram (schedule=slot_time)
  4. После успешного планирования обновляет базу данных
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


def _get_gateway_client():
    """Пытается подключиться к API Gateway."""
    try:
        from config.gateway_client import GatewayClient
        gw = GatewayClient()
        if gw.is_gateway_available():
            return gw
    except Exception:
        pass
    return None


# ─────────────────────────────────────────────
#  Генерация слотов
# ─────────────────────────────────────────────

def generate_pending_slots(tz, rules: list[dict], schedule_days: int,
                            existing_keys: set[str]) -> dict:
    now = datetime.now(tz)
    new_slots = {}

    for delta in range(schedule_days):
        day_dt = now.date() + timedelta(days=delta)
        day_name = day_dt.strftime("%A")

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
# ─────────────────────────────────────────────

def select_art(tz, images_data: dict, forced_tag: str | None,
               last_person: str | None) -> str | None:
    if forced_tag:
        candidates = [
            f for f, v in images_data.items()
            if v.get("posted") == 0 and v.get("person") == forced_tag
        ]
        if candidates:
            return candidates[0]
        log(tz, f"Нет арта для forced_tag={forced_tag}, берём из общего пула", "warning")

    pool = [
        f for f, v in images_data.items()
        if v.get("posted") == 0 and v.get("person") != last_person
    ]
    if pool:
        return pool[0]

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

    # Определяем режим: Gateway или JSON fallback
    gw = _get_gateway_client()
    use_gateway = gw is not None

    if use_gateway:
        log(tz, "Режим: API Gateway (SQLite)", "info")
        # Загрузка данных через Gateway
        raw_images = gw.get_images(unposted_only=True)
        images_data = {img["fileName"]: img for img in raw_images}

        raw_pending = gw.get_pending_slots()
        pending_slots = sorted(
            [(s["isoKey"], s) for s in raw_pending],
            key=lambda x: x[0]
        )

        log(tz, f"Артов доступно: {len(images_data)}", "info")
        log(tz, f"Pending-слотов: {len(pending_slots)}", "info")

        # Если нет pending — не генерируем (это делает панель)
        if not pending_slots:
            log(tz, "Нет слотов для планирования. Завершение.", "warning")
            return
    else:
        log(tz, "Режим: JSON fallback (Gateway недоступен)", "warning")
        images_data  = load_json(cfg.images_json, {})
        schedule_data = load_json(cfg.schedule_json, {})
        posted_data  = load_json(cfg.posted_images_json, {})

        log(tz, f"Артов доступно: {sum(1 for v in images_data.values() if v.get('posted') == 0)}", "info")

        pending_keys = [k for k, v in schedule_data.items() if v.get("status") == "pending"]
        if not pending_keys:
            log(tz, "Pending-слотов нет — генерируем из posting_rules.json", "info")
            new_slots = generate_pending_slots(
                tz=tz, rules=cfg.rules, schedule_days=cfg.schedule_days,
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

        pending_slots = sorted(
            [(k, schedule_data[k]) for k in pending_keys],
            key=lambda x: x[0]
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
            try:
                slot_dt = datetime.fromisoformat(iso_key)
            except ValueError:
                log(tz, f"Некорректный ключ слота: {iso_key}", "error")
                continue

            if slot_dt <= now:
                log(tz, f"Пропуск прошедшего слота: {iso_key}", "warning")
                if use_gateway:
                    gw.update_slot_status(iso_key, "missed")
                else:
                    schedule_data[iso_key]["status"] = "missed"
                    save_json(cfg.schedule_json, schedule_data)
                continue

            forced_tag = slot.get("forced_tag") if isinstance(slot, dict) else None
            art = select_art(tz, images_data, forced_tag, last_person)

            if not art:
                log(tz, "Арты закончились. Остановка.", "error")
                break

            src = cfg.check_images_dir / art
            if not src.exists():
                log(tz, f"Файл не найден: {src} — удаляем запись", "error")
                if use_gateway:
                    gw.remove_image(art)
                else:
                    images_data.pop(art, None)
                    save_json(cfg.images_json, images_data)
                continue

            caption = slot.get("caption", "")
            person = images_data[art].get("person", "")

            try:
                await client.send_file(
                    cfg.channel_link,
                    str(src),
                    caption=caption,
                    schedule=slot_dt,
                )
            except Exception as ex:
                log(tz, f"Ошибка отправки {art}: {ex}", "error")
                if use_gateway:
                    gw.update_slot_status(iso_key, "error")
                else:
                    schedule_data[iso_key]["status"] = "error"
                    save_json(cfg.schedule_json, schedule_data)
                continue

            log(tz, f"Запланировано: {art}  →  {slot_dt.strftime('%Y-%m-%d %H:%M %Z')}", "success")

            # Обновление данных
            if use_gateway:
                gw.update_slot_status(iso_key, "scheduled", file=art, person=person, caption=caption)
                gw.mark_image_posted(art, person=person, posted_at=iso_key, caption=caption)
            else:
                schedule_data[iso_key].update({"status": "scheduled", "file": art, "person": person})
                save_json(cfg.schedule_json, schedule_data)
                images_data.pop(art, None)
                save_json(cfg.images_json, images_data)
                posted_data[art] = {"person": person, "posted_at": iso_key, "caption": caption}
                save_json(cfg.posted_images_json, posted_data)

            # Перемещение файла
            cfg.post_images_dir.mkdir(parents=True, exist_ok=True)
            shutil.move(str(src), str(cfg.post_images_dir / art))

            # Удаляем из локального кэша
            images_data.pop(art, None)

            last_person = person
            scheduled_count += 1

            await asyncio.sleep(cfg.delay_between_post_sec)

    log(tz, f"=== Готово! Запланировано постов: {scheduled_count} ===", "info")


if __name__ == "__main__":
    asyncio.run(run_post_flow())
