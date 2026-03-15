# MAGI

Информационная система публикации и автоматизации управления контентом Telegram-каналов.

Автоматизация сбора, обработки, классификации, планирования и публикации контента.

---

## Архитектура

Система построена на **микросервисной архитектуре** с **API Gateway** в центре:

```
┌─────────────────────────────────────────────────┐
│              AdminPanel (WPF)                    │
│             .NET 8 / C# / XAML                   │
└──────────────────┬──────────────────────────────┘
                   │ HTTP (REST API)
                   ▼
┌─────────────────────────────────────────────────┐
│         API Gateway (ASP.NET Core)               │
│   Оркестрация / Расписание / Каналы / SQLite     │
│           http://localhost:5000                   │
│          Swagger: /swagger                        │
└──┬──────────────┬──────────────┬────────────────┘
   │ HTTP         │ HTTP         │ HTTP
   ▼              ▼              ▼
┌────────┐   ┌──────────┐   ┌───────────┐
│ Parser │   │ Tagger   │   │ Publisher │
│ Service│   │ Service  │   │ Service   │
│ :5001  │   │ :5002    │   │ :5003     │
│ FastAPI│   │ FastAPI  │   │ FastAPI   │
└────────┘   └──────────┘   └───────────┘
```

---

## Стек

| Компонент | Технология |
|---|---|
| API Gateway | C# / ASP.NET Core 8 / REST API |
| ORM / Database | Entity Framework Core 8 / SQLite |
| Панель управления | C# / WPF / .NET 8 |
| Микросервисы | Python 3.13 / FastAPI |
| Браузерная автоматизация | Selenium (Chrome) |
| Telegram API | Telethon |
| Документация API | Swagger / OpenAPI |

---

## Структура проекта

```
MAGI/
├── ApiGateway/              # API Gateway (ASP.NET Core)
│   ├── Controllers/         # REST-контроллеры
│   ├── Data/                # EF Core: Entities, DbContext, миграция
│   ├── Models/              # DTO-модели
│   ├── Services/            # Бизнес-логика, оркестрация
│   └── Program.cs           # Точка входа, DI-конфигурация
│
├── Parser/                  # Parser Service (FastAPI, порт 5001)
│   ├── PinterestParser.py   # Парсинг Pinterest
│   ├── PixivParser.py       # Парсинг Pixiv
│   └── service.py           # FastAPI HTTP-сервер
│
├── FilenameTagger/          # Tagger Service (FastAPI, порт 5002)
│   ├── FilenameTagger.py    # Тегирование по имени файла
│   └── service.py           # FastAPI HTTP-сервер
│
├── Auto-post/               # Publisher Service (FastAPI, порт 5003)
│   ├── Auto-post.py         # Публикация в Telegram
│   └── service.py           # FastAPI HTTP-сервер
│
├── AdmPanel/WpfApp1/        # GUI-панель управления (WPF)
├── config/                  # Общий загрузчик конфигурации + Gateway-клиент
├── data/                    # Хранилище данных
│   ├── magi.db              # SQLite база данных (создаётся автоматически)
│   └── json/                # JSON-конфигурации (статические)
└── docs/                    # Документация
```

---

## Поток данных

```
[Parser Service]  ──скачивает──►  New-Images/
         │
         ▼
[Tagger Service]  ──тегирует──►  SQLite (через Gateway)  ──перемещает──►  Check-Images/
         │
         ▼
[Publisher Service]  ──публикует──►  Telegram  ──архивирует──►  Post-Images/
```

Взаимодействие сервисов:
- AdminPanel → API Gateway → Python-сервисы (**HTTP REST**)
- Python-сервисы → API Gateway → SQLite (**данные через HTTP, не файлы**)
- Каждый сервис предоставляет эндпоинты: `/health`, `/status`, `/run`, `/stop`
- API Gateway оркестрирует выполнение, хранит данные и агрегирует статусы

### Хранилище данных

| Данные | Хранилище | Описание |
|---|---|---|
| Images, Posted Images | **SQLite** | Динамические бизнес-данные |
| Schedule Slots | **SQLite** | Расписание публикаций |
| Download Records | **SQLite** | История скачиваний |
| Posting Rules | **SQLite** | Правила публикации |
| Channels, Networks | **SQLite** | Каналы и сети каналов |
| Parser config | **JSON** | Статическая конфигурация |
| Filename tags | **JSON** | Маппинг тегов |
| User settings | **JSON** | Настройки пользователя |

---

## Быстрый старт

### 1. API Gateway

```bash
cd ApiGateway
dotnet run
# → http://localhost:5000
# → Swagger: http://localhost:5000/swagger
```

### 2. Python-сервисы

```bash
# Установка зависимостей (один раз)
pip install fastapi uvicorn pydantic

# Запуск сервисов (каждый в отдельном терминале)
cd Parser && python service.py          # → http://localhost:5001
cd FilenameTagger && python service.py  # → http://localhost:5002
cd Auto-post && python service.py       # → http://localhost:5003
```

### 3. Admin Panel

```bash
dotnet build AdmPanel/WpfApp1/WpfApp1.csproj
# exe: AdmPanel/WpfApp1/bin/Debug/net8.0-windows/MAGIAdmin.exe
```

Пользовательские настройки: `%APPDATA%\MAGI\user_settings.json`

---

## API эндпоинты

| Метод | URL | Описание |
|---|---|---|
| GET | `/health` | Health-check API Gateway |
| GET | `/health/services` | Статус всех сервисов |
| GET | `/api/parser/status` | Статус Parser |
| POST | `/api/parser/run` | Запуск парсинга |
| POST | `/api/parser/stop` | Остановка парсинга |
| GET | `/api/tagger/status` | Статус Tagger |
| POST | `/api/tagger/run` | Запуск тегирования |
| GET | `/api/publisher/status` | Статус Publisher |
| POST | `/api/publisher/run` | Запуск публикации |
| GET | `/api/publisher/stats` | Статистика публикаций |
| GET | `/api/schedule` | Все слоты расписания |
| POST | `/api/schedule` | Создать слот |
| PUT | `/api/schedule/{key}` | Обновить слот |
| DELETE | `/api/schedule/{key}` | Удалить слот |
| GET | `/api/channel` | Все каналы |
| POST | `/api/channel` | Создать канал |
| GET | `/api/channel/networks` | Все сети каналов |
| GET | `/api/data/images` | Изображения (для Python-сервисов) |
| POST | `/api/data/images` | Добавить изображение |
| POST | `/api/data/images/{name}/posted` | Пометить опубликованным |
| GET | `/api/data/schedule/pending` | Pending-слоты |
| PATCH | `/api/data/schedule/{key}/status` | Обновить статус слота |
| GET | `/api/data/downloads/check` | Проверить URL на дубликат |
| POST | `/api/data/downloads` | Добавить запись скачивания |
| GET | `/api/data/rules` | Правила публикации |

Полная документация: **Swagger UI** → `http://localhost:5000/swagger`

---

## Документация

| | |
|---|---|
| [API Gateway](docs/README-ApiGateway.md) | REST API, контроллеры, оркестрация |
| [Admin Panel](docs/README-AdminPanel.md) | Вкладки, окна настроек, сборка |
| [Parser](docs/README-Parser.md) | Pinterest и Pixiv, негативные теги, конфиг |
| [FilenameTagger](docs/README-FilenameTagger.md) | Маппинг тегов, алгоритм тегирования |
| [Auto-post](docs/README-Autopost.md) | Расписание, правила постинга, Telegram |
| [JSON-схемы](docs/README-JSON.md) | Все файлы данных: структура, кто читает, кто пишет |
