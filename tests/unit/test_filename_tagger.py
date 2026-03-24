"""
Unit-тесты для FilenameTagger.

Тестируемая логика:
  - Матчинг ключевых слов в именах файлов
  - Перемещение файлов New-Images → Check-Images
  - Пропуск уже обработанных файлов
  - Возврат 0 когда нет тегов или Gateway недоступен
"""

import os
import sys
import pytest
from pathlib import Path
from unittest.mock import patch, MagicMock

ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
sys.path.insert(0, ROOT_DIR)

from FilenameTagger.FilenameTagger import main as tagger_main


class TestTaggerNoGateway:
    """Тесты когда Gateway недоступен."""

    def test_returns_zero_when_gateway_unavailable(self, temp_arts_dir):
        with patch("FilenameTagger.FilenameTagger._get_gateway_client", return_value=None):
            result = tagger_main(channel_id="ch1", arts_root_path=temp_arts_dir["root"])
        assert result == 0


class TestTaggerNoTags:
    """Тесты когда нет тегов для канала."""

    def test_returns_zero_when_no_tags(self, mock_gateway, temp_arts_dir):
        mock_gateway.get_filename_tags.return_value = []
        with patch("FilenameTagger.FilenameTagger._get_gateway_client", return_value=mock_gateway):
            result = tagger_main(channel_id="ch1", arts_root_path=temp_arts_dir["root"])
        assert result == 0


class TestTaggerMatching:
    """Тесты матчинга ключевых слов."""

    def test_matches_keyword_in_filename(self, mock_gateway, temp_arts_dir):
        # Создаём тестовый файл
        test_file = Path(temp_arts_dir["new_images"]) / "asuka_art_001.jpg"
        test_file.write_bytes(b"fake_image_data")

        mock_gateway.get_filename_tags.return_value = [
            {"keyword": "asuka", "tag": "#Asuka_Langley"}
        ]
        mock_gateway.get_images.return_value = []

        with patch("FilenameTagger.FilenameTagger._get_gateway_client", return_value=mock_gateway):
            result = tagger_main(channel_id="ch1", arts_root_path=temp_arts_dir["root"])

        assert result == 1
        mock_gateway.add_image.assert_called_once_with(
            "asuka_art_001.jpg", "#Asuka_Langley", channel_id="ch1"
        )
        # Файл должен быть перемещён в Check-Images
        assert not test_file.exists()
        assert (Path(temp_arts_dir["check_images"]) / "asuka_art_001.jpg").exists()

    def test_no_match_skips_file(self, mock_gateway, temp_arts_dir):
        test_file = Path(temp_arts_dir["new_images"]) / "unknown_art.jpg"
        test_file.write_bytes(b"fake_image_data")

        mock_gateway.get_filename_tags.return_value = [
            {"keyword": "asuka", "tag": "#Asuka"}
        ]
        mock_gateway.get_images.return_value = []

        with patch("FilenameTagger.FilenameTagger._get_gateway_client", return_value=mock_gateway):
            result = tagger_main(channel_id="ch1", arts_root_path=temp_arts_dir["root"])

        assert result == 0
        # Файл остаётся на месте
        assert test_file.exists()

    def test_skips_already_processed_files(self, mock_gateway, temp_arts_dir):
        test_file = Path(temp_arts_dir["new_images"]) / "asuka_art_001.jpg"
        test_file.write_bytes(b"fake_image_data")

        mock_gateway.get_filename_tags.return_value = [
            {"keyword": "asuka", "tag": "#Asuka"}
        ]
        # Файл уже есть в БД
        mock_gateway.get_images.return_value = [
            {"fileName": "asuka_art_001.jpg", "person": "#Asuka", "posted": 0}
        ]

        with patch("FilenameTagger.FilenameTagger._get_gateway_client", return_value=mock_gateway):
            result = tagger_main(channel_id="ch1", arts_root_path=temp_arts_dir["root"])

        assert result == 0
        mock_gateway.add_image.assert_not_called()

    def test_processes_multiple_files(self, mock_gateway, temp_arts_dir):
        for name in ["asuka_001.jpg", "rei_002.png", "misato_003.jpeg"]:
            (Path(temp_arts_dir["new_images"]) / name).write_bytes(b"data")

        mock_gateway.get_filename_tags.return_value = [
            {"keyword": "asuka", "tag": "#Asuka"},
            {"keyword": "rei", "tag": "#Rei"},
            {"keyword": "misato", "tag": "#Misato"},
        ]
        mock_gateway.get_images.return_value = []

        with patch("FilenameTagger.FilenameTagger._get_gateway_client", return_value=mock_gateway):
            result = tagger_main(channel_id="ch1", arts_root_path=temp_arts_dir["root"])

        assert result == 3

    def test_skips_non_image_files(self, mock_gateway, temp_arts_dir):
        (Path(temp_arts_dir["new_images"]) / "asuka_doc.txt").write_bytes(b"data")
        (Path(temp_arts_dir["new_images"]) / "asuka_art.jpg").write_bytes(b"data")

        mock_gateway.get_filename_tags.return_value = [
            {"keyword": "asuka", "tag": "#Asuka"}
        ]
        mock_gateway.get_images.return_value = []

        with patch("FilenameTagger.FilenameTagger._get_gateway_client", return_value=mock_gateway):
            result = tagger_main(channel_id="ch1", arts_root_path=temp_arts_dir["root"])

        assert result == 1

    def test_case_insensitive_matching(self, mock_gateway, temp_arts_dir):
        (Path(temp_arts_dir["new_images"]) / "ASUKA_ART.jpg").write_bytes(b"data")

        mock_gateway.get_filename_tags.return_value = [
            {"keyword": "asuka", "tag": "#Asuka"}
        ]
        mock_gateway.get_images.return_value = []

        with patch("FilenameTagger.FilenameTagger._get_gateway_client", return_value=mock_gateway):
            result = tagger_main(channel_id="ch1", arts_root_path=temp_arts_dir["root"])

        assert result == 1


class TestTaggerArtsRootFromChannel:
    """Тесты получения artsRootPath из канала."""

    def test_gets_arts_root_from_channel(self, mock_gateway, temp_arts_dir):
        mock_gateway.get_channel.return_value = {"artsRootPath": temp_arts_dir["root"]}
        mock_gateway.get_filename_tags.return_value = []  # Нет тегов → 0

        with patch("FilenameTagger.FilenameTagger._get_gateway_client", return_value=mock_gateway):
            result = tagger_main(channel_id="ch1", arts_root_path=None)

        assert result == 0
        mock_gateway.get_channel.assert_called_once_with("ch1")

    def test_returns_zero_when_no_arts_root(self, mock_gateway):
        mock_gateway.get_channel.return_value = {"artsRootPath": ""}

        with patch("FilenameTagger.FilenameTagger._get_gateway_client", return_value=mock_gateway):
            result = tagger_main(channel_id="ch1", arts_root_path=None)

        assert result == 0
