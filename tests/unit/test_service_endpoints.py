"""
Unit-тесты для FastAPI-эндпоинтов микросервисов (Parser, Tagger, Publisher).

Используем httpx.AsyncClient + ASGITransport для тестирования
эндпоинтов без реального запуска серверов.
"""

import os
import sys
import importlib.util
import pytest

ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
sys.path.insert(0, ROOT_DIR)
sys.path.insert(0, os.path.join(ROOT_DIR, "Parser"))
sys.path.insert(0, os.path.join(ROOT_DIR, "FilenameTagger"))
sys.path.insert(0, os.path.join(ROOT_DIR, "Auto-post"))

from httpx import AsyncClient, ASGITransport


def _load_module_from_file(name: str, filepath: str):
    """Загружает модуль из файла напрямую (обход проблем с дефисами в именах пакетов)."""
    spec = importlib.util.spec_from_file_location(name, filepath)
    mod = importlib.util.module_from_spec(spec)
    sys.modules[name] = mod
    spec.loader.exec_module(mod)
    return mod


# ─── Parser Service ───

class TestParserServiceEndpoints:
    """Тесты эндпоинтов Parser Service."""

    @pytest.fixture(autouse=True)
    def setup(self):
        svc = _load_module_from_file(
            "parser_service", os.path.join(ROOT_DIR, "Parser", "service.py")
        )
        svc.current_task = svc.TaskResult()
        self.app = svc.app

    @pytest.mark.asyncio
    async def test_health(self):
        transport = ASGITransport(app=self.app)
        async with AsyncClient(transport=transport, base_url="http://test") as client:
            resp = await client.get("/health")
        assert resp.status_code == 200
        data = resp.json()
        assert data["status"] == "healthy"
        assert data["service"] == "parser-service"

    @pytest.mark.asyncio
    async def test_status_idle(self):
        transport = ASGITransport(app=self.app)
        async with AsyncClient(transport=transport, base_url="http://test") as client:
            resp = await client.get("/status")
        assert resp.status_code == 200
        assert resp.json()["status"] == "idle"

    @pytest.mark.asyncio
    async def test_run_requires_channel_id(self):
        transport = ASGITransport(app=self.app)
        async with AsyncClient(transport=transport, base_url="http://test") as client:
            resp = await client.post("/run", json={"sources": ["pinterest"]})
        assert resp.status_code == 200
        assert "error" in resp.json()

    @pytest.mark.asyncio
    async def test_stop(self):
        transport = ASGITransport(app=self.app)
        async with AsyncClient(transport=transport, base_url="http://test") as client:
            resp = await client.post("/stop")
        assert resp.status_code == 200
        assert resp.json()["status"] == "stopped"


# ─── Tagger Service ───

class TestTaggerServiceEndpoints:
    """Тесты эндпоинтов Tagger Service."""

    @pytest.fixture(autouse=True)
    def setup(self):
        svc = _load_module_from_file(
            "tagger_service", os.path.join(ROOT_DIR, "FilenameTagger", "service.py")
        )
        svc.current_task = svc.TaskResult()
        self.app = svc.app

    @pytest.mark.asyncio
    async def test_health(self):
        transport = ASGITransport(app=self.app)
        async with AsyncClient(transport=transport, base_url="http://test") as client:
            resp = await client.get("/health")
        assert resp.status_code == 200
        assert resp.json()["service"] == "tagger-service"

    @pytest.mark.asyncio
    async def test_status_idle(self):
        transport = ASGITransport(app=self.app)
        async with AsyncClient(transport=transport, base_url="http://test") as client:
            resp = await client.get("/status")
        assert resp.status_code == 200
        assert resp.json()["status"] == "idle"

    @pytest.mark.asyncio
    async def test_stop(self):
        transport = ASGITransport(app=self.app)
        async with AsyncClient(transport=transport, base_url="http://test") as client:
            resp = await client.post("/stop")
        assert resp.status_code == 200
        assert resp.json()["status"] == "stopped"


# ─── Publisher Service ───

class TestPublisherServiceEndpoints:
    """Тесты эндпоинтов Publisher Service."""

    @pytest.fixture(autouse=True)
    def setup(self):
        svc = _load_module_from_file(
            "publisher_service", os.path.join(ROOT_DIR, "Auto-post", "service.py")
        )
        svc.current_task = svc.TaskResult()
        self.app = svc.app

    @pytest.mark.asyncio
    async def test_health(self):
        transport = ASGITransport(app=self.app)
        async with AsyncClient(transport=transport, base_url="http://test") as client:
            resp = await client.get("/health")
        assert resp.status_code == 200
        assert resp.json()["service"] == "publisher-service"

    @pytest.mark.asyncio
    async def test_status_idle(self):
        transport = ASGITransport(app=self.app)
        async with AsyncClient(transport=transport, base_url="http://test") as client:
            resp = await client.get("/status")
        assert resp.status_code == 200
        assert resp.json()["status"] == "idle"

    @pytest.mark.asyncio
    async def test_stop(self):
        transport = ASGITransport(app=self.app)
        async with AsyncClient(transport=transport, base_url="http://test") as client:
            resp = await client.post("/stop")
        assert resp.status_code == 200
        assert resp.json()["status"] == "stopped"
