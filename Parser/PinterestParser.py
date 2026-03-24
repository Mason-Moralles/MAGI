"""
Pinterest Арт Парсер v4.0 (Python)
Скачивает изображения по хэштегам из Pinterest через Selenium.
Конфигурация и база скачиваний — через API Gateway (SQLite).
"""

import os
import re
import sys
import time
import urllib.parse
from pathlib import Path

import requests
from selenium import webdriver
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.common.by import By
from selenium.common.exceptions import NoSuchElementException, WebDriverException

# Gateway client
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from config.gateway_client import GatewayClient


# ════════════════════════════════════════
#  Логирование
# ════════════════════════════════════════

class Colors:
    RESET  = "\033[0m"
    RED    = "\033[91m"
    GREEN  = "\033[92m"
    YELLOW = "\033[93m"
    CYAN   = "\033[96m"
    GRAY   = "\033[90m"
    WHITE  = "\033[97m"


def log(msg: str, color: str = Colors.WHITE):
    try:
        print(f"{color}{msg}{Colors.RESET}", flush=True)
    except UnicodeEncodeError:
        safe = msg.encode(sys.stdout.encoding or "utf-8", errors="replace").decode(
            sys.stdout.encoding or "utf-8", errors="replace"
        )
        print(f"{color}{safe}{Colors.RESET}", flush=True)


# ════════════════════════════════════════
#  Конфигурация (из Gateway)
# ════════════════════════════════════════

class Config:
    def __init__(self, channel_id: str, gw: GatewayClient):
        self.channel_id = channel_id

        # Получаем данные канала
        channel = gw.get_channel(channel_id)
        if not channel:
            raise ValueError(f"Канал {channel_id} не найден в Gateway")

        # Получаем конфиг парсера
        parser_cfg = gw.get_parser_config(channel_id)
        if not parser_cfg:
            raise ValueError(f"Конфиг парсера для канала {channel_id} не найден")

        self.hashtags: list[str] = parser_cfg.get("hashtags", [])
        self.images_per_hashtag: int = parser_cfg.get("imagesPerHashtag", 50)
        self.scroll_delay_ms: int = parser_cfg.get("scrollDelayMs", 2000)
        self.image_load_delay_ms: int = parser_cfg.get("imageLoadDelayMs", 1000)

        # Путь для скачивания = ArtsRootPath / New-Images
        arts_root = channel.get("artsRootPath", "")
        if not arts_root:
            raise ValueError(f"ArtsRootPath не задан для канала {channel_id}")
        self.download_path: str = os.path.join(arts_root, "New-Images")


def load_config(channel_id: str, gw: GatewayClient) -> Config:
    log("Загрузка конфигурации из Gateway...", Colors.CYAN)
    cfg = Config(channel_id, gw)
    log("✓ Конфигурация загружена из Gateway", Colors.GREEN)
    return cfg


def validate_config(cfg: Config):
    if not cfg.hashtags:
        raise ValueError("Список хэштегов пуст")
    if not cfg.download_path:
        raise ValueError("Путь downloadPath не указан")
    log(f"Хэштегов: {len(cfg.hashtags)} | По {cfg.images_per_hashtag} изображений", Colors.CYAN)


# ════════════════════════════════════════
#  База данных скачанных изображений (Gateway API)
# ════════════════════════════════════════

class DownloadDatabase:
    """Обёртка над Gateway API для проверки и добавления скачанных записей."""

    def __init__(self, gw: GatewayClient, channel_id: str):
        self._gw = gw
        self._channel_id = channel_id
        self._count = 0
        # Локальный кэш URL-ов текущей сессии (чтобы не дёргать API на каждый пин)
        self._session_urls: set[str] = set()

    @property
    def count(self) -> int:
        return self._count

    def load(self):
        """Загружает счётчик из Gateway."""
        self._count = self._gw.get_download_count(source="pinterest")

    def is_downloaded(self, pin_url: str) -> bool:
        if pin_url in self._session_urls:
            return True
        return self._gw.is_downloaded(pin_url)

    def add(self, pin_url: str, image_url: str, file_name: str, hashtag: str):
        if pin_url in self._session_urls:
            return
        self._gw.add_download_record(
            source="pinterest",
            source_url=pin_url,
            image_url=image_url,
            file_name=file_name,
            hashtag=hashtag,
            channel_id=self._channel_id,
        )
        self._session_urls.add(pin_url)
        self._count += 1


# ════════════════════════════════════════
#  Chrome / Selenium
# ════════════════════════════════════════

def find_chrome_binary() -> str | None:
    candidates = [
        r"C:\Program Files\Google\Chrome\Application\chrome.exe",
        r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
        os.path.join(os.environ.get("LOCALAPPDATA", ""), r"Google\Chrome\Application\chrome.exe"),
    ]
    for p in candidates:
        if os.path.isfile(p):
            return p
    return None


