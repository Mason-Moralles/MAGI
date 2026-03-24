"""
Auto-post.py v3.0 — MAGI автопостинг в Telegram

Работает ТОЛЬКО через API Gateway (SQLite).
Gateway запускает этот сервис через ProcessManager,
поэтому Gateway гарантированно доступен.

Логика:
  1. Получает активные каналы из БД
  2. Для каждого канала получает pending-слоты
  3. Назначает арт каждому слоту (select_art)
  4. Планирует отправку через IPublisher (Telethon / Bot API)
  5. Обновляет БД: slot → scheduled, image → posted
  6. Перемещает файл Check-Images → Post-Images
"""

import shutil
import asyncio
from datetime import datetime
from pathlib import Path
import os
import sys

ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
sys.path.insert(0, ROOT_DIR)
sys.path.insert(0, os.path.dirname(__file__))

import pytz

from config.gateway_client import GatewayClient
from publishers.factory import create_publisher


# ─────────────────────────────────────────────
#  Утилиты
# ─────────────────────────────────────────────

def log(tz, msg, level="info"):
    ts = datetime.now(tz).strftime("%Y-%m-%d %H:%M:%S")
    print(f"[{ts}] [{level.upper():9}] {msg}", flush=True)


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
#  Публикация одного канала
# ─────────────────────────────────────────────

async def publish_channel(gw: GatewayClient, channel: dict, tz):
    """Публикует pending-слоты для одного канала."""
    ch_id = channel["id"]
    ch_name = channel.get("name", ch_id)
    ch_link = channel.get("link", "")
    publish_mode = channel.get("publishMode", "user")

    log(tz, f"═══ Канал: {ch_name} ({ch_link}) — режим {publish_mode} ═══")

    # ─── Пути из канала ───
    arts_root = channel.get("artsRootPath", "")
    if not arts_root:
        log(tz, f"  artsRootPath не задан для {ch_name}. Пропуск.", "error")
        return 0

    arts_root_path = Path(arts_root)

    # Пробуем оба варианта регистра (EnsureArtsFolderStructure создаёт Check-Images)
    check_images_dir = arts_root_path / "Check-Images"
    if not check_images_dir.exists():
        check_images_dir = arts_root_path / "check-images"
    post_images_dir = arts_root_path / "Post-Images"
    if not post_images_dir.exists():
        post_images_dir = arts_root_path / "post-images"

    if not check_images_dir.exists():
        log(tz, f"  Папка Check-Images не найдена: {arts_root_path}", "error")
        return 0

    # ─── Изображения ───
    raw_images = gw.get_images(unposted_only=True, channel_id=ch_id)
    raw_images_global = gw.get_images(unposted_only=True)
    images_data = {}
    for img in raw_images_global:
        images_data[img["fileName"]] = img
    for img in raw_images:
        images_data[img["fileName"]] = img

    # ─── Pending-слоты ───
    raw_pending = gw.get_pending_slots(channel_id=ch_id)
    pending_slots = sorted(
        [(s["isoKey"], s) for s in raw_pending],
        key=lambda x: x[0]
    )

    log(tz, f"  Артов доступно: {len(images_data)}")
    log(tz, f"  Pending-слотов: {len(pending_slots)}")

    if not pending_slots:
        log(tz, f"  Нет слотов для {ch_name}. Пропуск.", "warning")
        return 0

    # ─── Telegram credentials из канала ───
    api_id = channel.get("apiId")
    api_hash = channel.get("apiHash")
    bot_token = channel.get("botToken")
    session_file = channel.get("sessionFile")

    if not session_file:
        appdata = os.environ.get("APPDATA", os.path.expanduser("~"))
        session_dir = os.path.join(appdata, "MAGI")
        os.makedirs(session_dir, exist_ok=True)
        session_file = os.path.join(session_dir, f"session_{ch_id}")

    if publish_mode == "bot" and not bot_token:
        log(tz, f"  Bot token не задан для {ch_name}. Пропуск.", "error")
        return 0
    if publish_mode == "user" and (not api_id or not api_hash):
        log(tz, f"  API ID/Hash не заданы для {ch_name}. Пропуск.", "error")
        return 0

    publisher = create_publisher(
        publish_mode=publish_mode,
        api_id=api_id,
        api_hash=api_hash,
        bot_token=bot_token,
        session_file=session_file,
    )

    now = datetime.now(tz)
    last_person = None
    scheduled_count = 0
    delay = channel.get("delayBetweenPosts", 5)

    try:
        await publisher.connect()

        for iso_key, slot in pending_slots:
            try:
                slot_dt = datetime.fromisoformat(iso_key)
            except ValueError:
                log(tz, f"  Некорректный ключ: {iso_key}", "error")
                continue

            if slot_dt <= now:
                log(tz, f"  Пропуск прошедшего: {iso_key}", "warning")
                gw.update_slot_status(iso_key, "missed")
                continue

            forced_tag = slot.get("forced_tag") if isinstance(slot, dict) else None
            art = select_art(tz, images_data, forced_tag, last_person)

            if not art:
                log(tz, f"  Арты закончились для {ch_name}.", "error")
                break

            src = check_images_dir / art
            if not src.exists():
                log(tz, f"  Файл не найден: {src} — удаляем запись", "error")
                gw.remove_image(art)
                images_data.pop(art, None)
                continue

            caption = slot.get("caption", "")
            person = images_data[art].get("person", "")

            try:
                await publisher.send_scheduled_photo(
                    channel=ch_link,
                    file_path=src,
                    caption=caption,
                    schedule_time=slot_dt,
                )
            except Exception as ex:
                log(tz, f"  Ошибка отправки {art}: {ex}", "error")
                gw.update_slot_status(iso_key, "error")
                continue

            log(tz, f"  Запланировано: {art} → {slot_dt.strftime('%Y-%m-%d %H:%M %Z')}", "success")

            gw.update_slot_status(iso_key, "scheduled", file=art, person=person, caption=caption)
            gw.mark_image_posted(art, person=person, posted_at=iso_key, caption=caption, channel_id=ch_id)

            post_images_dir.mkdir(parents=True, exist_ok=True)
            shutil.move(str(src), str(post_images_dir / art))

            images_data.pop(art, None)
            last_person = person
            scheduled_count += 1

            await asyncio.sleep(delay)

    finally:
        await publisher.disconnect()

    log(tz, f"  Готово для {ch_name}: {scheduled_count} постов")
    return scheduled_count


