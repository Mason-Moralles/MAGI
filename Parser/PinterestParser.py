"""
Pinterest Арт Парсер v3.0 (Python)
Скачивает изображения по хэштегам из Pinterest через Selenium.
"""

import json
import os
import re
import sys
import time
import urllib.parse
from datetime import datetime
from pathlib import Path

import requests
from selenium import webdriver
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.chrome.service import Service
from selenium.webdriver.common.by import By
from selenium.common.exceptions import NoSuchElementException, WebDriverException


# ════════════════════════════════════════
#  Пути
# ════════════════════════════════════════

def get_project_root() -> Path:
    """Определяет корень проекта (папка, содержащая Parser/)."""
    return Path(__file__).resolve().parent.parent


def get_config_path() -> Path:
    return get_project_root() / "data" / "json" / "parser" / "config.json"


def get_database_path() -> Path:
    return get_project_root() / "data" / "json" / "parser" / "Pinterest_downloaded_images.json"


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
        # Fallback для Windows CP1251/CP866 консолей
        safe = msg.encode(sys.stdout.encoding or "utf-8", errors="replace").decode(sys.stdout.encoding or "utf-8", errors="replace")
        print(f"{color}{safe}{Colors.RESET}", flush=True)


# ════════════════════════════════════════
#  Конфигурация
# ════════════════════════════════════════

class Config:
    def __init__(self, data: dict):
        self.hashtags: list[str]   = data.get("hashtags", [])
        self.images_per_hashtag: int = data.get("imagesPerHashtag", 50)
        self.download_path: str    = data.get("downloadPath", "")
        self.scroll_delay_ms: int  = data.get("scrollDelayMs", 2000)
        self.image_load_delay_ms: int = data.get("imageLoadDelayMs", 1000)


def load_config() -> Config:
    log("Загрузка конфигурации...", Colors.CYAN)
    path = get_config_path()

    if not path.exists():
        raise FileNotFoundError(f"Файл конфигурации не найден: {path}")

    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)

    cfg = Config(data)
    log("✓ Конфигурация загружена", Colors.GREEN)
    return cfg


def validate_config(cfg: Config):
    if not cfg.hashtags:
        raise ValueError("Список хэштегов пуст")
    if not cfg.download_path:
        raise ValueError("Путь downloadPath не указан")
    log(f"Хэштегов: {len(cfg.hashtags)} | По {cfg.images_per_hashtag} изображений", Colors.CYAN)


# ════════════════════════════════════════
#  База данных скачанных изображений
# ════════════════════════════════════════

class DownloadDatabase:
    def __init__(self, path: Path):
        self._path = path
        self._records: list[dict] = []
        self._downloaded_urls: set[str] = set()

    @property
    def count(self) -> int:
        return len(self._records)

    def load(self):
        if not self._path.exists():
            return
        try:
            with open(self._path, "r", encoding="utf-8") as f:
                records = json.load(f)
            if isinstance(records, list):
                self._records = records
                for r in records:
                    url = r.get("pinUrl", "")
                    if url:
                        self._downloaded_urls.add(url)
        except (json.JSONDecodeError, IOError):
            pass

    def save(self):
        self._path.parent.mkdir(parents=True, exist_ok=True)
        with open(self._path, "w", encoding="utf-8") as f:
            json.dump(self._records, f, indent=2, ensure_ascii=False)

    def is_downloaded(self, pin_url: str) -> bool:
        return pin_url in self._downloaded_urls

    def add(self, pin_url: str, image_url: str, file_name: str, hashtag: str):
        if pin_url in self._downloaded_urls:
            return
        self._records.append({
            "pinUrl": pin_url,
            "imageUrl": image_url,
            "fileName": file_name,
            "hashtag": hashtag,
            "downloadedAt": datetime.now().isoformat(),
        })
        self._downloaded_urls.add(pin_url)


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

    # Профиль парсера (чтобы сохранять логин)
    profile_dir = os.path.join(
        os.environ.get("LOCALAPPDATA", os.path.expanduser("~")),
        "PinterestParserProfile",
    )
    os.makedirs(profile_dir, exist_ok=True)

    # Удаляем lock-файл если есть
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
        log("  ⚠ Придётся авторизовываться каждый раз", Colors.YELLOW)
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
        # Ищем пины
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

            # Дубликат?
            if db.is_downloaded(pin_url):
                log(f"  ⤷ Пин {pin_id} уже скачан, пропускаю", Colors.GRAY)
                continue

            file_name = download_pin(driver, pin_url, pin_id, hashtag, cfg)
            if file_name:
                # Получаем image_url для БД (из последней вкладки уже закрыта, берём из имени)
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

def main():
    # Принудительно UTF-8 для Windows
    if sys.platform == "win32":
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")

    print("═══════════════════════════════════════")
    print("    Pinterest Арт Парсер v3.0")
    print("═══════════════════════════════════════\n")

    try:
        cfg = load_config()
        validate_config(cfg)

        db = DownloadDatabase(get_database_path())
        db.load()
        log(f"База данных: {get_database_path()}", Colors.CYAN)
        if db.count > 0:
            log(f"  Уже скачано ранее: {db.count} изображений", Colors.GRAY)

        os.makedirs(cfg.download_path, exist_ok=True)

        driver = init_chrome()
        try:
            ensure_pinterest_login(driver)

            session_total = 0
            for hashtag in cfg.hashtags:
                log(f"\n▶ Обработка хэштега: #{hashtag}", Colors.YELLOW)
                session_total += process_hashtag(driver, hashtag, cfg, db)

            db.save()
            log(f"\n✓ Готово! Скачано в этой сессии: {session_total} | Всего в базе: {db.count}", Colors.GREEN)
        finally:
            driver.quit()

    except Exception as exc:
        log(f"\n✗ Ошибка: {exc}", Colors.RED)
        sys.exit(1)


if __name__ == "__main__":
    main()