def init_chrome() -> webdriver.Chrome:
    log("\nЗапуск Chrome...", Colors.CYAN)

    opts = Options()

    chrome_bin = find_chrome_binary()
    if chrome_bin:
        opts.binary_location = chrome_bin

    profile_dir = os.path.join(
        os.environ.get("LOCALAPPDATA", os.path.expanduser("~")),
        "PinterestParserProfile",
    )
    os.makedirs(profile_dir, exist_ok=True)

    lock_file = os.path.join(profile_dir, "SingletonLock")
    if os.path.exists(lock_file):
        try:
            os.remove(lock_file)
        except OSError:
            pass

    opts.add_argument(f"user-data-dir={profile_dir}")
    opts.add_argument("--start-maximized")
    opts.add_argument("--no-sandbox")
    opts.add_argument("--disable-dev-shm-usage")
    opts.add_argument("--disable-gpu")
    opts.add_argument("--disable-notifications")
    opts.add_argument("--disable-blink-features=AutomationControlled")
    opts.add_experimental_option("excludeSwitches", ["enable-automation"])
    opts.add_argument("--remote-debugging-port=9222")

    try:
        driver = webdriver.Chrome(options=opts)
        driver.implicitly_wait(10)
        log("✓ Chrome запущен", Colors.GREEN)
        return driver
    except WebDriverException as exc:
        log(f"⚠ Ошибка с профилем: {exc}", Colors.YELLOW)
        log("Пробую запустить без сохранения сессии...", Colors.YELLOW)

        simple_opts = Options()
        if chrome_bin:
            simple_opts.binary_location = chrome_bin
        simple_opts.add_argument("--no-sandbox")
        simple_opts.add_argument("--disable-dev-shm-usage")
        simple_opts.add_argument("--start-maximized")

        driver = webdriver.Chrome(options=simple_opts)
        driver.implicitly_wait(10)
        log("✓ Chrome запущен (без сохранения сессии)", Colors.GREEN)
        return driver


# ════════════════════════════════════════
#  Pinterest
# ════════════════════════════════════════

def ensure_pinterest_login(driver: webdriver.Chrome):
    log("\nПроверка Pinterest...", Colors.CYAN)
    driver.get("https://www.pinterest.com")
    time.sleep(3)

    url = driver.current_url.lower()
    if "/login" in url or "/auth" in url:
        log("\n═══════════════════════════════════════", Colors.YELLOW)
        log("  ТРЕБУЕТСЯ АВТОРИЗАЦИЯ", Colors.YELLOW)
        log("═══════════════════════════════════════", Colors.YELLOW)
        log("Войдите в Pinterest в браузере, затем нажмите Enter...\n", Colors.CYAN)
        input()
    else:
        log("✓ Авторизация подтверждена", Colors.GREEN)


def get_original_url(url: str) -> str:
    for size in ("/564x/", "/736x/", "/474x/", "/1200x/"):
        if size in url:
            return url.replace(size, "/originals/")
    return url


def generate_filename(hashtag: str, download_path: str) -> str:
    safe_tag = hashtag.replace(" ", "_")
    dl = Path(download_path)
    pattern = re.compile(rf"^{re.escape(safe_tag)}_(\d+)\.\w+$", re.IGNORECASE)

    max_idx = 0
    if dl.exists():
        for f in dl.iterdir():
            m = pattern.match(f.name)
            if m:
                max_idx = max(max_idx, int(m.group(1)))

    return f"{safe_tag}_{max_idx + 1:04d}.jpg"


def get_pin_id(pin) -> str | None:
    try:
        pid = pin.get_attribute("data-test-pin-id")
        if pid:
            return pid
    except Exception:
        pass
    try:
        inner = pin.find_element(By.CSS_SELECTOR, "[data-test-pin-id]")
        return inner.get_attribute("data-test-pin-id")
    except Exception:
        return None


def get_pin_url(pin) -> str | None:
    try:
        link = pin.find_element(By.CSS_SELECTOR, "a[href*='/pin/']")
        return link.get_attribute("href")
    except Exception:
        return None


def extract_image_url(driver: webdriver.Chrome) -> str | None:
    selectors = [
        "div[data-test-id='closeup-container'] div[data-test-id='pin-closeup-image'] img",
        "div[data-test-id='pin-closeup-image'] img",
    ]
    for sel in selectors:
        try:
            img = driver.find_element(By.CSS_SELECTOR, sel)
            src = img.get_attribute("src")
            if src:
                return get_original_url(src)
        except NoSuchElementException:
            continue
    return None


def download_image(url: str, path: str):
    resp = requests.get(url, timeout=30)
    resp.raise_for_status()
    with open(path, "wb") as f:
        f.write(resp.content)


