"""
Parser Service — FastAPI-обёртка для парсеров Pinterest и Pixiv.
Порт: 5001

Эндпоинты:
  GET  /health  — проверка доступности
  GET  /status  — статус текущей задачи
  POST /run     — запуск парсинга (фоновая задача)
  POST /stop    — остановка парсинга

Конфигурация парсеров загружается из API Gateway (SQLite) по channel_id.
JSON-файлы больше НЕ используются.
"""

import sys
import os
import threading
import uuid
from datetime import datetime
from pathlib import Path

# Принудительно UTF-8 для Windows
if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

# Gateway client
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from config.gateway_client import GatewayClient

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field

app = FastAPI(
    title="MAGI Parser Service",
    description="Микросервис парсинга изображений с Pinterest и Pixiv (Gateway API mode)",
    version="2.0.0",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)


# ─── Модели ───

class RunRequest(BaseModel):
    model_config = {"populate_by_name": True}

    sources: list[str] = ["pinterest"]
    channel_id: str | None = Field(None, alias="channelId")


class TaskResult(BaseModel):
    task_id: str = ""
    status: str = "idle"  # idle, running, completed, error
    message: str | None = None
    started_at: str | None = None
    completed_at: str | None = None


# ─── Состояние задачи ───

current_task = TaskResult()
_stop_event = threading.Event()
_task_lock = threading.Lock()


def _run_parser_sync(sources: list[str], channel_id: str):
    """Запускает парсеры синхронно в отдельном потоке."""
    global current_task

    gw = GatewayClient()

    try:
        if "pinterest" in sources:
            with _task_lock:
                current_task.message = "Running Pinterest parser..."
            print(f"[Parser Service] Starting Pinterest parser for channel {channel_id}...", flush=True)
            try:
                from PinterestParser import main as pinterest_main
                pinterest_main(channel_id=channel_id, gw=gw)
            except SystemExit:
                pass
            except Exception as e:
                print(f"[Parser Service] Pinterest error: {e}", flush=True)

            if _stop_event.is_set():
                with _task_lock:
                    current_task.status = "completed"
                    current_task.message = "Stopped by user"
                    current_task.completed_at = datetime.utcnow().isoformat()
                return

        if "pixiv" in sources:
            with _task_lock:
                current_task.message = "Running Pixiv parser..."
            print(f"[Parser Service] Starting Pixiv parser for channel {channel_id}...", flush=True)
            try:
                from PixivParser import main as pixiv_main
                pixiv_main(channel_id=channel_id, gw=gw)
            except SystemExit:
                pass
            except Exception as e:
                print(f"[Parser Service] Pixiv error: {e}", flush=True)

        with _task_lock:
            current_task.status = "completed"
            current_task.message = "Parsing completed"
            current_task.completed_at = datetime.utcnow().isoformat()
        print("[Parser Service] Parsing completed.", flush=True)

    except Exception as e:
        with _task_lock:
            current_task.status = "error"
            current_task.message = str(e)
            current_task.completed_at = datetime.utcnow().isoformat()
        print(f"[Parser Service] Error: {e}", flush=True)


# ─── Эндпоинты ───

@app.get("/health")
async def health():
    return {"status": "healthy", "service": "parser-service"}


@app.get("/status")
async def status():
    with _task_lock:
        return current_task.model_dump()


@app.post("/run")
async def run(request: RunRequest | None = None):
    global current_task
    sources = request.sources if request else ["pinterest"]
    channel_id = request.channel_id if request else None

    if not channel_id:
        return {"error": "channel_id is required"}

    with _task_lock:
        if current_task.status == "running":
            return {"error": "Task already running", "task_id": current_task.task_id}

        _stop_event.clear()
        current_task = TaskResult(
            task_id=uuid.uuid4().hex[:8],
            status="running",
            message=f"Starting parsers: {', '.join(sources)} for channel {channel_id}",
            started_at=datetime.utcnow().isoformat(),
        )

    thread = threading.Thread(target=_run_parser_sync, args=(sources, channel_id), daemon=True)
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
    uvicorn.run(app, host="0.0.0.0", port=5001)
