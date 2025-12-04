# Parser-channel/Parser-channel.py
import os
import sys
import json
import shutil
import argparse
import asyncio
from datetime import datetime
from telethon import TelegramClient
from dotenv import load_dotenv

# переключаемся в корень проекта, чтобы находить пакет config
root = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
sys.path.insert(0, root)

from config.config import TIMEZONE  # общий конфиг

# ========== Аргументы ==========
parser = argparse.ArgumentParser(description="Parser-channel microservice")
parser.add_argument('--clean', action='store_true',
                    help='Удалять из отложки арты из Post-Images и возвращать их в Check-Images')
args = parser.parse_args()

# ========== Пути ==========
load_dotenv()
BASE_DIR        = os.getenv('BASE_DIR', os.getcwd())
DATA_DIR        = os.getenv('DATA_JSON_DIR', os.path.join(BASE_DIR, 'data', 'json'))
SCHEDULE_JSON   = os.path.join(DATA_DIR, 'schedule.json')
POST_FOLDER     = os.getenv('POST_IMAGES_FOLDER', os.path.join(BASE_DIR, 'Post-Images'))
CHECK_FOLDER    = os.getenv('CHECK_IMAGES_FOLDER', os.path.join(BASE_DIR, 'Check-Images'))

# ========== Telegram ==========
API_ID       = int(os.getenv('API_ID'))
API_HASH     = os.getenv('API_HASH')
CHANNEL      = os.getenv('CHANNEL')
SESSION_FILE = os.path.join(BASE_DIR, 'data', '.session', os.getenv('SESSION_FILE', 'session'))
client       = TelegramClient(SESSION_FILE, API_ID, API_HASH)

# ========== Утилиты ==========
def load_json(path):
    if not os.path.exists(path):
        return {}
    with open(path, 'r', encoding='utf-8') as f:
        return json.load(f)

def save_json(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False, indent=2)

def log(msg):
    ts = datetime.now(TIMEZONE).strftime('%Y-%m-%d %H:%M:%S')
    print(f"[{ts}] {msg}")

# ========== Основная логика ==========
async def fetch_scheduled():
    msgs = []
    async for m in client.iter_messages(CHANNEL, scheduled=True):
        fname = None
        # Пытаемся вытащить имя файла
        if m.file and m.file.name:
            fname = m.file.name
        elif m.media and hasattr(m.media, 'document'):
            for attr in m.media.document.attributes:
                if hasattr(attr, 'file_name'):
                    fname = attr.file_name
        msgs.append({
            'id': m.id,
            'date': m.date.astimezone(TIMEZONE).isoformat(),
            'file': fname
        })
    return msgs

async def main():
    schedule_data = load_json(SCHEDULE_JSON)
    real_msgs = await fetch_scheduled()

    # Словарь для быстрого поиска по дате
    real_map = {m['date']: m for m in real_msgs}

    removed = []
    # 1) Обычная синхронизация: убрать из schedule.json слоты без реальных msgcd
    for dt in list(schedule_data.keys()):
        if dt not in real_map:
            removed.append(dt)
            del schedule_data[dt]

    log(f"Синхронизация отложки и расписания: Было удалено {len(removed)} постов")

    # 2) Если clean-режим, обрабатываем «откат» из Post-Images
    if args.clean:
        for m in real_msgs:
            dt, fname = m['date'], m['file']
            if not fname:
                log(f"Пропуск: сообщение ID={m['id']} без имени файла")
                continue
            src_path = os.path.join(POST_FOLDER, fname)
            if os.path.exists(src_path):
                # удаление из отложки
                await client.delete_messages(CHANNEL, m['id'])
                # перемещение
                dst = os.path.join(CHECK_FOLDER, fname)
                shutil.move(src_path, dst)
                # чистка schedule
                if dt in schedule_data:
                    del schedule_data[dt]
                log(f"Откат: {fname} (слот {dt}) удалён из канала и перемещён обратно")
            else:
                log(f"Файл {fname} не найден в {POST_FOLDER}, пропущен")

    # Сохраняем обновлённое расписание
    save_json(SCHEDULE_JSON, schedule_data)
    log("Готово.")

if __name__ == '__main__':
    async def _run():
        await client.start()
        await main()
        await client.disconnect()

    import asyncio
    asyncio.run(_run())