def download_pin(
    driver: webdriver.Chrome,
    pin_url: str,
    pin_id: str,
    hashtag: str,
    cfg: Config,
) -> str | None:
    """Открывает пин в новой вкладке, скачивает изображение, возвращает имя файла."""
    main_window = driver.current_window_handle
    try:
        driver.execute_script("window.open(arguments[0], '_blank');", pin_url)
        time.sleep(2.5)
        driver.switch_to.window(driver.window_handles[-1])
        time.sleep(2)

        image_url = extract_image_url(driver)
        if not image_url:
            log(f"\n  ✗ Не найдено изображение для пина {pin_id}", Colors.RED)
            return None

        file_name = generate_filename(hashtag, cfg.download_path)
        file_path = os.path.join(cfg.download_path, file_name)

        download_image(image_url, file_path)
        return file_name

    except Exception as exc:
        log(f"\n  ✗ Ошибка при скачивании пина {pin_id}: {exc}", Colors.RED)
        return None
    finally:
        try:
            driver.close()
            driver.switch_to.window(main_window)
        except Exception:
            try:
                driver.switch_to.window(driver.window_handles[0])
            except Exception:
                pass


def process_hashtag(
    driver: webdriver.Chrome,
    hashtag: str,
    cfg: Config,
    db: DownloadDatabase,
) -> int:
    search_url = f"https://www.pinterest.com/search/pins/?q={urllib.parse.quote(hashtag)}"
    log(f"Переход: {search_url}", Colors.GRAY)

    driver.get(search_url)
    time.sleep(4)

    downloaded = 0
    processed: set[str] = set()
    scroll_attempts = 0
    session_for_tag = 0

    while downloaded < cfg.images_per_hashtag and scroll_attempts < 50:
        pins = driver.find_elements(By.CSS_SELECTOR, "div[data-test-id='pin']")
        if not pins:
            pins = driver.find_elements(By.CSS_SELECTOR, "div[data-test-id='pinWrapper']")

        found_new = False

        for pin in pins:
            if downloaded >= cfg.images_per_hashtag:
                break

            pin_id = get_pin_id(pin)
            if not pin_id or pin_id in processed:
                continue

            processed.add(pin_id)
            found_new = True

            pin_url = get_pin_url(pin)
            if not pin_url:
                continue

            # Дубликат? (проверка через Gateway API)
            if db.is_downloaded(pin_url):
                log(f"  ⤷ Пин {pin_id} уже скачан, пропускаю", Colors.GRAY)
                continue

            file_name = download_pin(driver, pin_url, pin_id, hashtag, cfg)
            if file_name:
                db.add(pin_url, "", file_name, hashtag)
                downloaded += 1
                session_for_tag += 1
                print(f"\r  [{downloaded}/{cfg.images_per_hashtag}] ✓ {file_name}                    ", end="", flush=True)

            time.sleep(cfg.image_load_delay_ms / 1000)

        if not found_new:
            scroll_attempts += 1
            if scroll_attempts >= 3:
                log(f"\n  ⚠ Больше нет новых пинов", Colors.YELLOW)
                break

        driver.execute_script("window.scrollTo(0, document.body.scrollHeight);")
        time.sleep(cfg.scroll_delay_ms / 1000)

    print()
    log(f"✓ Хэштег #{hashtag}: скачано {downloaded} изображений", Colors.GREEN)
    return session_for_tag


# ════════════════════════════════════════
#  Main
# ════════════════════════════════════════

def main(channel_id: str | None = None, gw: GatewayClient | None = None):
    # Принудительно UTF-8 для Windows
    if sys.platform == "win32":
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")

    print("═══════════════════════════════════════")
    print("    Pinterest Арт Парсер v4.0")
    print("    (Gateway API mode)")
    print("═══════════════════════════════════════\n")

    if not channel_id:
        raise ValueError("channel_id обязателен. Передайте через service.py или аргумент.")

    if gw is None:
        gw = GatewayClient()

    try:
        cfg = load_config(channel_id, gw)
        validate_config(cfg)

        db = DownloadDatabase(gw, channel_id)
        db.load()
        log(f"База данных: Gateway API (SQLite)", Colors.CYAN)
        if db.count > 0:
            log(f"  Уже скачано ранее: {db.count} изображений (pinterest)", Colors.GRAY)

        os.makedirs(cfg.download_path, exist_ok=True)

        driver = init_chrome()
        try:
            ensure_pinterest_login(driver)

            session_total = 0
            for hashtag in cfg.hashtags:
                log(f"\n▶ Обработка хэштега: #{hashtag}", Colors.YELLOW)
                session_total += process_hashtag(driver, hashtag, cfg, db)

            log(f"\n✓ Готово! Скачано в этой сессии: {session_total} | Всего в базе: {db.count}", Colors.GREEN)
        finally:
            driver.quit()

    except Exception as exc:
        log(f"\n✗ Ошибка: {exc}", Colors.RED)
        raise


if __name__ == "__main__":
    _ch_id = sys.argv[1] if len(sys.argv) > 1 else None
    main(channel_id=_ch_id)
