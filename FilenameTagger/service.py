"""
Tagger Service — FastAPI-обёртка для FilenameTagger.
Порт: 5002

Эндпоинты:
  GET  /health  — проверка доступности
  GET  /status  — статус текущей задачи
  POST /run     — запуск тегирования (фоновая задача)
  POST /stop    — остановка тегирования
"""

import sys
import os
import threading
import uuid
from datetime import datetime, UTC

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

app = FastAPI(
    title="MAGI Tagger Service",
    description="Микросервис тегирования изображений по именам файлов",
    version="2.0.0",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)


# ─── Модели ───

class ChannelConfig(BaseModel):
    """Per-channel конфигурация теггера, передаётся Gateway при запуске."""
    channel_id: str | None = None
    rename_template: str = "{artist}_{title}_{id}"
    separator: str = "_"
    only_new: bool = True
    mode: str = "rename"
    arts_root_path: str | None = None


class RunRequest(BaseModel):
    channel_config: ChannelConfig | None = None


class TaskResult(BaseModel):
    task_id: str = ""
    status: str = "idle"  # idle, running, completed, error
    message: str | None = None
    started_at: str | None = None
    completed_at: str | None = None
    files_processed: int = 0


# ─── Состояние задачи ───

current_task = TaskResult()
_task_lock = threading.Lock()


def _run_tagger_sync(channel_config: dict | None = None):
    """Запускает FilenameTagger синхронно в отдельном потоке."""
    global current_task

    try:
        channel_id = None
        arts_root_path = None

        if channel_config:
            channel_id = channel_config.get("channel_id")
            arts_root_path = channel_config.get("arts_root_path")
            print(f"[Tagger Service] Channel config applied: {channel_id}", flush=True)

        print("[Tagger Service] Starting FilenameTagger...", flush=True)

        from FilenameTagger import main as tagger_main
        count = tagger_main(channel_id=channel_id, arts_root_path=arts_root_path)

        with _task_lock:
            current_task.status = "completed"
            current_task.message = f"Tagging completed: {count or 0} files"
            current_task.files_processed = count or 0
            current_task.completed_at = datetime.now(UTC).isoformat()
        print(f"[Tagger Service] Tagging completed. Files processed: {count or 0}", flush=True)

    except Exception as e:
        with _task_lock:
            current_task.status = "error"
            current_task.message = str(e)
            current_task.completed_at = datetime.now(UTC).isoformat()
        print(f"[Tagger Service] Error: {e}", flush=True)
        import traceback
        traceback.print_exc()


# ─── Эндпоинты ───

@app.get("/health")
async def health():
    return {"status": "healthy", "service": "tagger-service"}


@app.get("/status")
async def status():
    with _task_lock:
        return current_task.model_dump()


@app.post("/run")
async def run(request: RunRequest | None = None):
    global current_task
    ch_config = request.channel_config.model_dump() if request and request.channel_config else None

    with _task_lock:
        if current_task.status == "running":
            return {"error": "Task already running", "task_id": current_task.task_id}

        current_task = TaskResult(
            task_id=uuid.uuid4().hex[:8],
            status="running",
            message="Starting tagger...",
            started_at=datetime.now(UTC).isoformat(),
        )

    thread = threading.Thread(target=_run_tagger_sync, args=(ch_config,), daemon=True)
    thread.start()

    return current_task.model_dump()


@app.post("/stop")
async def stop():
    """Tagger выполняется быстро, но эндпоинт реализован для единообразия API."""
    global current_task

    with _task_lock:
        if current_task.status == "running":
            current_task.status = "completed"
            current_task.message = "Stopped by user"
            current_task.completed_at = datetime.now(UTC).isoformat()

    return {"status": "stopped"}


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=5002)
