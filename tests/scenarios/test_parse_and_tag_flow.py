"""
Сценарный тест: Создание канала → Настройка парсера и теггера.

Сценарий:
  1. Создать канал
  2. Настроить parser-config (хэштеги, источники)
  3. Настроить tagger-config (шаблон, режим)
  4. Добавить filename-теги
  5. Проверить добавление download-записи (имитация работы парсера)
  6. Проверить добавление изображения (имитация работы теггера)
  7. Проверить что всё связано с каналом
  8. Удалить канал

Требует запущенный API Gateway.
"""

import os
import sys
import pytest
import requests

ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
sys.path.insert(0, ROOT_DIR)

GATEWAY_URL = os.environ.get("MAGI_GATEWAY_URL", "http://localhost:5000")


def gateway_available():
    try:
        return requests.get(f"{GATEWAY_URL}/health", timeout=3).status_code == 200
    except Exception:
        return False


skip_no_gateway = pytest.mark.skipif(
    not gateway_available(),
    reason="API Gateway недоступен"
)


@skip_no_gateway
class TestParseAndTagFlow:
    """E2E-сценарий парсинга и тегирования."""

    def test_parse_tag_scenario(self):
        session = requests.Session()

        # ─── 1. Создаём канал ───
        resp = session.post(f"{GATEWAY_URL}/api/channel", json={
            "name": "Parse&Tag Test",
            "link": "@parse_tag_test",
            "publishMode": "user",
            "timeZone": "Asia/Tokyo",
        })
        assert resp.status_code in (200, 201)
        ch_id = resp.json()["data"]["id"]

        try:
            # ─── 2. Настраиваем parser-config ───
            resp = session.put(
                f"{GATEWAY_URL}/api/channel/{ch_id}/parser-config",
                json={
                    "hashtags": ["#evangelion", "#neon_genesis"],
                    "negativeHashtags": ["#nsfw"],
                    "imagesPerHashtag": 25,
                    "sources": "pinterest,pixiv",
                }
            )
            assert resp.status_code == 200
            cfg = resp.json()["data"]
            assert cfg["imagesPerHashtag"] == 25
            assert "pinterest,pixiv" == cfg["sources"]

            # ─── 3. Настраиваем tagger-config ───
            resp = session.put(
                f"{GATEWAY_URL}/api/channel/{ch_id}/tagger-config",
                json={
                    "renameTemplate": "{id}_{artist}",
                    "separator": "-",
                    "mode": "copy",
                    "onlyNew": False,
                }
            )
            assert resp.status_code == 200
            cfg = resp.json()["data"]
            assert cfg["mode"] == "copy"
            assert cfg["onlyNew"] is False

            # ─── 4. Добавляем filename-теги ───
            resp = session.put(
                f"{GATEWAY_URL}/api/channel/{ch_id}/filename-tags",
                json={"tags": [
                    {"keyword": "evangelion", "tag": "#Evangelion"},
                    {"keyword": "asuka", "tag": "#Asuka_Langley"},
                    {"keyword": "rei", "tag": "#Rei_Ayanami"},
                ]}
            )
            assert resp.status_code == 200
            assert len(resp.json()["data"]) == 3

            # ─── 5. Имитация парсера: добавляем download-записи ───
            downloads = [
                {"source": "pinterest", "sourceUrl": f"https://pinterest.com/pin/e2e_{i}",
                 "imageUrl": f"https://img.com/{i}.jpg", "fileName": f"e2e_art_{i}.jpg",
                 "hashtag": "#evangelion", "channelId": ch_id}
                for i in range(1, 4)
            ]
            for dl in downloads:
                resp = session.post(f"{GATEWAY_URL}/api/data/downloads", json=dl)
                assert resp.status_code == 200

            # Проверяем дубликат
            resp = session.get(
                f"{GATEWAY_URL}/api/data/downloads/check",
                params={"sourceUrl": "https://pinterest.com/pin/e2e_1"}
            )
            assert resp.json()["data"] is True

            # ─── 6. Имитация теггера: добавляем изображения ───
            for i in range(1, 4):
                resp = session.post(f"{GATEWAY_URL}/api/data/images", json={
                    "fileName": f"e2e_art_{i}.jpg",
                    "person": "#Evangelion",
                    "posted": 0,
                    "channelId": ch_id,
                })
                assert resp.status_code == 200

            # ─── 7. Проверяем привязку к каналу ───
            resp = session.get(
                f"{GATEWAY_URL}/api/data/images",
                params={"channelId": ch_id}
            )
            assert resp.status_code == 200
            images = resp.json()["data"]
            assert len(images) >= 3
            for img in images:
                assert img["channelId"] == ch_id

            # Проверяем count
            resp = session.get(
                f"{GATEWAY_URL}/api/data/downloads/count",
                params={"source": "pinterest"}
            )
            assert resp.status_code == 200
            assert resp.json()["data"]["count"] >= 3

        finally:
            # ─── 8. Удаляем канал ───
            resp = session.delete(f"{GATEWAY_URL}/api/channel/{ch_id}")
            assert resp.status_code == 200


@skip_no_gateway
class TestProcessManagement:
    """Тест управления процессами (если ProcessController доступен)."""

    def test_process_status(self):
        resp = requests.get(f"{GATEWAY_URL}/api/process/status")
        # Может быть 200 или 404 если ProcessController не зарегистрирован
        if resp.status_code == 200:
            data = resp.json()
            assert isinstance(data, (dict, list))
