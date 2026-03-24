# MAGI

Информационная система публикации и автоматизации управления контентом Telegram-каналов.

Автоматизация сбора, обработки, классификации, планирования и публикации контента.

---

## Архитектура

Система построена на **микросервисной архитектуре** с **API Gateway** в центре:

```
┌─────────────────────────────────────────────────┐
│         AdminPanel (WPF / MVVM)                 │
│       .NET 8 / C# / XAML / HttpClient           │
└──────────────────┬──────────────────────────────┘
                   │ HTTP (REST API)
                   ▼
┌─────────────────────────────────────────────────┐
│         API Gateway (ASP.NET Core)              │
│   Оркестрация / Расписание / Каналы / SQLite    │
│           http://localhost:5000                 │
│          Swagger: /swagger                      │
└──┬──────────────┬──────────────┬────────────────┘
   │ HTTP         │ HTTP         │ HTTP
   ▼              ▼              ▼
┌────────┐   ┌──────────┐   ┌──────────────────┐
│ Parser │   │ Tagger   │   │   Publisher      │
│ Service│   │ Service  │   │   Service        │
│ :5001  │   │ :5002    │   │   :5003          │
│ FastAPI│   │ FastAPI  │   │   FastAPI        │
└────────┘   └──────────┘   │                  │
                            │ IPublisher       │
                            │ ├ Telethon (user)│
                            │ └ Bot API (bot)  │
                            └──────────────────┘
```

### Мультиканальная публикация

```
                    ┌─── Channel A (user mode, Telethon) ──► @channel_a
Publisher Service ──┤
                    ├─── Channel B (bot mode, Bot API)  ──► @channel_b
                    │
                    └─── Channel C (user mode, Telethon) ──► @channel_c
```

Каждый канал имеет собственные API-креденшалы, режим публикации и расписание.

### Канал = изолированный контейнер

```
Channel (корневая сущность)
├── Telegram-креденшалы (API ID, API Hash, Bot Token, Session)
├── ArtsRootPath (папка с артами: New-Images/, Check-Images/, Post-Images/)
├── ParserConfig (хэштеги, источники, задержки)
├── TaggerConfig (шаблон, сепаратор, режим)
├── FilenameTags (keyword→tag маппинг для тегирования)
├── Schedule (расписание слотов)
├── PostingRules (правила автопостинга)
└── Images (база изображений)
```

**Принцип работы UI:**
- Канал не выбран -> все окна и данные пустые
- Канал выбран -> загружаются ТОЛЬКО его настройки, расписание, арты
- Все сервисы запускаются с привязкой к конкретному каналу

---

## Стек

| Компонент | Технология |
|---|---|
| API Gateway | C# / ASP.NET Core 8 / REST API |
| ORM / Database | Entity Framework Core 8 / SQLite |
| Панель управления | C# / WPF / .NET 8 / MVVM |
| Микросервисы | Python 3.13 / FastAPI |
| Браузерная автоматизация | Selenium (Chrome) |
| Telegram (user mode) | Telethon |
| Telegram (bot mode) | Bot API (aiohttp) |
| Документация API | Swagger / OpenAPI |

---

## Структура проекта

