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

app = FastAPI(
    title="MAGI Tagger Service",
    description="Микросервис тегирования изображений по именам файлов",
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
    files_processed: int = 0


# ─── Состояние задачи ───

current_task = TaskResult()
_task_lock = threading.Lock()


def _run_tagger_sync():
    """Запускает FilenameTagger синхронно в отдельном потоке."""
    global current_task

    try:
        print("[Tagger Service] Starting FilenameTagger...", flush=True)

        from FilenameTagger import main as tagger_main
        tagger_main()

        with _task_lock:
            current_task.status = "completed"
            current_task.message = "Tagging completed"
            current_task.completed_at = datetime.utcnow().isoformat()
        print("[Tagger Service] Tagging completed.", flush=True)

    except Exception as e:
        with _task_lock:
            current_task.status = "error"
            current_task.message = str(e)
            current_task.completed_at = datetime.utcnow().isoformat()
        print(f"[Tagger Service] Error: {e}", flush=True)


# ─── Эндпоинты ───

@app.get("/health")
async def health():
    return {"status": "healthy", "service": "tagger-service"}


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

        current_task = TaskResult(
            task_id=uuid.uuid4().hex[:8],
            status="running",
            message="Starting tagger...",
            started_at=datetime.utcnow().isoformat(),
        )

    thread = threading.Thread(target=_run_tagger_sync, daemon=True)
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
            current_task.completed_at = datetime.utcnow().isoformat()

    return {"status": "stopped"}


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=5002)
