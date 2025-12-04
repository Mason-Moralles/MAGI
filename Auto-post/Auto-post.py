import os
import json
import shutil
import time
import asyncio
from datetime import datetime, timedelta
from telethon import TelegramClient
from dotenv import load_dotenv
import sys
# Добавляем корень проекта в PYTHONPATH, чтобы найти пакет config
root_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
sys.path.insert(0, root_dir)
from config.config import (TIMEZONE, SCHEDULE_TEMPLATE, CAPTIONS, FORCED_POST_RULES, FORCED_CAPTIONS, SCHEDULE_DAYS, DELAY_BETWEEN)

# Загрузка переменных и путей
load_dotenv()
BASE_DIR      = os.getenv('BASE_DIR', os.getcwd())
DATA_DIR      = os.path.join(BASE_DIR, 'data', 'json')
ART_FOLDER    = os.getenv('CHECK_IMAGES_FOLDER', os.path.join(BASE_DIR, 'Check-Images'))
USED_FOLDER   = os.getenv('POST_IMAGES_FOLDER', os.path.join(BASE_DIR, 'Post-Images'))

# JSON-файлы
IMAGES_JSON   = os.path.join(DATA_DIR, 'images.json')
SCHEDULE_JSON = os.path.join(DATA_DIR, 'schedule.json')
POSTED_JSON   = os.path.join(DATA_DIR, 'posted_images.json')

# TelegramClient
api_id        = int(os.getenv('API_ID'))
api_hash      = os.getenv('API_HASH')
channel       = os.getenv('CHANNEL')
client        = TelegramClient(os.path.join(BASE_DIR, 'data', '.session', 'session'), api_id, api_hash)

# Логирование
import pytz

def log(msg, level='info'):
    ts = datetime.now(TIMEZONE).strftime('%Y-%m-%d %H:%M:%S')
    print(f"[{ts}] [{level.upper():7}] {msg}")

# JSON-утилиты
def load_json(path, default=None):
    if default is None: default = {}
    if not os.path.exists(path): return default
    try:
        with open(path, 'r', encoding='utf-8') as f:
            return json.load(f)
    except:
        return default

def save_json(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False, indent=2)

# Создание слотов в соответствии с шаблоном

def create_slots(schedule_data):
    now = datetime.now(TIMEZONE)
    log(f"Текущее время: {now}", 'info')
    start = now.date()
    end   = start + timedelta(days=SCHEDULE_DAYS)
    slots = []
    d = start
    while d <= end:
        day = d.strftime('%A')
        for t in SCHEDULE_TEMPLATE.get(day, []):
            tm = datetime.strptime(t, "%H:%M").time()
            slot = TIMEZONE.localize(datetime.combine(d, tm))
            if slot <= now or slot.isoformat() in schedule_data:
                continue
            # стандартная подпись
            caption = CAPTIONS.get(t, "")
            # принудительная подпись из конфига
            cap_forced = FORCED_CAPTIONS.get((day, t))
            if cap_forced:
                caption = cap_forced
            slots.append((slot, caption))
        d += timedelta(days=1)
    slots.sort(key=lambda x: x[0])
    log(f"Создано слотов: {len(slots)}", 'success')
    return slots

# Выбор арта на слот

def select_art(images_data, slot_time, last_person):
    weekday, time_str = slot_time.strftime('%A'), slot_time.strftime('%H:%M')
    forced = FORCED_POST_RULES.get((weekday, time_str))
    if forced:
        candidates = [f for f,v in images_data.items() if v['posted']==0 and v['person']==forced]
        if candidates:
            return candidates[0]
        log(f"Нет forced-арта для {forced}", 'warning')
    # обычный выбор: не тот же персонаж подряд
    pool = [f for f,v in images_data.items() if v['posted']==0 and v['person']!=last_person]
    return pool[0] if pool else None

# Постинг и финализация

async def run_post_flow():
    images_data   = load_json(IMAGES_JSON, {})
    schedule_data = load_json(SCHEDULE_JSON, {})
    posted_data   = load_json(POSTED_JSON, {})

    slots = create_slots(schedule_data)
    last_person = None

    async with client:
        if not await client.is_user_authorized():
            await client.start()

        for slot, caption in slots:
            art = select_art(images_data, slot, last_person)
            if not art:
                log("Арты закончились", 'error')
                break

            # Отправка
            await client.send_file(channel,
                                   os.path.join(ART_FOLDER, art),
                                   caption=caption,
                                   schedule=slot)
            log(f"Запланировано: {art} -> {slot}", 'success')

            # Обновление данных
            data = images_data[art]
            data.update({'posted':1, 'post_time':slot.isoformat(), 'caption':caption})
            schedule_data[slot.isoformat()] = {'file': art, 'caption': caption, 'person': data['person']}
            posted_data[art] = data
            del images_data[art]

            save_json(IMAGES_JSON, images_data)
            save_json(SCHEDULE_JSON, schedule_data)
            save_json(POSTED_JSON, posted_data)

            # Архивация файла
            os.makedirs(USED_FOLDER, exist_ok=True)
            shutil.move(os.path.join(ART_FOLDER, art), os.path.join(USED_FOLDER, art))
            last_person = data['person']

            await asyncio.sleep(DELAY_BETWEEN)

    log(f"Готово! Всего запланировано: {len(posted_data)}", 'info')

if __name__ == '__main__':
    asyncio.run(run_post_flow())