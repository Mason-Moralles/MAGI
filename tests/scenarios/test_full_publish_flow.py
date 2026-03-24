"""
Сценарный тест: Полный цикл публикации.

Сценарий:
  1. Создать канал
  2. Добавить filename-теги
  3. Добавить изображения в БД
  4. Создать слоты расписания (pending)
  5. Проверить что pending-слоты доступны
  6. Обновить статус слота → scheduled
  7. Пометить изображение как posted
  8. Проверить что изображение в posted_images
  9. Удалить канал (каскадное удаление)

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
class TestFullPublishFlow:
    """Полный E2E-сценарий публикации."""

    def test_complete_publish_scenario(self):
        session = requests.Session()

        # ─── 1. Создаём канал ───
        resp = session.post(f"{GATEWAY_URL}/api/channel", json={
            "name": "E2E Publish Test",
            "link": "@e2e_publish_test",
            "publishMode": "bot",
            "timeZone": "Europe/Moscow",
            "artsRootPath": "",
        })
        assert resp.status_code in (200, 201)
        channel = resp.json()["data"]
        ch_id = channel["id"]

        try:
            # ─── 2. Добавляем filename-теги ───
            resp = session.put(
                f"{GATEWAY_URL}/api/channel/{ch_id}/filename-tags",
                json={"tags": [
                    {"keyword": "asuka", "tag": "#Asuka_Langley"},
                    {"keyword": "rei", "tag": "#Rei_Ayanami"},
                ]}
            )
            assert resp.status_code == 200
            assert len(resp.json()["data"]) == 2

            # ─── 3. Добавляем изображения ───
            images = [
                {"fileName": "e2e_asuka_001.jpg", "person": "#Asuka_Langley", "posted": 0, "channelId": ch_id},
                {"fileName": "e2e_rei_002.jpg", "person": "#Rei_Ayanami", "posted": 0, "channelId": ch_id},
            ]
            for img in images:
                resp = session.post(f"{GATEWAY_URL}/api/data/images", json=img)
                assert resp.status_code == 200

            # Проверяем что изображения добавлены
            resp = session.get(f"{GATEWAY_URL}/api/data/images", params={"channelId": ch_id})
            assert resp.status_code == 200
            assert len(resp.json()["data"]) >= 2

            # ─── 4. Создаём слоты расписания ───
            slot1_resp = session.post(f"{GATEWAY_URL}/api/schedule", json={
                "date": "2026-12-25",
                "time": "12:00",
                "caption": "Merry Christmas",
                "channelId": ch_id,
            })
            assert slot1_resp.status_code in (200, 201)
            slot1_key = slot1_resp.json()["data"]["isoKey"]

            slot2_resp = session.post(f"{GATEWAY_URL}/api/schedule", json={
                "date": "2026-12-25",
                "time": "18:00",
                "caption": "Evening post",
                "channelId": ch_id,
            })
            assert slot2_resp.status_code in (200, 201)
            slot2_key = slot2_resp.json()["data"]["isoKey"]

            # ─── 5. Проверяем pending-слоты ───
            resp = session.get(
                f"{GATEWAY_URL}/api/data/schedule/pending",
                params={"channelId": ch_id}
            )
            assert resp.status_code == 200
            pending = resp.json()["data"]
            pending_keys = [s["isoKey"] for s in pending]
            assert slot1_key in pending_keys

            # ─── 6. Обновляем статус слота → scheduled ───
            from urllib.parse import quote
            resp = session.patch(
                f"{GATEWAY_URL}/api/data/schedule/{quote(slot1_key, safe='')}/status",
                json={
                    "status": "scheduled",
                    "file": "e2e_asuka_001.jpg",
                    "person": "#Asuka_Langley",
                    "caption": "Merry Christmas",
                }
            )
            assert resp.status_code == 200

            # ─── 7. Помечаем изображение как posted ───
            resp = session.post(
                f"{GATEWAY_URL}/api/data/images/e2e_asuka_001.jpg/posted",
                json={
                    "person": "#Asuka_Langley",
                    "postedAt": "2026-12-25T12:00:00+03:00",
                    "caption": "Merry Christmas",
                    "channelId": ch_id,
                }
            )
            assert resp.status_code == 200

            # ─── 8. Проверяем что изображение в posted ───
            resp = session.get(f"{GATEWAY_URL}/api/schedule/posted")
            assert resp.status_code == 200
            posted = resp.json()["data"]
            posted_names = [p["fileName"] for p in posted]
            assert "e2e_asuka_001.jpg" in posted_names

            # Изображение должно быть удалено из images
            resp = session.get(f"{GATEWAY_URL}/api/data/images/e2e_asuka_001.jpg")
            assert resp.status_code == 404 or resp.json().get("data") is None

        finally:
            # ─── 9. Удаляем канал (каскад) ───
            resp = session.delete(f"{GATEWAY_URL}/api/channel/{ch_id}")
            assert resp.status_code == 200

            # Проверяем каскадное удаление
            resp = session.get(f"{GATEWAY_URL}/api/channel/{ch_id}")
            assert resp.status_code == 404 or resp.json().get("data") is None