```
MAGI/
├── ApiGateway/              # API Gateway (ASP.NET Core)
│   ├── Controllers/         # REST-контроллеры (7 шт.)
│   ├── Data/                # EF Core: Entities, DbContext, миграции
│   ├── Models/              # DTO-модели
│   ├── Services/            # Бизнес-логика, оркестрация, ProcessManager
│   └── Program.cs           # Точка входа, DI-конфигурация
│
├── Parser/                  # Parser Service (FastAPI, порт 5001)
│   ├── PinterestParser.py   # Парсинг Pinterest (v4.0 — Gateway API)
│   ├── PixivParser.py       # Парсинг Pixiv (v2.0 — Gateway API)
│   └── service.py           # FastAPI HTTP-сервер
│
├── FilenameTagger/          # Tagger Service (FastAPI, порт 5002)
│   ├── FilenameTagger.py    # Тегирование по имени файла (v2.0 — Gateway API)
│   └── service.py           # FastAPI HTTP-сервер
│
├── Auto-post/               # Publisher Service (FastAPI, порт 5003)
│   ├── Auto-post.py         # Мультиканальная публикация
│   ├── service.py           # FastAPI HTTP-сервер
│   └── publishers/          # Абстракция публикации (Strategy pattern)
│       ├── base.py           # IPublisher — абстрактный интерфейс
│       ├── telethon_publisher.py  # User-mode (Telethon)
│       ├── bot_publisher.py       # Bot-mode (Bot API)
│       └── factory.py        # Фабрика создания publisher по режиму
│
├── AdmPanel/WpfApp1/        # GUI-панель управления (WPF)
│   ├── MainWindow.xaml(.cs)         # Главное окно (микросервисы, галерея, расписание)
│   ├── ChannelManagementWindow      # Управление каналами
│   ├── ParserSettingsWindow         # Настройки парсера (per-channel)
│   ├── TaggerSettingsWindow         # Настройки теггера + filename-теги
│   ├── AutopostSettingsWindow       # Настройки публикации (Telegram credentials)
│   └── Services/
│       └── GatewayApiClient.cs      # HTTP-клиент к API Gateway
│
├── config/                  # Общие Python-утилиты
│   ├── gateway_client.py    # HTTP-клиент для Python-сервисов → Gateway
│   └── config_loader.py     # Legacy: JSON конфиг (fallback для Auto-post)
│
├── data/                    # Хранилище данных
│   ├── magi.db              # SQLite база данных (основное хранилище)
│   └── json/                # Legacy JSON-файлы (не используются в runtime)
└── docs/                    # Документация по модулям
```

---

## Паттерны проектирования

| Паттерн | Где используется |
|---|---|
| **API Gateway** | ASP.NET Core — единая точка входа для всех сервисов |
| **Strategy** | IPublisher → TelethonPublisher / BotApiPublisher |
| **Factory** | PublisherFactory — создание publisher по режиму канала |
| **Repository** | DataService — абстракция над EF Core |
| **Facade** | GatewayApiClient — упрощённый HTTP-интерфейс |

---

## Поток данных (Pipeline)

```
[Parser Service]  ──скачивает──►  New-Images/
         │
         ▼
[Tagger Service]  ──тегирует──►  SQLite (через Gateway)  ──перемещает──►  Check-Images/
         │
         ▼
[Publisher Service]  ──публикует──►  Telegram (N каналов)  ──архивирует──►  Post-Images/
```

Взаимодействие сервисов:
- AdminPanel → API Gateway → Python-сервисы (**HTTP REST**)
- Python-сервисы → API Gateway → SQLite (**данные через HTTP, не файлы**)
- Каждый сервис предоставляет эндпоинты: `/health`, `/status`, `/run`, `/stop`
- API Gateway оркестрирует выполнение, хранит данные и агрегирует статусы
- Gateway автоматически запускает Python-процессы при обращении (ProcessManager)
- Publisher создаёт IPublisher для каждого канала (Telethon или Bot API)

### Хранилище данных

| Данные | Хранилище | Описание |
|---|---|---|
| Images, Posted Images | **SQLite** | Метаданные изображений (привязка к каналу) |
| Schedule Slots | **SQLite** | Расписание публикаций (per-channel) |
| Download Records | **SQLite** | История скачиваний |
| Posting Rules | **SQLite** | Правила публикации (per-channel) |
| Channels, Networks | **SQLite** | Каналы с API-креденшалами, сети каналов |
| Parser Config | **SQLite** | Per-channel: хэштеги, источники, задержки |
| Tagger Config | **SQLite** | Per-channel: шаблон, сепаратор, режим |
| Filename Tags | **SQLite** | Per-channel: keyword → tag маппинг |
| User Settings | **JSON** | ⚠️ Legacy fallback для Auto-post (не используется при наличии Gateway) |

