"""
TelethonPublisher — публикация через пользовательский аккаунт Telegram (Telethon).

Режим 'user': использует Telethon API для планирования отложенных публикаций.
Поддерживает schedule (отложенная отправка через Telegram Scheduled Messages).
"""

from datetime import datetime
from pathlib import Path
from typing import Optional

from telethon import TelegramClient

from .base import IPublisher


class TelethonPublisher(IPublisher):
    """Публикатор через Telethon (user-mode)."""

    def __init__(
        self,
        api_id: int,
        api_hash: str,
        session_file: str,
        bot_token: Optional[str] = None,
    ):
        self._api_id = api_id
        self._api_hash = api_hash
        self._session_file = session_file
        self._bot_token = bot_token
        self._client: Optional[TelegramClient] = None

    async def connect(self) -> None:
        self._client = TelegramClient(self._session_file, self._api_id, self._api_hash)
        await self._client.connect()

        if self._bot_token:
            await self._client.start(bot_token=self._bot_token)
        else:
            if not await self._client.is_user_authorized():
                await self._client.start()

    async def disconnect(self) -> None:
        if self._client:
            await self._client.disconnect()
            self._client = None

    async def send_scheduled_photo(
        self,
        channel: str,
        file_path: Path,
        caption: str,
        schedule_time: datetime,
    ) -> bool:
        if not self._client:
            raise RuntimeError("TelethonPublisher: не подключен. Вызовите connect().")

        await self._client.send_file(
            channel,
            str(file_path),
            caption=caption,
            schedule=schedule_time,
        )
        return True

    async def send_photo(
        self,
        channel: str,
        file_path: Path,
        caption: str,
    ) -> bool:
        if not self._client:
            raise RuntimeError("TelethonPublisher: не подключен. Вызовите connect().")

        await self._client.send_file(
            channel,
            str(file_path),
            caption=caption,
        )
        return True

    @property
    def mode(self) -> str:
        return "user"
