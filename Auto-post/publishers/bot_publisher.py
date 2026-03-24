"""
BotApiPublisher — публикация через Telegram Bot API.

Режим 'bot': использует aiohttp для прямого вызова Bot API.
Не требует дополнительных библиотек (aiogram/python-telegram-bot).

Ограничения Bot API:
  - НЕ поддерживает schedule (отложенная отправка) — отправляется сразу.
  - Бот должен быть администратором канала.
"""

import asyncio
from datetime import datetime
from pathlib import Path
from typing import Optional

import aiohttp

from .base import IPublisher


BOT_API_BASE = "https://api.telegram.org"


class BotApiPublisher(IPublisher):
    """Публикатор через Telegram Bot API."""

    def __init__(self, bot_token: str):
        if not bot_token:
            raise ValueError("BotApiPublisher: bot_token обязателен")
        self._bot_token = bot_token
        self._session: Optional[aiohttp.ClientSession] = None

    async def connect(self) -> None:
        self._session = aiohttp.ClientSession()
        # Проверяем токен
        url = f"{BOT_API_BASE}/bot{self._bot_token}/getMe"
        async with self._session.get(url) as resp:
            data = await resp.json()
            if not data.get("ok"):
                raise RuntimeError(f"BotApiPublisher: невалидный токен — {data}")

    async def disconnect(self) -> None:
        if self._session:
            await self._session.close()
            self._session = None

    async def send_scheduled_photo(
        self,
        channel: str,
        file_path: Path,
        caption: str,
        schedule_time: datetime,
    ) -> bool:
        """
        Bot API не поддерживает отложенную отправку.
        Ждём до schedule_time и отправляем в реальном времени.
        """
        now = datetime.now(schedule_time.tzinfo)
        delay = (schedule_time - now).total_seconds()
        if delay > 0:
            # Ждём до нужного времени
            await asyncio.sleep(delay)

        return await self.send_photo(channel, file_path, caption)

    async def send_photo(
        self,
        channel: str,
        file_path: Path,
        caption: str,
    ) -> bool:
        if not self._session:
            raise RuntimeError("BotApiPublisher: не подключен. Вызовите connect().")

        url = f"{BOT_API_BASE}/bot{self._bot_token}/sendPhoto"

        data = aiohttp.FormData()
        data.add_field("chat_id", channel)
        data.add_field("caption", caption)
        data.add_field(
            "photo",
            open(file_path, "rb"),
            filename=file_path.name,
            content_type="image/jpeg",
        )

        async with self._session.post(url, data=data) as resp:
            result = await resp.json()
            if not result.get("ok"):
                raise RuntimeError(f"Bot API error: {result.get('description', 'unknown')}")
            return True

    @property
    def mode(self) -> str:
        return "bot"
