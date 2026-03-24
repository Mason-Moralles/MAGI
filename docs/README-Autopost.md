# Auto-post — Микросервис публикации

Автоматически планирует публикацию артов в Telegram-каналы по расписанию.
Поддерживает мультиканальную публикацию с двумя режимами: user-mode (Telethon) и bot-mode (Bot API).

---

## Файлы

| Файл | Описание |
|---|---|
| `Auto-post/service.py` | FastAPI HTTP-сервер (порт 5003) |
| `Auto-post/Auto-post.py` | Основной скрипт публикации (v3.0) |
| `Auto-post/publishers/base.py` | `IPublisher` — абстрактный интерфейс публикации |
| `Auto-post/publishers/telethon_publisher.py` | User-mode публикация (Telethon MTProto) |
| `Auto-post/publishers/bot_publisher.py` | Bot-mode публикация (Bot API через aiohttp) |
| `Auto-post/publishers/factory.py` | Фабрика создания publisher по режиму канала |
| `Auto-post/requirements.txt` | Зависимости Python |
| `config/gateway_client.py` | HTTP-клиент для взаимодействия с Gateway |

---

## Запуск

### Как HTTP-сервис (рекомендуется)

```bash
cd Auto-post
pip install -r requirements.txt
python service.py
# → http://localhost:5003
```

API-эндпоинты:

| Метод | URL | Описание |
|---|---|---|
| GET | `/health` | Health-check сервиса |
| GET | `/status` | Статус текущей задачи |
| POST | `/run` | Запуск публикации |
| POST | `/stop` | Остановка |

### Через AdminPanel

Запускается из AdminPanel (вкладка **Микросервисы**, кнопка **START** у блока **Auto-post**).
Режим публикации (user / bot) определяется настройками канала в Gateway.

### Через API Gateway

```bash
POST http://localhost:5000/api/publisher/run
```

---

## Источники данных

Все данные читаются и записываются **через API Gateway** (HTTP REST → SQLite).
Прямого доступа к JSON-файлам или БД нет.

### Что читает (из Gateway)

| Endpoint | Что берёт |
|---|---|
| `GET /api/data/channels/active` | Список активных каналов (креденшалы, пути, TimeZone) |
| `GET /api/data/schedule/pending?channelId=X` | Pending-слоты расписания для канала |
| `GET /api/data/images?channelId=X&unpostedOnly=true` | Неопубликованные арты канала |
| `GET /api/data/rules?channelId=X` | Правила постинга (для генерации слотов) |
| `{ArtsRootPath}/Check-Images/{file}` | Файл арта для отправки (локальная ФС) |

### Что пишет (в Gateway)

| Endpoint | Что записывает |
|---|---|
| `PATCH /api/data/schedule/{isoKey}/status` | Обновляет слот: `status` → `"scheduled"`, `file`, `person`, `caption`, `channelId` |
| `POST /api/data/images/{fileName}/posted` | Перемещает запись: `Images` → `PostedImages` |

### Что перемещает (локальная ФС)

| Откуда | Куда |
|---|---|
| `{ArtsRootPath}/Check-Images/{file}` | `{ArtsRootPath}/Post-Images/{file}` |

---

## Логика работы

1. Получить список **активных каналов** из Gateway (`GET /api/data/channels/active`)
2. Для каждого канала:
   a. Получить **pending-слоты** (`GET /api/data/schedule/pending?channelId=X`)
   b. Получить **неопубликованные арты** (`GET /api/data/images?channelId=X&unpostedOnly=true`)
   c. Получить **правила постинга** (`GET /api/data/rules?channelId=X`)
   d. Прошедшие слоты пометить `"missed"`, не трогать `"scheduled"`/`"posted"` слоты
3. Для каждого `pending`-слота **выбрать арт** (`select_art()`):
   - Приоритет: арт с `forced_tag` (если задан в правиле)
   - Иначе: первый неопубликованный арт, персонаж которого отличается от предыдущего
   - Фолбэк: если все оставшиеся арты одного персонажа — взять любой
4. Подпись берётся из поля `caption` правила, совпавшего с днём и временем слота
5. Создать **IPublisher** для канала через `PublisherFactory`:
   - `publish_mode == "user"` → `TelethonPublisher` (Telethon MTProto)
   - `publish_mode == "bot"` → `BotApiPublisher` (Bot API через aiohttp)
6. Запланировать публикацию через `publisher.send_file(..., schedule=slot_dt)`
7. Обновить данные через Gateway:
   - Слот: `status="scheduled"`, `file`, `person`, `caption` (`PATCH /api/data/schedule/{key}/status`)
   - Изображение: перенести в posted (`POST /api/data/images/{fileName}/posted`)
   - Переместить файл `Check-Images → Post-Images`
8. Ждать `DelayBetweenPosts` (из настроек канала) между постами

---

## Мультиканальная публикация

```
                    ┌─── Channel A (user mode, Telethon) ──► @channel_a
Publisher Service ──┤
                    ├─── Channel B (bot mode, Bot API)  ──► @channel_b
                    │
                    └─── Channel C (user mode, Telethon) ──► @channel_c
```

Каждый канал обрабатывается независимо со своими:
- Telegram-креденшалами (API ID, API Hash, Bot Token, Session File)
- Расписанием слотов (привязаны к каналу через ChannelId)
- Набором артов (привязаны к каналу)
- Правилами постинга (per-channel)
- Часовым поясом (для генерации IsoKey)

---

## IPublisher — Strategy Pattern

```
IPublisher (base.py)
├── TelethonPublisher (telethon_publisher.py) — user-mode через MTProto
└── BotApiPublisher (bot_publisher.py) — bot-mode через HTTP Bot API
```

`PublisherFactory.create_publisher(channel)` — создаёт нужный publisher по `channel.publishMode`.

---

## Значения статуса слота

| Статус | Описание |
|---|---|
| `pending` | Слот создан, арт ещё не привязан |
| `scheduled` | Арт привязан, публикация запланирована в Telegram |
| `posted` | Опубликовано |
| `missed` | Время слота прошло, арт не был привязан |
| `error` | Ошибка при отправке в Telegram |

---

## Настройка через AdminPanel

### Вкладка «Расписание»
Прямое редактирование слотов расписания:
- Добавить / Удалить слот, изменить дату, время, изображение, подпись
- Кнопка **Применить правила** — генерирует `pending`-слоты по правилам постинга
- Кнопка **Сохранить** — записывает данные через Gateway API

Правила постинга:
- Редактируются в нижней панели «Правила постинга»
- Кнопка **+ Добавить время** — создаёт новое правило (время + дни + подпись)
- Кнопка **Сохранить правила**

### Вкладка «Микросервисы» → ⚙ Auto-post → `AutopostSettingsWindow`

| Поле в окне | Поле канала (Gateway) | Описание |
|---|---|---|
| Ссылка на канал | `link` | `@username` или `-100xxxxxxx` |
| API ID | `apiId` | Из my.telegram.org |
| API Hash | `apiHash` | Из my.telegram.org |
| Session file | `sessionFile` | Имя `.session` файла |
| Bot Token | `botToken` | Если режим «Бот» (от @BotFather) |

---

## Зависимости Python

```
fastapi       # HTTP-сервер
uvicorn       # ASGI-сервер
pydantic      # Валидация данных
telethon      # Telegram MTProto клиент (user-mode)
pytz          # Часовые пояса
aiohttp       # HTTP для Bot API
requests      # HTTP-клиент
```
