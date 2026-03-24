# Parser — Микросервис парсинга

Скачивает арты из Pinterest и Pixiv по заданным хэштегам.

---

## Файлы

| Файл | Описание |
|---|---|
| `Parser/service.py` | FastAPI HTTP-сервер (порт 5001) |
| `Parser/PinterestParser.py` | Парсер Pinterest v4.0 (Gateway API) |
| `Parser/PixivParser.py` | Парсер Pixiv v2.0 (Gateway API) |
| `Parser/requirements.txt` | Зависимости Python |
| `config/gateway_client.py` | HTTP-клиент для взаимодействия с Gateway |

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
{"channelId": "b80d4957", "sources": ["pinterest"]}
```

---

## Источники данных

Все данные читаются и записываются **через API Gateway** (HTTP REST → SQLite).

### Что читает (из Gateway)

| Endpoint | Что берёт |
|---|---|
| `GET /api/channel/{id}/parser-config` | Хэштеги, негативные хэштеги, лимиты, задержки, источники |
| `GET /api/data/downloads/check?sourceUrl=URL` | Проверка: был ли URL уже скачан (дедупликация) |
| `GET /api/channel/{id}` | Данные канала (`ArtsRootPath` → путь для скачивания) |

### Что пишет (в Gateway)

| Endpoint | Что записывает |
|---|---|
| `POST /api/data/downloads` | Запись о скачивании: source, sourceUrl, imageUrl, fileName, hashtag, channelId |

### Что пишет (локальная ФС)

| Путь | Описание |
|---|---|
| `{ArtsRootPath}/New-Images/*.jpg` | Скачанные изображения |

---

## Логика работы

### Общий алгоритм (оба парсера)
1. Получить конфиг канала из Gateway (`ChannelParserConfigs`)
2. Для каждого хэштега из конфига:
   - Открыть страницу поиска в браузере (Selenium Chrome)
   - Прокрутить до нужного количества артов
   - Для каждого арта/пина:
     - Проверить через Gateway — если уже скачан, пропустить (`GET /api/data/downloads/check`)
     - Открыть страницу арта в новой вкладке
     - **[Только Pixiv]** Проверить теги на совпадение с `negativeHashtags`
     - Если негативный тег найден → записать в Gateway с `fileName: "_skipped_"`, пропустить скачивание
     - Иначе → скачать изображение, добавить запись через Gateway (`POST /api/data/downloads`)
3. Файлы сохраняются в `{ArtsRootPath}/New-Images/`

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

| Поле в окне | Поле конфига (Gateway) | Описание |
|---|---|---|
| Хэштеги (каждый на новой строке) | `hashtags` | Поисковые запросы (JSON-массив) |
| Негативные хэштеги (Pixiv) | `negativeHashtags` | Арты с этими тегами пропускаются |
| Изображений на хэштег | `imagesPerHashtag` | Максимум скачиваний за сессию |
| Задержка прокрутки (мс) | `scrollDelayMs` | Пауза между прокрутками |
| Задержка загрузки (мс) | `imageLoadDelayMs` | Пауза между скачиваниями |
| Источники | `sources` | `pinterest`, `pixiv` (через запятую) |

Путь загрузки вычисляется автоматически: `Channel.ArtsRootPath + "/New-Images"`.

---

## Зависимости Python

```
fastapi       # HTTP-сервер
uvicorn       # ASGI-сервер
pydantic      # Валидация данных
selenium      # Браузерная автоматизация (Chrome)
requests      # HTTP-клиент для скачивания изображений
```