# ─────────────────────────────────────────────
#  Основной сценарий
# ─────────────────────────────────────────────

async def run_post_flow():
    """Мультиканальная публикация через Gateway."""
    gw = GatewayClient()

    if not gw.is_gateway_available():
        print("[AUTO-POST] ОШИБКА: API Gateway недоступен. Публикация невозможна.", flush=True)
        return

    # Timezone: берём из первого канала или дефолт
    channels = gw.get_active_channels()
    default_tz = "Europe/Moscow"

    if not channels:
        tz = pytz.timezone(default_tz)
        log(tz, "Нет активных каналов в БД. Нечего публиковать.", "warning")
        return

    # Используем timezone первого канала для логирования
    first_tz = channels[0].get("timeZone", default_tz)
    tz = pytz.timezone(first_tz)
    now = datetime.now(tz)

    log(tz, "=== MAGI Auto-post v3.0 запущен ===")
    log(tz, f"Текущее время: {now.strftime('%Y-%m-%d %H:%M:%S %Z')}")
    log(tz, f"Режим: API Gateway (SQLite)")
    log(tz, f"Найдено каналов: {len(channels)}")

    total = 0
    for channel in channels:
        # Каждый канал может иметь свой timezone
        ch_tz_name = channel.get("timeZone", default_tz)
        try:
            ch_tz = pytz.timezone(ch_tz_name)
        except Exception:
            ch_tz = tz

        count = await publish_channel(gw, channel, ch_tz)
        total += count

    log(tz, f"=== Итого запланировано: {total} постов в {len(channels)} канал(ов) ===")


if __name__ == "__main__":
    asyncio.run(run_post_flow())
