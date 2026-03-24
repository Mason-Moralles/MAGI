"""
Общие фикстуры для Python-тестов MAGI.
"""

import os
import sys
import pytest
import tempfile
import shutil
from pathlib import Path
from unittest.mock import MagicMock, patch

# Добавляем корень проекта в sys.path
ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
if ROOT_DIR not in sys.path:
    sys.path.insert(0, ROOT_DIR)


@pytest.fixture
def mock_gateway():
    """Мок GatewayClient для unit-тестов."""
    gw = MagicMock()
    gw.is_gateway_available.return_value = True
    gw.get_images.return_value = []
    gw.get_active_channels.return_value = []
    gw.get_pending_slots.return_value = []
    gw.get_filename_tags.return_value = []
    gw.get_channel.return_value = None
    gw.add_image.return_value = {}
    gw.mark_image_posted.return_value = True
    gw.remove_image.return_value = True
    gw.update_slot_status.return_value = True
    gw.is_downloaded.return_value = False
    gw.add_download_record.return_value = True
    gw.get_download_count.return_value = 0
    gw.get_posting_rules.return_value = []
    gw.get_parser_config.return_value = None
    return gw


@pytest.fixture
def temp_arts_dir():
    """Временная структура папок артов для тестов."""
    tmpdir = tempfile.mkdtemp(prefix="magi_test_")
    new_images = os.path.join(tmpdir, "New-Images")
    check_images = os.path.join(tmpdir, "Check-Images")
    post_images = os.path.join(tmpdir, "Post-Images")
    os.makedirs(new_images)
    os.makedirs(check_images)
    os.makedirs(post_images)

    yield {
        "root": tmpdir,
        "new_images": new_images,
        "check_images": check_images,
        "post_images": post_images,
    }

    shutil.rmtree(tmpdir, ignore_errors=True)


@pytest.fixture
def sample_images_data():
    """Пример данных изображений для тестов select_art."""
    return {
        "asuka_art_001.jpg": {"fileName": "asuka_art_001.jpg", "person": "#Asuka", "posted": 0},
        "rei_art_002.jpg": {"fileName": "rei_art_002.jpg", "person": "#Rei", "posted": 0},
        "misato_art_003.jpg": {"fileName": "misato_art_003.jpg", "person": "#Misato", "posted": 0},
        "asuka_art_004.jpg": {"fileName": "asuka_art_004.jpg", "person": "#Asuka", "posted": 0},
        "posted_art_005.jpg": {"fileName": "posted_art_005.jpg", "person": "#Asuka", "posted": 1},
    }
