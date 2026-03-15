"""
gateway_client.py — HTTP-клиент для взаимодействия Python-сервисов с API Gateway.

Все операции с данными (images, schedule, downloads) проходят через API Gateway,
который хранит данные в SQLite через Entity Framework Core.

Использование:
    from config.gateway_client import GatewayClient
    gw = GatewayClient()  # по умолчанию http://localhost:5000
    images = gw.get_unposted_images()
"""

import os
import requests
from typing import Optional


GATEWAY_URL = os.environ.get("MAGI_GATEWAY_URL", "http://localhost:5000")


class GatewayClient:
    def __init__(self, base_url: str = GATEWAY_URL):
        self.base_url = base_url.rstrip("/")
        self._session = requests.Session()
        self._session.headers.update({"Content-Type": "application/json"})

    # ═══════════════════════════════════════
    #  Images (Tagger → Gateway, Publisher → Gateway)
    # ═══════════════════════════════════════

    def get_images(self, unposted_only: bool = False) -> list[dict]:
        """Получить изображения из базы."""
        params = {"unpostedOnly": "true"} if unposted_only else {}
        resp = self._session.get(f"{self.base_url}/api/data/images", params=params)
        resp.raise_for_status()
        return resp.json().get("data", [])

    def get_image(self, file_name: str) -> Optional[dict]:
        """Получить конкретное изображение."""
        resp = self._session.get(f"{self.base_url}/api/data/images/{file_name}")
        if resp.status_code == 404:
            return None
        resp.raise_for_status()
        return resp.json().get("data")

    def add_image(self, file_name: str, person: str, caption: str = "") -> dict:
        """Добавить изображение (вызывается Tagger)."""
        payload = {
            "fileName": file_name,
            "person": person,
            "posted": 0,
            "caption": caption,
        }
        resp = self._session.post(f"{self.base_url}/api/data/images", json=payload)
        resp.raise_for_status()
        return resp.json().get("data", {})

    def mark_image_posted(
        self,
        file_name: str,
        person: Optional[str] = None,
        posted_at: Optional[str] = None,
        caption: str = "",
    ) -> bool:
        """Пометить изображение как опубликованное (вызывается Publisher)."""
        payload = {"person": person, "postedAt": posted_at, "caption": caption}
        resp = self._session.post(
            f"{self.base_url}/api/data/images/{file_name}/posted", json=payload
        )
        return resp.status_code == 200

    def remove_image(self, file_name: str) -> bool:
        """Удалить изображение из базы."""
        resp = self._session.delete(f"{self.base_url}/api/data/images/{file_name}")
        return resp.status_code == 200

    # ═══════════════════════════════════════
    #  Schedule (Publisher → Gateway)
    # ═══════════════════════════════════════

    def get_pending_slots(self) -> list[dict]:
        """Получить pending-слоты расписания."""
        resp = self._session.get(f"{self.base_url}/api/data/schedule/pending")
        resp.raise_for_status()
        return resp.json().get("data", [])

    def update_slot_status(
        self,
        iso_key: str,
        status: str,
        file: Optional[str] = None,
        person: Optional[str] = None,
        caption: Optional[str] = None,
    ) -> bool:
        """Обновить статус слота расписания."""
        payload = {"status": status, "file": file, "person": person, "caption": caption}
        resp = self._session.patch(
            f"{self.base_url}/api/data/schedule/{requests.utils.quote(iso_key, safe='')}/status",
            json=payload,
        )
        return resp.status_code == 200

    # ═══════════════════════════════════════
    #  Download Records (Parser → Gateway)
    # ═══════════════════════════════════════

    def is_downloaded(self, source_url: str) -> bool:
        """Проверить, был ли URL уже скачан."""
        resp = self._session.get(
            f"{self.base_url}/api/data/downloads/check",
            params={"sourceUrl": source_url},
        )
        resp.raise_for_status()
        return resp.json().get("data", False)

    def add_download_record(
        self,
        source: str,
        source_url: str,
        image_url: str = "",
        file_name: str = "",
        hashtag: str = "",
    ) -> bool:
        """Добавить запись о скачивании."""
        payload = {
            "source": source,
            "sourceUrl": source_url,
            "imageUrl": image_url,
            "fileName": file_name,
            "hashtag": hashtag,
        }
        resp = self._session.post(f"{self.base_url}/api/data/downloads", json=payload)
        return resp.status_code == 200

    def get_download_count(self, source: Optional[str] = None) -> int:
        """Получить количество скачанных записей."""
        params = {"source": source} if source else {}
        resp = self._session.get(
            f"{self.base_url}/api/data/downloads/count", params=params
        )
        resp.raise_for_status()
        return resp.json().get("data", {}).get("count", 0)

    # ═══════════════════════════════════════
    #  Posting Rules (Publisher → Gateway)
    # ═══════════════════════════════════════

    def get_posting_rules(self) -> list[dict]:
        """Получить правила публикации."""
        resp = self._session.get(f"{self.base_url}/api/data/rules")
        resp.raise_for_status()
        return resp.json().get("data", [])

    # ═══════════════════════════════════════
    #  Health
    # ═══════════════════════════════════════

    def is_gateway_available(self) -> bool:
        """Проверить доступность API Gateway."""
        try:
            resp = self._session.get(f"{self.base_url}/health", timeout=5)
            return resp.status_code == 200
        except Exception:
            return False
