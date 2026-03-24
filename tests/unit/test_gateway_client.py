"""
Unit-тесты для GatewayClient.

Тестируемая логика:
  - Формирование HTTP-запросов (URL, параметры, payload)
  - Обработка ответов (парсинг JSON, коды ошибок)
  - Метод is_gateway_available (обработка исключений)
"""

import os
import sys
import pytest
from unittest.mock import patch, MagicMock

ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
sys.path.insert(0, ROOT_DIR)

from config.gateway_client import GatewayClient


@pytest.fixture
def client():
    """GatewayClient с мок-сессией."""
    with patch("config.gateway_client.requests.Session") as MockSession:
        mock_session = MagicMock()
        MockSession.return_value = mock_session
        gw = GatewayClient("http://test-gateway:5000")
        yield gw, mock_session


class TestHealthCheck:
    """Тесты проверки доступности Gateway."""

    def test_available_when_200(self, client):
        gw, session = client
        session.get.return_value = MagicMock(status_code=200)
        assert gw.is_gateway_available() is True

    def test_unavailable_when_500(self, client):
        gw, session = client
        session.get.return_value = MagicMock(status_code=500)
        assert gw.is_gateway_available() is False

    def test_unavailable_on_connection_error(self, client):
        gw, session = client
        session.get.side_effect = ConnectionError("Connection refused")
        assert gw.is_gateway_available() is False


class TestGetImages:
    """Тесты получения изображений."""

    def test_get_all_images(self, client):
        gw, session = client
        session.get.return_value = MagicMock(
            status_code=200,
            json=lambda: {"data": [{"fileName": "art1.jpg"}]}
        )
        session.get.return_value.raise_for_status = MagicMock()

        result = gw.get_images()
        assert len(result) == 1
        assert result[0]["fileName"] == "art1.jpg"
        session.get.assert_called_with(
            "http://test-gateway:5000/api/data/images", params={}
        )

    def test_get_unposted_images(self, client):
        gw, session = client
        session.get.return_value = MagicMock(
            status_code=200,
            json=lambda: {"data": []}
        )
        session.get.return_value.raise_for_status = MagicMock()

        gw.get_images(unposted_only=True)
        session.get.assert_called_with(
            "http://test-gateway:5000/api/data/images",
            params={"unpostedOnly": "true"}
        )

    def test_get_images_by_channel(self, client):
        gw, session = client
        session.get.return_value = MagicMock(
            status_code=200,
            json=lambda: {"data": []}
        )
        session.get.return_value.raise_for_status = MagicMock()

        gw.get_images(channel_id="ch1")
        session.get.assert_called_with(
            "http://test-gateway:5000/api/data/images",
            params={"channelId": "ch1"}
        )


class TestAddImage:
    """Тесты добавления изображений."""

    def test_add_image_sends_correct_payload(self, client):
        gw, session = client
        session.post.return_value = MagicMock(
            status_code=200,
            json=lambda: {"data": {"fileName": "art.jpg"}}
        )
        session.post.return_value.raise_for_status = MagicMock()

        gw.add_image("art.jpg", "#Asuka", caption="test", channel_id="ch1")
        session.post.assert_called_once_with(
            "http://test-gateway:5000/api/data/images",
            json={
                "fileName": "art.jpg",
                "person": "#Asuka",
                "posted": 0,
                "caption": "test",
                "channelId": "ch1",
            }
        )


class TestMarkImagePosted:
    """Тесты пометки изображения как опубликованного."""

    def test_mark_posted_returns_true_on_200(self, client):
        gw, session = client
        session.post.return_value = MagicMock(status_code=200)

        result = gw.mark_image_posted("art.jpg", person="#Asuka", posted_at="2026-01-01")
        assert result is True

    def test_mark_posted_returns_false_on_404(self, client):
        gw, session = client
        session.post.return_value = MagicMock(status_code=404)

        result = gw.mark_image_posted("nonexistent.jpg")
        assert result is False


class TestDownloadRecords:
    """Тесты работы с записями о скачивании."""

    def test_is_downloaded_true(self, client):
        gw, session = client
        session.get.return_value = MagicMock(
            status_code=200,
            json=lambda: {"data": True}
        )
        session.get.return_value.raise_for_status = MagicMock()

        assert gw.is_downloaded("https://pinterest.com/pin/123") is True

    def test_is_downloaded_false(self, client):
        gw, session = client
        session.get.return_value = MagicMock(
            status_code=200,
            json=lambda: {"data": False}
        )
        session.get.return_value.raise_for_status = MagicMock()

        assert gw.is_downloaded("https://pinterest.com/pin/456") is False

    def test_add_download_record(self, client):
        gw, session = client
        session.post.return_value = MagicMock(status_code=200)

        result = gw.add_download_record(
            source="pinterest",
            source_url="https://pinterest.com/pin/123",
            image_url="https://img.com/1.jpg",
            file_name="art.jpg",
            hashtag="#asuka",
            channel_id="ch1"
        )
        assert result is True


class TestSchedule:
    """Тесты работы с расписанием."""

    def test_get_pending_slots(self, client):
        gw, session = client
        session.get.return_value = MagicMock(
            status_code=200,
            json=lambda: {"data": [{"isoKey": "2026-01-01T12:00:00+03:00", "status": "pending"}]}
        )
        session.get.return_value.raise_for_status = MagicMock()

        result = gw.get_pending_slots(channel_id="ch1")
        assert len(result) == 1
        assert result[0]["status"] == "pending"

    def test_update_slot_status(self, client):
        gw, session = client
        session.patch.return_value = MagicMock(status_code=200)

        result = gw.update_slot_status(
            "2026-01-01T12:00:00+03:00", "scheduled",
            file="art.jpg", person="#Asuka"
        )
        assert result is True


class TestChannels:
    """Тесты работы с каналами."""

    def test_get_active_channels(self, client):
        gw, session = client
        session.get.return_value = MagicMock(
            status_code=200,
            json=lambda: {"data": [{"id": "ch1", "name": "Test Channel"}]}
        )
        session.get.return_value.raise_for_status = MagicMock()

        result = gw.get_active_channels()
        assert len(result) == 1
        assert result[0]["id"] == "ch1"

    def test_get_channel_returns_none_on_404(self, client):
        gw, session = client
        session.get.return_value = MagicMock(status_code=404)

        result = gw.get_channel("nonexistent")
        assert result is None


class TestBaseUrl:
    """Тесты нормализации base_url."""

    def test_strips_trailing_slash(self):
        with patch("config.gateway_client.requests.Session"):
            gw = GatewayClient("http://localhost:5000/")
        assert gw.base_url == "http://localhost:5000"
