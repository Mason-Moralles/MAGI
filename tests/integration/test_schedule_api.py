"""
Integration-тесты Schedule & Data API.

Тестируют операции с расписанием, изображениями и записями о скачивании.
Требуют запущенный API Gateway на http://localhost:5000.
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
        r = requests.get(f"{GATEWAY_URL}/health", timeout=3)
        return r.status_code == 200
    except Exception:
        return False


skip_no_gateway = pytest.mark.skipif(
    not gateway_available(),
    reason="API Gateway недоступен"
)


@skip_no_gateway
class TestScheduleAPI:
    """Тесты CRUD расписания."""

    @pytest.fixture
    def channel_id(self):
        resp = requests.post(f"{GATEWAY_URL}/api/channel", json={
            "name": "Schedule Test Channel",
            "link": "@schedule_test",
            "timeZone": "Europe/Moscow",
        })
        ch_id = resp.json()["data"]["id"]
        yield ch_id
        requests.delete(f"{GATEWAY_URL}/api/channel/{ch_id}")

    def test_create_and_get_slot(self, channel_id):
        # CREATE
        resp = requests.post(f"{GATEWAY_URL}/api/schedule", json={
            "date": "2026-12-25",
            "time": "14:30",
            "caption": "Merry Christmas",
            "channelId": channel_id,
        })
        assert resp.status_code == 200
        slot = resp.json()["data"]
        iso_key = slot["isoKey"]
        assert slot["status"] == "pending"
        assert "14:30" in iso_key

        try:
            # GET ALL
            resp = requests.get(f"{GATEWAY_URL}/api/schedule", params={"channelId": channel_id})
            assert resp.status_code == 200
            slots = resp.json()["data"]
            assert any(s["isoKey"] == iso_key for s in slots)

            # GET PENDING
            resp = requests.get(f"{GATEWAY_URL}/api/schedule/pending", params={"channelId": channel_id})
            assert resp.status_code == 200
        finally:
            # DELETE
            resp = requests.post(f"{GATEWAY_URL}/api/schedule/delete", json={"isoKey": iso_key})
            assert resp.status_code == 200

    def test_update_slot(self, channel_id):
        # Create
        resp = requests.post(f"{GATEWAY_URL}/api/schedule", json={
            "date": "2026-12-31",
            "time": "23:59",
            "channelId": channel_id,
        })
        iso_key = resp.json()["data"]["isoKey"]

        try:
            # Update via body
            resp = requests.put(f"{GATEWAY_URL}/api/schedule/update", json={
                "isoKey": iso_key,
                "date": "2026-12-31",
                "time": "23:59",
                "caption": "Happy New Year!",
            })
            assert resp.status_code == 200
        finally:
            requests.post(f"{GATEWAY_URL}/api/schedule/delete", json={"isoKey": iso_key})

    def test_time_normalization(self, channel_id):
        """Время '7:29' должно нормализоваться в '07:29' в isoKey."""
        resp = requests.post(f"{GATEWAY_URL}/api/schedule", json={
            "date": "2026-06-15",
            "time": "7:29",
            "channelId": channel_id,
        })
        assert resp.status_code == 200
        iso_key = resp.json()["data"]["isoKey"]
        assert "T07:29:00" in iso_key

        requests.post(f"{GATEWAY_URL}/api/schedule/delete", json={"isoKey": iso_key})


@skip_no_gateway
class TestImagesAPI:
    """Тесты CRUD изображений."""

    def test_add_get_delete_image(self):
        base = f"{GATEWAY_URL}/api/data/images"
        fname = "test_integration_art_001.jpg"

        # ADD
        resp = requests.post(base, json={
            "fileName": fname,
            "person": "#TestPerson",
            "posted": 0,
            "caption": "Test caption",
        })
        assert resp.status_code == 200

        try:
            # GET
            resp = requests.get(f"{base}/{fname}")
            assert resp.status_code == 200
            assert resp.json()["data"]["person"] == "#TestPerson"

            # GET ALL (filtered)
            resp = requests.get(base, params={"unpostedOnly": "true"})
            assert resp.status_code == 200
            names = [img["fileName"] for img in resp.json()["data"]]
            assert fname in names
        finally:
            # DELETE
            resp = requests.delete(f"{base}/{fname}")
            assert resp.status_code == 200

    def test_mark_image_posted(self):
        base = f"{GATEWAY_URL}/api/data/images"
        fname = "test_post_mark_002.jpg"

        # Add image
        requests.post(base, json={
            "fileName": fname,
            "person": "#Test",
            "posted": 0,
        })

        # Mark posted
        resp = requests.post(f"{base}/{fname}/posted", json={
            "person": "#Test",
            "postedAt": "2026-01-01T12:00:00+03:00",
        })
        assert resp.status_code == 200

        # Should be gone from images
        resp = requests.get(f"{base}/{fname}")
        assert resp.status_code == 404 or resp.json().get("data") is None

        # Should be in posted
        resp = requests.get(f"{GATEWAY_URL}/api/schedule/posted")
        assert resp.status_code == 200


@skip_no_gateway
class TestDownloadsAPI:
    """Тесты записей о скачивании."""

    def test_add_and_check_download(self):
        source_url = "https://test-integration.example.com/unique_test_image_12345"

        # CHECK (should not exist)
        resp = requests.get(
            f"{GATEWAY_URL}/api/data/downloads/check",
            params={"sourceUrl": source_url}
        )
        assert resp.status_code == 200
        assert resp.json()["data"] is False

        # ADD
        resp = requests.post(f"{GATEWAY_URL}/api/data/downloads", json={
            "source": "pinterest",
            "sourceUrl": source_url,
            "imageUrl": "https://img.example.com/1.jpg",
            "fileName": "test_dl.jpg",
            "hashtag": "#test",
        })
        assert resp.status_code == 200

        # CHECK again (should exist now)
        resp = requests.get(
            f"{GATEWAY_URL}/api/data/downloads/check",
            params={"sourceUrl": source_url}
        )
        assert resp.status_code == 200
        assert resp.json()["data"] is True

        # COUNT
        resp = requests.get(f"{GATEWAY_URL}/api/data/downloads/count")
        assert resp.status_code == 200
        assert resp.json()["data"]["count"] >= 1


@skip_no_gateway
class TestPostingRulesAPI:
    """Тесты правил публикации."""

    @pytest.fixture
    def channel_id(self):
        resp = requests.post(f"{GATEWAY_URL}/api/channel", json={
            "name": "Rules Test Channel",
            "link": "@rules_test",
        })
        ch_id = resp.json()["data"]["id"]
        yield ch_id
        requests.delete(f"{GATEWAY_URL}/api/channel/{ch_id}")

    def test_rules_crud(self, channel_id):
        base = f"{GATEWAY_URL}/api/data/rules"

        # ADD rule
        resp = requests.post(base, json={
            "time": "12:00",
            "days": ["Monday", "Wednesday", "Friday"],
            "caption": "",
            "channelId": channel_id,
        })
        assert resp.status_code == 200

        # GET rules
        resp = requests.get(base, params={"channelId": channel_id})
        assert resp.status_code == 200
        rules = resp.json()["data"]
        assert len(rules) >= 1

        # REPLACE rules (batch)
        resp = requests.put(base, json={
            "channelId": channel_id,
            "rules": [
                {"time": "10:00", "days": ["Monday"], "caption": "Morning"},
                {"time": "18:00", "days": ["Friday"], "caption": "Evening"},
            ]
        })
        assert resp.status_code == 200


@skip_no_gateway
class TestHealthEndpoints:
    """Тесты health-эндпоинтов."""

    def test_gateway_health(self):
        resp = requests.get(f"{GATEWAY_URL}/health")
        assert resp.status_code == 200
        assert resp.json()["status"] == "healthy"

    def test_services_health(self):
        resp = requests.get(f"{GATEWAY_URL}/health/services")
        assert resp.status_code == 200
        # Должен вернуть список сервисов (даже если они offline)
        data = resp.json()
        assert isinstance(data, (dict, list))
