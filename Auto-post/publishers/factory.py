"""
PublisherFactory — создание нужного IPublisher по конфигурации канала.
"""

from pathlib import Path
from typing import Optional

from .base import IPublisher
from .telethon_publisher import TelethonPublisher
from .bot_publisher import BotApiPublisher


def create_publisher(
    publish_mode: str,
    api_id: Optional[int] = None,
    api_hash: Optional[str] = None,
    bot_token: Optional[str] = None,
    session_file: Optional[str] = None,
) -> IPublisher:
    """
    Создать экземпляр IPublisher по режиму публикации.

    Args:
        publish_mode: 'user' или 'bot'
        api_id: Telegram API ID (для user-mode)
        api_hash: Telegram API Hash (для user-mode)
        bot_token: Bot Token (для bot-mode, опционально для user-mode)
        session_file: путь к файлу сессии Telethon (для user-mode)

    Returns:
        IPublisher — экземпляр публикатора
    """
    if publish_mode == "bot":
        if not bot_token:
            raise ValueError("Для режима 'bot' необходим bot_token")
        return BotApiPublisher(bot_token=bot_token)

    # user mode (Telethon)
    if not api_id or not api_hash:
        raise ValueError("Для режима 'user' необходимы api_id и api_hash")
    if not session_file:
        raise ValueError("Для режима 'user' необходим session_file")

    return TelethonPublisher(
        api_id=api_id,
        api_hash=api_hash,
        session_file=session_file,
        bot_token=bot_token,
    )