> **Статус миграции JSON → SQLite:** Parser, Tagger — завершено. Auto-post — частично (JSON fallback при недоступности Gateway).

---

## Структура базы данных (SQLite)

```
┌──────────────────────┐     ┌───────────────────────────┐
│   ChannelNetworks    │     │        Channels            │
│ ─────────────────    │     │ ──────────────────────     │
│ Id (PK)              │◄────│ NetworkId (FK)             │
│ Name                 │     │ Id (PK), Name, Link        │
└──────────────────────┘     │ PublishMode (user/bot)     │
                             │ ApiId, ApiHash, BotToken   │
                             │ SessionFile, IsActive      │
                             │ TimeZone, DelayBetweenPosts│
                             │ ArtsRootPath               │
                             └──────┬──────────────────────┘
                                    │ 1:1 / 1:N
           ┌────────────────────────┼────────────────────────┐
           ▼                        ▼                        ▼
┌──────────────────────┐ ┌──────────────────────┐ ┌──────────────────┐
│ ChannelParserConfigs │ │ ChannelTaggerConfigs │ │  FilenameTags    │
│ ChannelId (unique)   │ │ ChannelId (unique)   │ │  ChannelId (FK)  │
│ Hashtags (JSON)      │ │ RenameTemplate       │ │  Keyword         │
│ NegativeHashtags     │ │ Separator            │ │  Tag             │
│ ImagesPerHashtag     │ │ OnlyNew, Mode        │ │  (1:N per channel│
│ ScrollDelayMs        │ └──────────────────────┘ └──────────────────┘
│ ImageLoadDelayMs     │
│ Sources              │  + Images, PostedImages, PostingRules,
└──────────────────────┘    ScheduleSlots, DownloadRecords (все с ChannelId)
```

**10 таблиц:** Channels, ChannelNetworks, ChannelParserConfigs, ChannelTaggerConfigs, FilenameTags, Images, PostedImages, ScheduleSlots, PostingRules, DownloadRecords.

---

## Быстрый старт

### 1. API Gateway

```bash
cd ApiGateway
dotnet run
# → http://localhost:5000
# → Swagger: http://localhost:5000/swagger
```

При первом запуске:
- Создаётся SQLite БД (`data/magi.db`)
- Автоматически мигрируются данные из JSON (one-time)
- Создаются недостающие таблицы и колонки

### 2. Python-сервисы (запускаются автоматически)

Python-сервисы **автоматически запускаются** API Gateway через ProcessManager при первом обращении. Ручной запуск не требуется.

При необходимости ручного запуска:
```bash
# Установка зависимостей (один раз)
pip install -r Parser/requirements.txt
pip install -r FilenameTagger/requirements.txt
pip install -r Auto-post/requirements.txt

# Запуск сервисов (каждый в отдельном терминале)
cd Parser && python service.py          # → http://localhost:5001
cd FilenameTagger && python service.py  # → http://localhost:5002
cd Auto-post && python service.py       # → http://localhost:5003
```

### 3. Admin Panel

```bash
cd AdmPanel/WpfApp1
dotnet build
# exe: bin/Debug/net8.0-windows/MAGIAdmin.exe
```

---

## API эндпоинты

### Управление процессами

| Метод | URL | Описание |
|---|---|---|
| GET | `/api/process/status` | Статус всех Python-процессов |
| POST | `/api/process/{service}/start` | Запустить Python-процесс |
| POST | `/api/process/{service}/stop` | Остановить Python-процесс |

### Управление сервисами

| Метод | URL | Описание |
|---|---|---|
| GET | `/health` | Health-check API Gateway |
| GET | `/api/parser/status` | Статус Parser |
| POST | `/api/parser/run` | Запуск парсинга |
| POST | `/api/parser/stop` | Остановка парсинга |
| GET | `/api/tagger/status` | Статус Tagger |
| POST | `/api/tagger/run` | Запуск тегирования |
| GET | `/api/publisher/status` | Статус Publisher |
| POST | `/api/publisher/run` | Запуск публикации |
| GET | `/api/publisher/stats` | Статистика публикаций |

