"""
Pixiv Арт Парсер v2.0 (Python)
Скачивает изображения по хэштегам из Pixiv через Selenium.
Поддержка блеклиста тегов (напр. #AIGenerated).
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
from selenium.common.exceptions import (
    NoSuchElementException,
    WebDriverException,
    StaleElementReferenceException,
)

# Gateway client
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from config.gateway_client import GatewayClient


# ════════════════════════════════════════
#  Логирование
# ════════════════════════════════════════

class Colors:
    RESET   = "\033[0m"
    RED     = "\033[91m"
    GREEN   = "\033[92m"
    YELLOW  = "\033[93m"
    CYAN    = "\033[96m"
    GRAY    = "\033[90m"
    WHITE   = "\033[97m"
    MAGENTA = "\033[95m"


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
        self.negative_hashtags: list[str] = parser_cfg.get("negativeHashtags", [])
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
    if cfg.negative_hashtags:
        log(f"Негативные теги: {', '.join(cfg.negative_hashtags)}", Colors.YELLOW)


# ════════════════════════════════════════
#  База данных скачанных изображений (Gateway API)
# ════════════════════════════════════════

class DownloadDatabase:
    """Обёртка над Gateway API для проверки и добавления скачанных записей."""

    def __init__(self, gw: GatewayClient, channel_id: str):
        self._gw = gw
        self._channel_id = channel_id
        self._count = 0
        self._session_urls: set[str] = set()

    @property
    def count(self) -> int:
        return self._count

    def load(self):
        """Загружает счётчик из Gateway."""
        self._count = self._gw.get_download_count(source="pixiv")

    def is_downloaded(self, artwork_url: str) -> bool:
        if artwork_url in self._session_urls:
            return True
        return self._gw.is_downloaded(artwork_url)

    def add(self, artwork_url: str, image_url: str, file_name: str, hashtag: str):
        if artwork_url in self._session_urls:
            return
        self._gw.add_download_record(
            source="pixiv",
            source_url=artwork_url,
            image_url=image_url,
            file_name=file_name,
            hashtag=hashtag,
            channel_id=self._channel_id,
        )
        self._session_urls.add(artwork_url)
        self._count += 1


# ════════════════════════════════════════
#  Chrome / Selenium
# ════════════════════════════════════════

def find_chrome_binary() -> str | None:
    candidates = [
        r"C:\Program Files\Google\Chrome\Application\chrome.exe",
        r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
        os.path.join(
            os.environ.get("LOCALAPPDATA", ""),
            r"Google\Chrome\Application\chrome.exe",
        ),
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
        "PixivParserProfile",
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
    opts.add_argument("--remote-debugging-port=9223")

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
#  Pixiv
# ════════════════════════════════════════

def ensure_pixiv_login(driver: webdriver.Chrome):
    """Проверяет авторизацию на Pixiv."""
    log("\nПроверка Pixiv...", Colors.CYAN)
    driver.get("https://www.pixiv.net/en/")
    time.sleep(4)

    url = driver.current_url.lower()
    if "/login" in url or "/accounts/login" in url:
        log("\n═══════════════════════════════════════", Colors.YELLOW)
        log("  ТРЕБУЕТСЯ АВТОРИЗАЦИЯ НА PIXIV", Colors.YELLOW)
        log("═══════════════════════════════════════", Colors.YELLOW)
        log("Войдите в Pixiv в браузере, затем нажмите Enter...\n", Colors.CYAN)
        input()
    else:
        try:
            driver.find_element(By.CSS_SELECTOR, "a[data-gtm-value='header-click-avatar']")
            log("✓ Авторизация подтверждена", Colors.GREEN)
        except NoSuchElementException:
            try:
                driver.find_element(By.CSS_SELECTOR, "button[data-click-label='header-login']")
                log("\n═══════════════════════════════════════", Colors.YELLOW)
                log("  ТРЕБУЕТСЯ АВТОРИЗАЦИЯ НА PIXIV", Colors.YELLOW)
                log("═══════════════════════════════════════", Colors.YELLOW)
                log("Войдите в Pixiv в браузере, затем нажмите Enter...\n", Colors.CYAN)
                input()
            except NoSuchElementException:
                log("✓ Авторизация подтверждена (предположительно)", Colors.GREEN)


def generate_filename(hashtag: str, download_path: str) -> str:
    """Генерирует имя файла с инкрементальным номером."""
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


def check_artwork_tags(driver: webdriver.Chrome, negative_tags: list[str]) -> bool:
    """
    Проверяет теги арта на странице.
    Возвращает True если арт проходит фильтр (нет негативных тегов).
    """
    if not negative_tags:
        return True

    neg_tags_lower = [t.strip().lower().replace("#", "") for t in negative_tags]

    try:
        tag_elements = driver.find_elements(By.CSS_SELECTOR, "figcaption ul li span a")
        if not tag_elements:
            tag_elements = driver.find_elements(By.CSS_SELECTOR, "footer ul li a")
        if not tag_elements:
            tag_elements = driver.find_elements(
                By.CSS_SELECTOR, "span.gtm-new-work-tag-event-click"
            )

        for tag_el in tag_elements:
            try:
                tag_text = tag_el.text.strip().lower().replace("#", "")
                if tag_text in neg_tags_lower:
                    return False
            except StaleElementReferenceException:
                continue

    except Exception as exc:
        log(f"  ⚠ Не удалось проверить теги: {exc}", Colors.YELLOW)

    return True


def get_artwork_image_url(driver: webdriver.Chrome) -> str | None:
    """Извлекает URL оригинального изображения со страницы арта Pixiv."""
    selectors = [
        "div[role='presentation'] a[href*='img-original'] img",
        "figure img[src*='i.pximg.net']",
        "div[role='presentation'] img",
        "main section figure img",
        "canvas + div img",
    ]

    for sel in selectors:
        try:
            imgs = driver.find_elements(By.CSS_SELECTOR, sel)
            for img in imgs:
                src = img.get_attribute("src")
                if src and "i.pximg.net" in src:
                    return src
        except NoSuchElementException:
            continue

    try:
        links = driver.find_elements(By.CSS_SELECTOR, "a[href*='img-original']")
        if links:
            return links[0].get_attribute("href")
    except Exception:
        pass

    try:
        all_imgs = driver.find_elements(By.TAG_NAME, "img")
        for img in all_imgs:
            src = img.get_attribute("src") or ""
            if "i.pximg.net" in src and "/img/" in src:
                if "/user-profile/" in src or "50x50" in src or "48x48" in src:
                    continue
                return src
    except Exception:
        pass

    return None


def convert_to_original_url(url: str) -> str:
    """Преобразует URL превью Pixiv в URL оригинального изображения."""
    if not url:
        return url

    original = url
    if "/img-master/" in original:
        original = original.replace("/img-master/", "/img-original/")
    if "/c/" in original and "/img/" in original:
        original = re.sub(r"/c/[^/]+/", "/", original)

    original = re.sub(r"_master\d+", "", original)
    original = re.sub(r"_square\d+", "", original)

    if original.endswith(".webp"):
        original = original[:-5] + ".jpg"

    return original


def download_pixiv_image(url: str, path: str, referer: str = "https://www.pixiv.net/"):
    """Скачивает изображение с Pixiv (требуется Referer header)."""
    headers = {
        "Referer": referer,
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
                       "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
    }

    original_url = convert_to_original_url(url)

    urls_to_try = [original_url]
    if original_url != url:
        urls_to_try.append(url)
    if original_url.endswith(".jpg"):
        urls_to_try.insert(1, original_url[:-4] + ".png")

    for try_url in urls_to_try:
        try:
            resp = requests.get(try_url, headers=headers, timeout=30)
            if resp.status_code == 200 and len(resp.content) > 1000:
                ct = resp.headers.get("Content-Type", "")
                ext = ".jpg"
                if "png" in ct:
                    ext = ".png"
                elif "webp" in ct:
                    ext = ".webp"
                elif "gif" in ct:
                    ext = ".gif"

                if not path.endswith(ext):
                    path = os.path.splitext(path)[0] + ext

                with open(path, "wb") as f:
                    f.write(resp.content)
                return path
        except Exception:
            continue

    raise Exception(f"Не удалось скачать изображение: {url}")


def process_hashtag(
    driver: webdriver.Chrome,
    hashtag: str,
    cfg: Config,
    db: DownloadDatabase,
) -> int:
    """Обрабатывает один хэштег: ищет, фильтрует, скачивает."""
    encoded = urllib.parse.quote(hashtag)
    search_url = f"https://www.pixiv.net/en/tags/{encoded}/artworks?s_mode=s_tag"

    log(f"Переход: {search_url}", Colors.GRAY)
    driver.get(search_url)
    time.sleep(5)

    downloaded = 0
    processed_artwork_ids: set[str] = set()
    scroll_attempts = 0
    session_for_tag = 0

    while downloaded < cfg.images_per_hashtag and scroll_attempts < 50:
        artwork_links = driver.find_elements(By.CSS_SELECTOR, "a[href*='/artworks/']")

        if not artwork_links:
            log("  ⚠ Ссылки на арты не найдены, пробую прокрутить...", Colors.YELLOW)
            scroll_attempts += 1
            driver.execute_script("window.scrollTo(0, document.body.scrollHeight);")
            time.sleep(cfg.scroll_delay_ms / 1000)
            continue

        found_new = False

        for link_el in artwork_links:
            if downloaded >= cfg.images_per_hashtag:
                break

            try:
                href = link_el.get_attribute("href") or ""
            except StaleElementReferenceException:
                continue

            match = re.search(r"/artworks/(\d+)", href)
            if not match:
                continue

            artwork_id = match.group(1)
            artwork_url = f"https://www.pixiv.net/en/artworks/{artwork_id}"

            if artwork_id in processed_artwork_ids:
                continue

            processed_artwork_ids.add(artwork_id)
            found_new = True

            # Проверяем — уже скачан? (через Gateway API)
            if db.is_downloaded(artwork_url):
                log(f"  ⤷ Арт {artwork_id} уже скачан, пропускаю", Colors.GRAY)
                continue

            # Открываем арт в новой вкладке
            main_window = driver.current_window_handle
            file_name = None
            image_url = ""
            tab_closed = False

            try:
                driver.execute_script("window.open(arguments[0], '_blank');", artwork_url)
                time.sleep(3)
                driver.switch_to.window(driver.window_handles[-1])
                time.sleep(2)

                # Проверяем негативные теги
                if not check_artwork_tags(driver, cfg.negative_hashtags):
                    log(f"  ✗ Арт {artwork_id} содержит негативный тег, пропускаю", Colors.YELLOW)
                    driver.close()
                    tab_closed = True
                    driver.switch_to.window(main_window)
                    db.add(artwork_url, "", "_skipped_", hashtag)
                    time.sleep(0.5)
                    continue

                # Извлекаем URL изображения
                image_url = get_artwork_image_url(driver)
                if not image_url:
                    log(f"  ✗ Не найдено изображение для арта {artwork_id}", Colors.RED)
                    driver.close()
                    tab_closed = True
                    driver.switch_to.window(main_window)
                    time.sleep(0.5)
                    continue

                # Скачиваем
                file_name = generate_filename(hashtag, cfg.download_path)
                file_path = os.path.join(cfg.download_path, file_name)

                actual_path = download_pixiv_image(image_url, file_path, referer=artwork_url)
                file_name = os.path.basename(actual_path)

            except Exception as exc:
                log(f"  ✗ Ошибка при скачивании арта {artwork_id}: {exc}", Colors.RED)
                file_name = None
            finally:
                if not tab_closed:
                    try:
                        driver.close()
                        driver.switch_to.window(main_window)
                    except Exception:
                        try:
                            driver.switch_to.window(driver.window_handles[0])
                        except Exception:
                            pass

            if file_name:
                db.add(artwork_url, image_url, file_name, hashtag)
                downloaded += 1
                session_for_tag += 1
                print(
                    f"\r  [{downloaded}/{cfg.images_per_hashtag}] ✓ {file_name}                    ",
                    end="", flush=True,
                )

            time.sleep(cfg.image_load_delay_ms / 1000)

        if not found_new:
            scroll_attempts += 1
            if scroll_attempts >= 3:
                log(f"\n  ⚠ Больше нет новых артов", Colors.YELLOW)
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
    print("    Pixiv Арт Парсер v2.0")
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
            log(f"  Уже скачано ранее: {db.count} изображений (pixiv)", Colors.GRAY)

        os.makedirs(cfg.download_path, exist_ok=True)

        driver = init_chrome()
        try:
            ensure_pixiv_login(driver)

            session_total = 0
            for hashtag in cfg.hashtags:
                log(f"\n▶ Обработка хэштега: #{hashtag}", Colors.YELLOW)
                session_total += process_hashtag(driver, hashtag, cfg, db)

            log(
                f"\n✓ Готово! Скачано в этой сессии: {session_total} | Всего в базе: {db.count}",
                Colors.GREEN,
            )
        finally:
            driver.quit()

    except Exception as exc:
        log(f"\n✗ Ошибка: {exc}", Colors.RED)
        raise


if __name__ == "__main__":
    _ch_id = sys.argv[1] if len(sys.argv) > 1 else None
    main(channel_id=_ch_id)
