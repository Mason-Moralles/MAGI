# Parser — Микросервис парсинга

Скачивает арты из Pinterest и Pixiv по заданным хэштегам.

---

## Файлы

| Файл | Описание |
|---|---|
| `Parser/service.py` | FastAPI HTTP-сервер (порт 5001) |
| `Parser/PinterestParser.py` | Парсер Pinterest v3.0 |
| `Parser/PixivParser.py` | Парсер Pixiv v1.0 |
| `Parser/requirements.txt` | Зависимости Python |

---

## Запуск

### Как HTTP-сервис (рекомендуется)

```bash
cd Parser
pip install -r requirements.txt
python service.py
# → http://localhost:5001
```

API-эндпоинты:

| Метод | URL | Описание |
|---|---|---|
| GET | `/health` | Health-check сервиса |
| GET | `/status` | Статус текущей задачи |
| POST | `/run` | Запуск парсинга (body: `{"sources": ["pinterest", "pixiv"]}`) |
| POST | `/stop` | Остановка парсинга |

### Через AdminPanel

Запускается из AdminPanel (вкладка **Микросервисы**, кнопка **START** у блока **Parser**).
Можно выбрать источник: ✅ Pinterest / ✅ Pixiv (или оба).

### Через API Gateway

```bash
POST http://localhost:5000/api/parser/run
Content-Type: application/json
{"sources": ["pinterest"]}
```

---

## Что читает

| Файл | Что берёт |
|---|---|
| `data/json/Parser/config.json` | `hashtags`, `negativeHashtags`, `imagesPerHashtag`, `downloadPath`, `scrollDelayMs`, `imageLoadDelayMs` |
| `data/json/Parser/Pinterest_downloaded_images.json` | Список уже скачанных URL (чтобы не скачивать повторно) |
| `data/json/Parser/Pixiv_downloaded_images.json` | Список уже скачанных URL (чтобы не скачивать повторно) |

## Что пишет

| Файл | Что записывает |
|---|---|
| `data/json/Parser/Pinterest_downloaded_images.json` | Новые записи скачанных пинов: `pinUrl`, `imageUrl`, `fileName`, `hashtag`, `downloadedAt` |
| `data/json/Parser/Pixiv_downloaded_images.json` | Новые записи: `artworkUrl`, `imageUrl`, `fileName`, `hashtag`, `downloadedAt`. Пропущенные арты (негативный тег) — `fileName: "_skipped_"` |
| `downloadPath\*.jpg` | Скачанные изображения (по умолчанию `D:\MAGI-Images\New-Images\`) |

---

## Логика работы

### Общий алгоритм (оба парсера)
1. Загрузка `config.json`
2. Загрузка БД уже скачанных URL
3. Для каждого хэштега из `hashtags`:
   - Открыть страницу поиска в браузере (Selenium Chrome)
   - Прокрутить до нужного количества артов
   - Для каждого арта/пина:
     - Проверить по БД — если уже скачан, пропустить
     - Открыть страницу арта в новой вкладке
     - **[Только Pixiv]** Проверить теги на совпадение с `negativeHashtags`
     - Если негативный тег найден → вписать в БД с `fileName: "_skipped_"`, пропустить скачивание
     - Иначе → скачать изображение, добавить запись в БД
4. Сохранить БД

### Особенности Pixiv
- Требует авторизацию (Selenium открывает браузер с профилем пользователя)
- Заголовок `Referer: https://www.pixiv.net/` обязателен для скачивания изображений
- CSS-селекторы тегов: `figcaption ul li span a` → `footer ul li a` → `span.gtm-new-work-tag-event-click`
- При закрытии вкладки вручную (негативный тег / нет изображения) устанавливается флаг `tab_closed=True`, чтобы блок `finally` не закрыл вкладку повторно

### Особенности Pinterest
- Негативные хэштеги **не применяются** (только Pixiv)
- Простое скачивание через `requests.get()`
- URL нормализуется: `/564x/`, `/736x/` → `/originals/`

---

## Настройка через AdminPanel

Вкладка **Микросервисы** → кнопка ⚙ рядом с **Parser** → окно `ParserSettingsWindow`:

| Поле в окне | Ключ в config.json | Описание |
|---|---|---|
| Путь загрузки | `downloadPath` | Куда скачивать (обычно `New-Images\`) |
| Хэштеги (каждый на новой строке) | `hashtags` | Поисковые запросы |
| Негативные хэштеги (Pixiv) | `negativeHashtags` | Арты с этими тегами пропускаются |
| Изображений на хэштег | `imagesPerHashtag` | Максимум скачиваний за сессию |
| Задержка прокрутки (мс) | `scrollDelayMs` | Пауза между прокрутками |
| Задержка загрузки (мс) | `imageLoadDelayMs` | Пауза между скачиваниями |

---

## config.json — полная схема

```json
{
  "hashtags": ["綾波レイ", "asuka langley"],
  "negativeHashtags": ["AI-generated"],
  "imagesPerHashtag": 2,
  "downloadPath": "D:\\MAGI-Images\\New-Images",
  "databasePath": "D:\\MAGI\\data\\json\\parser\\Pinterest_downloaded_images.json",
  "scrollDelayMs": 2000,
  "imageLoadDelayMs": 1000
}
```

---

## Pinterest_downloaded_images.json / Pixiv_downloaded_images.json — схема записи

```json
[
  {
    "pinUrl": "https://www.pinterest.com/pin/12345/",
    "imageUrl": "https://i.pinimg.com/.../image.jpg",
    "fileName": "rei_ayanami_0001.jpg",
    "hashtag": "rei ayanami",
    "downloadedAt": "2026-02-14T06:05:37.431230"
  }
]
```

Для пропущенных артов (негативный тег, только Pixiv):
```json
{
  "artworkUrl": "https://www.pixiv.net/en/artworks/140987963",
  "imageUrl": "",
  "fileName": "_skipped_",
  "hashtag": "asuka langley",
  "downloadedAt": "2026-02-15T13:25:20"
}
```
