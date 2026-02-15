"""
Pixiv Арт Парсер v1.0 (Python)
Скачивает изображения по хэштегам из Pixiv через Selenium.
Поддержка блеклиста тегов (напр. #AIGenerated).
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
from selenium.webdriver.common.keys import Keys
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC
from selenium.common.exceptions import (
    NoSuchElementException,
    WebDriverException,
    TimeoutException,
    StaleElementReferenceException,
)


# ════════════════════════════════════════
#  Пути
# ════════════════════════════════════════

def get_project_root() -> Path:
    """Определяет корень проекта (папка, содержащая Parser/)."""
    return Path(__file__).resolve().parent.parent


def get_config_path() -> Path:
    return get_project_root() / "data" / "json" / "parser" / "config.json"


def get_database_path() -> Path:
    return get_project_root() / "data" / "json" / "parser" / "Pixiv_downloaded_images.json"


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
#  Конфигурация
# ════════════════════════════════════════

class Config:
    def __init__(self, data: dict):
        self.hashtags: list[str] = data.get("hashtags", [])
        self.negative_hashtags: list[str] = data.get("negativeHashtags", [])
        self.images_per_hashtag: int = data.get("imagesPerHashtag", 50)
        self.download_path: str = data.get("downloadPath", "")
        self.scroll_delay_ms: int = data.get("scrollDelayMs", 2000)
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
    if cfg.negative_hashtags:
        log(f"Негативные теги: {', '.join(cfg.negative_hashtags)}", Colors.YELLOW)


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
                    url = r.get("artworkUrl", "")
                    if url:
                        self._downloaded_urls.add(url)
        except (json.JSONDecodeError, IOError):
            pass

    def save(self):
        self._path.parent.mkdir(parents=True, exist_ok=True)
        with open(self._path, "w", encoding="utf-8") as f:
            json.dump(self._records, f, indent=2, ensure_ascii=False)

    def is_downloaded(self, artwork_url: str) -> bool:
        return artwork_url in self._downloaded_urls

    def add(self, artwork_url: str, image_url: str, file_name: str, hashtag: str):
        if artwork_url in self._downloaded_urls:
            return
        self._records.append({
            "artworkUrl": artwork_url,
            "imageUrl": image_url,
            "fileName": file_name,
            "hashtag": hashtag,
            "downloadedAt": datetime.now().isoformat(),
        })
        self._downloaded_urls.add(artwork_url)


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

    # Профиль парсера (отдельный для Pixiv, чтобы сохранять логин)
    profile_dir = os.path.join(
        os.environ.get("LOCALAPPDATA", os.path.expanduser("~")),
        "PixivParserProfile",
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
    opts.add_argument("--remote-debugging-port=9223")  # отдельный порт от Pinterest

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
#  Pixiv
# ════════════════════════════════════════

def ensure_pixiv_login(driver: webdriver.Chrome):
    """Проверяет авторизацию на Pixiv."""
    log("\nПроверка Pixiv...", Colors.CYAN)
    driver.get("https://www.pixiv.net/en/")
    time.sleep(4)

    url = driver.current_url.lower()
    # Проверяем есть ли кнопка логина или нас перенаправило на страницу входа
    if "/login" in url or "/accounts/login" in url:
        log("\n═══════════════════════════════════════", Colors.YELLOW)
        log("  ТРЕБУЕТСЯ АВТОРИЗАЦИЯ НА PIXIV", Colors.YELLOW)
        log("═══════════════════════════════════════", Colors.YELLOW)
        log("Войдите в Pixiv в браузере, затем нажмите Enter...\n", Colors.CYAN)
        input()
    else:
        # Дополнительная проверка — ищем элемент аватара залогиненного пользователя
        try:
            driver.find_element(By.CSS_SELECTOR, "a[data-gtm-value='header-click-avatar']")
            log("✓ Авторизация подтверждена", Colors.GREEN)
        except NoSuchElementException:
            # Может быть залогинен, но аватар не найден по этому селектору — проверяем по другим признакам
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
    Возвращает False если найден негативный тег.
    """
    if not negative_tags:
        return True

    # Нормализуем негативные теги для сравнения
    neg_tags_lower = [t.strip().lower().replace("#", "") for t in negative_tags]

    try:
        # Основной селектор по структуре: figcaption > ul > li > span > a
        tag_elements = driver.find_elements(By.CSS_SELECTOR, "figcaption ul li span a")
        if not tag_elements:
            # Запасной: любой li внутри footer с ссылкой
            tag_elements = driver.find_elements(By.CSS_SELECTOR, "footer ul li a")
        if not tag_elements:
            # GTM-класс как последний вариант
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
    """
    Извлекает URL оригинального изображения со страницы арта Pixiv.
    """
    selectors = [
        # Основное изображение на странице арта (кликабельный контейнер)
        "div[role='presentation'] a[href*='img-original'] img",
        # Изображение в просмотре
        "figure img[src*='i.pximg.net']",
        # Основной img на странице арта
        "div[role='presentation'] img",
        # Общий контейнер арта
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

    # Попробуем найти ссылку на оригинал через контейнер
    try:
        links = driver.find_elements(By.CSS_SELECTOR, "a[href*='img-original']")
        if links:
            return links[0].get_attribute("href")
    except Exception:
        pass

    # Попробуем найти любое изображение с pximg.net
    try:
        all_imgs = driver.find_elements(By.TAG_NAME, "img")
        for img in all_imgs:
            src = img.get_attribute("src") or ""
            if "i.pximg.net" in src and "/img/" in src:
                # Пропускаем аватары и мелкие превью
                if "/user-profile/" in src or "50x50" in src or "48x48" in src:
                    continue
                return src
    except Exception:
        pass

    return None


def convert_to_original_url(url: str) -> str:
    """
    Преобразует URL превью Pixiv в URL оригинального изображения.
    Pixiv форматы URL:
    - /img-master/img/... → /img-original/img/...
    - _master1200 → убираем суффикс
    - /c/600x1200_90_webp/ → убираем ограничение
    """
    if not url:
        return url

    # Замена master на original
    original = url
    if "/img-master/" in original:
        original = original.replace("/img-master/", "/img-original/")
    if "/c/" in original and "/img/" in original:
        # Убираем ограничение размера /c/600x1200_90_webp/
        original = re.sub(r"/c/[^/]+/", "/", original)

    # Убираем суффикс _master1200
    original = re.sub(r"_master\d+", "", original)
    # Убираем _square1200
    original = re.sub(r"_square\d+", "", original)

    # Расширение — пробуем jpg, png
    if original.endswith(".webp"):
        original = original[:-5] + ".jpg"

    return original


def download_pixiv_image(url: str, path: str, referer: str = "https://www.pixiv.net/"):
    """
    Скачивает изображение с Pixiv (требуется Referer header).
    """
    headers = {
        "Referer": referer,
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
                       "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
    }

    # Попробуем скачать как оригинал (jpg), потом png, потом как есть
    original_url = convert_to_original_url(url)

    urls_to_try = [original_url]
    if original_url != url:
        urls_to_try.append(url)
    # Попробуем и png вариант
    if original_url.endswith(".jpg"):
        urls_to_try.insert(1, original_url[:-4] + ".png")

    for try_url in urls_to_try:
        try:
            resp = requests.get(try_url, headers=headers, timeout=30)
            if resp.status_code == 200 and len(resp.content) > 1000:
                # Определяем реальное расширение по Content-Type
                ct = resp.headers.get("Content-Type", "")
                ext = ".jpg"
                if "png" in ct:
                    ext = ".png"
                elif "webp" in ct:
                    ext = ".webp"
                elif "gif" in ct:
                    ext = ".gif"

                # Поправляем расширение в пути если нужно
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
    # Pixiv поиск по тегам — URL формат
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
        # Ищем ссылки на арты
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

            # Извлекаем artwork ID
            match = re.search(r"/artworks/(\d+)", href)
            if not match:
                continue

            artwork_id = match.group(1)
            artwork_url = f"https://www.pixiv.net/en/artworks/{artwork_id}"

            if artwork_id in processed_artwork_ids:
                continue

            processed_artwork_ids.add(artwork_id)
            found_new = True

            # Проверяем — уже скачан?
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

        # Прокрутка
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
    print("    Pixiv Арт Парсер v1.0")
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
            ensure_pixiv_login(driver)

            session_total = 0
            for hashtag in cfg.hashtags:
                log(f"\n▶ Обработка хэштега: #{hashtag}", Colors.YELLOW)
                session_total += process_hashtag(driver, hashtag, cfg, db)

            db.save()
            log(
                f"\n✓ Готово! Скачано в этой сессии: {session_total} | Всего в базе: {db.count}",
                Colors.GREEN,
            )
        finally:
            driver.quit()

    except Exception as exc:
        log(f"\n✗ Ошибка: {exc}", Colors.RED)
        sys.exit(1)


if __name__ == "__main__":
    main()