### Каналы

| Метод | URL | Описание |
|---|---|---|
| GET | `/api/channel` | Все каналы |
| GET | `/api/channel/{id}` | Канал по ID |
| POST | `/api/channel` | Создать канал |
| PUT | `/api/channel/{id}` | Обновить канал |
| DELETE | `/api/channel/{id}` | Удалить канал (каскадно) |
| GET | `/api/channel/networks` | Все сети каналов |

### Per-channel конфигурации

| Метод | URL | Описание |
|---|---|---|
| GET | `/api/channel/{id}/parser-config` | Конфиг парсера |
| PUT | `/api/channel/{id}/parser-config` | Обновить конфиг парсера |
| GET | `/api/channel/{id}/tagger-config` | Конфиг теггера |
| PUT | `/api/channel/{id}/tagger-config` | Обновить конфиг теггера |
| GET | `/api/channel/{id}/filename-tags` | Теги для тегирования файлов |
| PUT | `/api/channel/{id}/filename-tags` | Заменить теги для канала |

### Расписание

| Метод | URL | Описание |
|---|---|---|
| GET | `/api/schedule?channelId=` | Все слоты расписания |
| POST | `/api/schedule` | Создать слот |
| PUT | `/api/schedule/{key}` | Обновить слот |
| DELETE | `/api/schedule/{key}` | Удалить слот |

### Data API (для Python-сервисов)

| Метод | URL | Описание |
|---|---|---|
| GET | `/api/data/images` | Изображения (?channelId=, ?unpostedOnly=) |
| POST | `/api/data/images` | Добавить изображение |
| POST | `/api/data/images/{name}/posted` | Пометить опубликованным |
| DELETE | `/api/data/images/{name}` | Удалить изображение |
| GET | `/api/data/schedule/pending` | Pending-слоты (?channelId=) |
| PATCH | `/api/data/schedule/{key}/status` | Обновить статус слота |
| GET | `/api/data/channels/active` | Активные каналы |
| GET | `/api/data/downloads/check` | Проверить URL на дубликат |
| POST | `/api/data/downloads` | Добавить запись скачивания |
| GET | `/api/data/rules` | Правила публикации (?channelId=) |
| PUT | `/api/data/rules?channelId=` | Заменить правила для канала |

### Примеры запросов

**Запуск парсера:**
```bash
POST /api/parser/run
{"channelId": "b80d4957", "sources": ["pinterest", "pixiv"]}
```

**Запуск теггера:**
```bash
POST /api/tagger/run
# Gateway автоматически подставляет channel_config (arts_root_path, channel_id)
```

**Запуск публикации:**
```bash
POST /api/publisher/run
# Gateway автоматически подставляет channel_config (credentials, paths)
```

Python-сервисы получают `channelId` и загружают per-channel конфиг из Gateway автоматически.

Полная документация: **Swagger UI** → `http://localhost:5000/swagger`

---

## Документация

| | |
|---|---|
| [API Gateway](docs/README-ApiGateway.md) | REST API, контроллеры, оркестрация |
| [Admin Panel](docs/README-AdminPanel.md) | Вкладки, окна настроек, сборка |
| [Parser](docs/README-Parser.md) | Pinterest и Pixiv, негативные теги, конфиг |
| [FilenameTagger](docs/README-FilenameTagger.md) | Маппинг тегов, алгоритм тегирования |
| [Auto-post](docs/README-Autopost.md) | Расписание, IPublisher, мультиканальная публикация |
| [Testing](docs/README-Testing.md) | Python Unit-тесты, Integration-тесты, Scenario; C# xUnit-тесты |
| [DataBase](docs/README-DB.md) | Архитектура данных |

