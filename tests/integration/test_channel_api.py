"""
Integration-тесты Channel API.

Тестируют CRUD-операции каналов через реальные HTTP-запросы к API Gateway.
Требуют запущенный API Gateway на http://localhost:5000.

Запуск: pytest tests/integration/ -v --gateway-url=http://localhost:5000
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
class TestChannelCRUD:
    """Полный CRUD-цикл канала."""

    def test_create_get_update_delete_channel(self):
        session = requests.Session()
        base = f"{GATEWAY_URL}/api/channel"

        # CREATE
        create_resp = session.post(base, json={
            "name": "Test Channel",
            "link": "@test_channel_magi",
            "publishMode": "bot",
            "timeZone": "Europe/Moscow",
            "artsRootPath": "",
        })
        assert create_resp.status_code in (200, 201)
        channel = create_resp.json()["data"]
        ch_id = channel["id"]
        assert channel["name"] == "Test Channel"
        assert channel["link"] == "@test_channel_magi"

        try:
            # GET
            get_resp = session.get(f"{base}/{ch_id}")
            assert get_resp.status_code == 200
            assert get_resp.json()["data"]["name"] == "Test Channel"

            # GET ALL
            all_resp = session.get(base)
            assert all_resp.status_code == 200
            ids = [ch["id"] for ch in all_resp.json()["data"]]
            assert ch_id in ids

            # UPDATE
            update_resp = session.put(f"{base}/{ch_id}", json={
                "name": "Updated Channel",
                "delayBetweenPosts": 10,
            })
            assert update_resp.status_code == 200
            assert update_resp.json()["data"]["name"] == "Updated Channel"
            assert update_resp.json()["data"]["delayBetweenPosts"] == 10

            # GET after update
            get_resp2 = session.get(f"{base}/{ch_id}")
            assert get_resp2.json()["data"]["name"] == "Updated Channel"

        finally:
            # DELETE
            del_resp = session.delete(f"{base}/{ch_id}")
            assert del_resp.status_code == 200

        # Verify deleted
        get_resp3 = session.get(f"{base}/{ch_id}")
        assert get_resp3.status_code == 404 or get_resp3.json().get("data") is None

    def test_get_nonexistent_channel_returns_404(self):
        resp = requests.get(f"{GATEWAY_URL}/api/channel/nonexistent_id_12345")
        assert resp.status_code == 404


@skip_no_gateway
class TestChannelConfigs:
    """Тесты per-channel конфигов (parser, tagger, filename tags)."""

    @pytest.fixture
    def channel_id(self):
        """Создаёт канал для теста и удаляет после."""
        resp = requests.post(f"{GATEWAY_URL}/api/channel", json={
            "name": "Config Test Channel",
            "link": "@config_test",
        })
        ch_id = resp.json()["data"]["id"]
        yield ch_id
        requests.delete(f"{GATEWAY_URL}/api/channel/{ch_id}")

    def test_parser_config_crud(self, channel_id):
        base = f"{GATEWAY_URL}/api/channel/{channel_id}/parser-config"

        # GET default config
        resp = requests.get(base)
        assert resp.status_code == 200
        cfg = resp.json()["data"]
        assert cfg["imagesPerHashtag"] == 50

        # UPDATE
        resp = requests.put(base, json={
            "hashtags": ["#evangelion", "#anime"],
            "imagesPerHashtag": 100,
        })
        assert resp.status_code == 200
        assert resp.json()["data"]["imagesPerHashtag"] == 100

    def test_tagger_config_crud(self, channel_id):
        base = f"{GATEWAY_URL}/api/channel/{channel_id}/tagger-config"

        # GET default
        resp = requests.get(base)
        assert resp.status_code == 200
        cfg = resp.json()["data"]
        assert cfg["renameTemplate"] == "{artist}_{title}_{id}"

        # UPDATE
        resp = requests.put(base, json={
            "renameTemplate": "{id}_{artist}",
            "mode": "copy",
        })
        assert resp.status_code == 200
        assert resp.json()["data"]["mode"] == "copy"

    def test_filename_tags_crud(self, channel_id):
        base = f"{GATEWAY_URL}/api/channel/{channel_id}/filename-tags"

        # GET empty
        resp = requests.get(base)
        assert resp.status_code == 200
        assert resp.json()["data"] == []

        # SET tags
        resp = requests.put(base, json={
            "tags": [
                {"keyword": "asuka", "tag": "#Asuka_Langley"},
                {"keyword": "rei", "tag": "#Rei_Ayanami"},
            ]
        })
        assert resp.status_code == 200
        tags = resp.json()["data"]
        assert len(tags) == 2

        # GET after set
        resp = requests.get(base)
        assert len(resp.json()["data"]) == 2

        # REPLACE tags
        resp = requests.put(base, json={
            "tags": [{"keyword": "misato", "tag": "#Misato"}]
        })
        assert resp.status_code == 200
        assert len(resp.json()["data"]) == 1


@skip_no_gateway
class TestNetworks:
    """Тесты сетей каналов."""

    def test_network_crud(self):
        base = f"{GATEWAY_URL}/api/channel/networks"

        # CREATE
        resp = requests.post(base, json={"name": "Test Network"})
        assert resp.status_code == 200
        net = resp.json()["data"]
        net_id = net["id"]
        assert net["name"] == "Test Network"

        try:
            # GET ALL
            resp = requests.get(base)
            assert resp.status_code == 200
            ids = [n["id"] for n in resp.json()["data"]]
            assert net_id in ids
        finally:
            # DELETE
            resp = requests.delete(f"{base}/{net_id}")
            assert resp.status_code == 200
