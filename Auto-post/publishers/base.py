"""
IPublisher — абстрактный интерфейс для публикации контента в Telegram-каналы.

Конкретные реализации:
  - TelethonPublisher (user mode) — через Telethon API
  - BotApiPublisher (bot mode) — через aiogram / Bot API
"""

from abc import ABC, abstractmethod
from datetime import datetime
from pathlib import Path
from typing import Optional


class IPublisher(ABC):
    """Абстрактный публикатор для Telegram-канала."""

    @abstractmethod
    async def connect(self) -> None:
        """Подключение к Telegram (авторизация)."""
        ...

    @abstractmethod
    async def disconnect(self) -> None:
        """Отключение от Telegram."""
        ...

    @abstractmethod
    async def send_scheduled_photo(
        self,
        channel: str,
        file_path: Path,
        caption: str,
        schedule_time: datetime,
    ) -> bool:
        """
        Отправить фото с отложенной публикацией.

        Args:
            channel: ID или ссылка на канал (@channel_name или -100...)
            file_path: путь к файлу изображения
            caption: подпись к посту
            schedule_time: время публикации (timezone-aware)

        Returns:
            True если успешно запланировано
        """
        ...

    @abstractmethod
    async def send_photo(
        self,
        channel: str,
        file_path: Path,
        caption: str,
    ) -> bool:
        """
        Отправить фото сейчас (без отложенной публикации).

        Args:
            channel: ID или ссылка на канал
            file_path: путь к файлу изображения
            caption: подпись к посту

        Returns:
            True если успешно отправлено
        """
        ...

    @property
    @abstractmethod
    def mode(self) -> str:
        """Режим публикации: 'user' или 'bot'."""
        ...
