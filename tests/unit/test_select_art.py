"""
Unit-тесты для функции select_art из Auto-post.py.

Тестируемая логика:
  - Выбор арта по forced_tag
  - Избегание повторного персонажа (last_person)
  - Fallback на общий пул
  - Пустой пул
"""

import os
import sys
import pytest

ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
sys.path.insert(0, ROOT_DIR)
sys.path.insert(0, os.path.join(ROOT_DIR, "Auto-post"))

import pytz

# Импортируем select_art напрямую
from importlib import import_module
auto_post = import_module("Auto-post")
select_art = auto_post.select_art

TZ = pytz.timezone("Europe/Moscow")


class TestSelectArtForcedTag:
    """Тесты выбора арта по forced_tag."""

    def test_forced_tag_returns_matching_art(self, sample_images_data):
        result = select_art(TZ, sample_images_data, forced_tag="#Asuka", last_person=None)
        assert result is not None
        assert sample_images_data[result]["person"] == "#Asuka"

    def test_forced_tag_skips_posted(self, sample_images_data):
        # Помечаем все Asuka как posted
        sample_images_data["asuka_art_001.jpg"]["posted"] = 1
        sample_images_data["asuka_art_004.jpg"]["posted"] = 1
        # forced_tag=#Asuka, но все Asuka posted → fallback в общий пул
        result = select_art(TZ, sample_images_data, forced_tag="#Asuka", last_person=None)
        assert result is not None
        assert sample_images_data[result]["posted"] == 0

    def test_forced_tag_nonexistent_falls_back(self, sample_images_data):
        result = select_art(TZ, sample_images_data, forced_tag="#Shinji", last_person=None)
        assert result is not None
        assert sample_images_data[result]["posted"] == 0


class TestSelectArtLastPerson:
    """Тесты избегания повторного персонажа."""

    def test_avoids_last_person(self, sample_images_data):
        result = select_art(TZ, sample_images_data, forced_tag=None, last_person="#Asuka")
        assert result is not None
        assert sample_images_data[result]["person"] != "#Asuka"

    def test_falls_back_to_last_person_if_only_option(self):
        images = {
            "only_asuka.jpg": {"fileName": "only_asuka.jpg", "person": "#Asuka", "posted": 0},
        }
        result = select_art(TZ, images, forced_tag=None, last_person="#Asuka")
        assert result == "only_asuka.jpg"

    def test_no_last_person_returns_first_available(self, sample_images_data):
        result = select_art(TZ, sample_images_data, forced_tag=None, last_person=None)
        assert result is not None
        assert sample_images_data[result]["posted"] == 0


class TestSelectArtEmptyPool:
    """Тесты с пустым или полностью использованным пулом."""

    def test_empty_pool_returns_none(self):
        result = select_art(TZ, {}, forced_tag=None, last_person=None)
        assert result is None

    def test_all_posted_returns_none(self):
        images = {
            "art1.jpg": {"posted": 1, "person": "#A"},
            "art2.jpg": {"posted": 1, "person": "#B"},
        }
        result = select_art(TZ, images, forced_tag=None, last_person=None)
        assert result is None

    def test_mixed_posted_returns_unposted(self, sample_images_data):
        # Помечаем всё кроме одного как posted
        for k in sample_images_data:
            sample_images_data[k]["posted"] = 1
        sample_images_data["rei_art_002.jpg"]["posted"] = 0

        result = select_art(TZ, sample_images_data, forced_tag=None, last_person=None)
        assert result == "rei_art_002.jpg"
