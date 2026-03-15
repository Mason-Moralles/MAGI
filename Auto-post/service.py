"""
Publisher Service — FastAPI-обёртка для Auto-post.
Порт: 5003

Эндпоинты:
  GET  /health  — проверка доступности
  GET  /status  — статус текущей задачи
  POST /run     — запуск публикации (фоновая задача)
  POST /stop    — остановка публикации
"""

import asyncio
import sys
import os
import threading
import uuid
from datetime import datetime

# Принудительно UTF-8 для Windows
if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

# Добавляем корень проекта в sys.path
ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
if ROOT_DIR not in sys.path:
    sys.path.insert(0, ROOT_DIR)

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

# Импортируем основную логику Auto-post
# Используем относительный путь — service.py лежит рядом с Auto-post.py
sys.path.insert(0, os.path.dirname(__file__))

app = FastAPI(
    title="MAGI Publisher Service",
    description="Микросервис публикации контента в Telegram-каналы",
    version="1.0.0",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)


# ─── Модели ───

class TaskResult(BaseModel):
    task_id: str = ""
    status: str = "idle"  # idle, running, completed, error
    message: str | None = None
    started_at: str | None = None
    completed_at: str | None = None
    scheduled_count: int = 0


# ─── Состояние задачи ───

current_task = TaskResult()
_stop_event = threading.Event()
_task_lock = threading.Lock()


def _run_publisher_sync():
    """Запускает Auto-post синхронно в отдельном потоке."""
    global current_task

    try:
        print("[Publisher Service] Starting Auto-post...", flush=True)

        # Auto-post использует asyncio, запускаем в новом event loop
        loop = asyncio.new_event_loop()
        asyncio.set_event_loop(loop)

        try:
            # Импорт внутри функции чтобы не было конфликта с event loop FastAPI
            # Берём имя модуля с учётом дефиса в имени файла
            import importlib
            auto_post = importlib.import_module("Auto-post")
            loop.run_until_complete(auto_post.run_post_flow())
        finally:
            loop.close()

        with _task_lock:
            current_task.status = "completed"
            current_task.message = "Publishing completed"
            current_task.completed_at = datetime.utcnow().isoformat()
        print("[Publisher Service] Publishing completed.", flush=True)

    except Exception as e:
        with _task_lock:
            current_task.status = "error"
            current_task.message = str(e)
            current_task.completed_at = datetime.utcnow().isoformat()
        print(f"[Publisher Service] Error: {e}", flush=True)


# ─── Эндпоинты ───

@app.get("/health")
async def health():
    return {"status": "healthy", "service": "publisher-service"}


@app.get("/status")
async def status():
    with _task_lock:
        return current_task.model_dump()


@app.post("/run")
async def run():
    global current_task

    with _task_lock:
        if current_task.status == "running":
            return {"error": "Task already running", "task_id": current_task.task_id}

        _stop_event.clear()
        current_task = TaskResult(
            task_id=uuid.uuid4().hex[:8],
            status="running",
            message="Starting publisher...",
            started_at=datetime.utcnow().isoformat(),
        )

    thread = threading.Thread(target=_run_publisher_sync, daemon=True)
    thread.start()

    return current_task.model_dump()


@app.post("/stop")
async def stop():
    global current_task
    _stop_event.set()

    with _task_lock:
        if current_task.status == "running":
            current_task.status = "completed"
            current_task.message = "Stopped by user"
            current_task.completed_at = datetime.utcnow().isoformat()

    return {"status": "stopped"}


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=5003)
