# API Gateway

REST API Gateway на ASP.NET Core 8, центральный компонент системы MAGI.

---

## Назначение

API Gateway обеспечивает:
- Единую точку входа для всех клиентов (AdminPanel, внешние интеграции)
- Оркестрацию Python-микросервисов (Parser, Tagger, Publisher)
- Мониторинг состояния сервисов (health checks)
- CRUD-операции с расписанием публикаций
- Управление каналами и сетями каналов
- **Хранение данных в SQLite через Entity Framework Core**
- **Data API для Python-сервисов** (images, schedule, downloads)
- Автоматическую миграцию данных из JSON в SQLite при первом запуске
- Автодокументацию API (Swagger/OpenAPI)

---

## Стек

| Компонент | Технология |
|---|---|
| Фреймворк | ASP.NET Core 8 |
| Язык | C# 12 |
| ORM | Entity Framework Core 8 |
| База данных | SQLite (файл `data/magi.db`) |
| API-документация | Swashbuckle / Swagger |
| HTTP-клиент | HttpClient (typed) |
| Сериализация | System.Text.Json |

---

## Структура

```
ApiGateway/
├── Controllers/
│   ├── HealthController.cs      # /health, /health/services
│   ├── ProcessController.cs     # /api/process/* (управление Python-процессами)
│   ├── ParserController.cs      # /api/parser/*
│   ├── TaggerController.cs      # /api/tagger/*
│   ├── PublisherController.cs   # /api/publisher/*
│   ├── ChannelController.cs     # /api/channel/*
│   ├── ScheduleController.cs    # /api/schedule/*
│   └── DataController.cs        # /api/data/* (Data API для Python-сервисов)
│
├── Data/
│   ├── Entities.cs              # EF Core сущности (Image, Schedule, Channel...)
│   ├── MagiDbContext.cs         # EF Core DbContext
│   └── DataMigrationService.cs  # Миграция JSON → SQLite при первом запуске
│
├── Models/
│   ├── ServiceModels.cs         # ServiceStatusDto, TaskResultDto, ApiResponse
│   ├── ScheduleModels.cs        # ScheduleSlotDto, PostingRuleDto, ImageDto
│   └── ChannelModels.cs         # ChannelDto, ChannelNetworkDto
│
├── Services/
│   ├── PythonServiceClient.cs   # HTTP-клиент для Python-сервисов
│   ├── ServiceOrchestrator.cs   # Оркестрация и мониторинг сервисов
│   ├── ProcessManager.cs        # Управление жизненным циклом Python-процессов
│   ├── DataService.cs           # Доступ к данным через EF Core (SQLite)
│   └── ChannelService.cs        # Управление каналами и сетями
│
├── Program.cs                   # DI, middleware, Swagger
├── appsettings.json             # Конфигурация сервисов
└── MAGI.ApiGateway.csproj       # Проект (.NET 8)
```

---

## Конфигурация сервисов

`appsettings.json`:

```json
{
  "MagiServices": {
    "Parser":    { "BaseUrl": "http://localhost:5001", "Name": "parser-service" },
    "Tagger":    { "BaseUrl": "http://localhost:5002", "Name": "tagger-service" },
    "Publisher": { "BaseUrl": "http://localhost:5003", "Name": "publisher-service" }
  }
}
```

---

## Взаимодействие с микросервисами

API Gateway общается с Python-сервисами по HTTP:

```
AdminPanel ──► API Gateway ──► Python Service
   (WPF)        (ASP.NET)       (FastAPI)
                    │
                    ├─ GET  /health   → 200 {"status": "healthy"}
                    ├─ GET  /status   → 200 {"task_id": "...", "status": "running"}
                    ├─ POST /run      → 200 {"task_id": "...", "status": "running"}
                    └─ POST /stop     → 200 {"status": "stopped"}
```

Каждый Python-сервис реализует единый контракт с 4 эндпоинтами.

---

## Сборка и запуск

```bash
cd ApiGateway
dotnet restore
dotnet run
```

Доступен на: `http://localhost:5000`
Swagger UI: `http://localhost:5000/swagger`

### Доступ с телефона по Wi-Fi

Для физического Android-устройства одного `localhost` недостаточно: телефон не видит loopback интерфейс компьютера.

Запусти Gateway с LAN-профилем:

```bash
cd ApiGateway
dotnet run --launch-profile http-lan
```

Этот профиль поднимает Gateway сразу на двух адресах:
- `http://localhost:5000`
- `http://0.0.0.0:5000`

Дальше:
- узнай IP компьютера в локальной сети, например через `ipconfig`
- проверь, что телефон и ПК находятся в одной Wi-Fi сети
- открой в телефоне адрес вида `http://<IP_ПК>:5000/health`
- если запрос не проходит, разреши входящие подключения для `dotnet` или порта `5000` в Windows Firewall

Для MAGI.Mobile на физическом устройстве в `Settings` нужно указывать адрес вида `http://<IP_ПК>:5000`, а не `localhost` и не `10.0.2.2`.

---

## Контроллеры

### HealthController
- `GET /health` — health-check самого Gateway
- `GET /health/services` — статусы всех микросервисов

### ProcessController
- `GET /api/process/status` — статус всех Python-процессов
- `POST /api/process/{service}/start` — запустить Python-процесс
- `POST /api/process/{service}/stop` — остановить Python-процесс

### ParserController
- `GET /api/parser/status` — статус Parser-сервиса
- `POST /api/parser/run` — запуск парсинга (body: `{"sources": ["pinterest", "pixiv"]}`)
- `POST /api/parser/stop` — остановка

### TaggerController
- `GET /api/tagger/status` — статус Tagger-сервиса
- `POST /api/tagger/run` — запуск тегирования
- `POST /api/tagger/stop` — остановка

### PublisherController
- `GET /api/publisher/status` — статус Publisher-сервиса
- `POST /api/publisher/run` — запуск публикации
- `POST /api/publisher/stop` — остановка
- `GET /api/publisher/stats` — статистика (сколько артов, слотов и т.д.)

### ScheduleController
- `GET /api/schedule` — все слоты (`?channelId=` — фильтр по каналу)
- `GET /api/schedule/pending` — только pending-слоты
- `GET /api/schedule/images` — изображения для публикации
- `GET /api/schedule/posted` — опубликованные изображения
- `GET /api/schedule/{isoKey}` — слот по ключу (`?channelId=` — для мультиканальной идентификации)
- `POST /api/schedule` — создать слот (channelId в body)
- `PUT /api/schedule/{isoKey}` — обновить слот (channelId в body)
- `DELETE /api/schedule/{isoKey}` — удалить слот (`?channelId=`)
- `PUT /api/schedule/update` — обновить слот (isoKey + channelId в body)
- `POST /api/schedule/delete` — удалить слот (isoKey + channelId в body)

### ChannelController
- `GET /api/channel` — все каналы
- `GET /api/channel/{id}` — канал по ID
- `POST /api/channel` — создать канал (автосоздание дефолтных конфигов парсера/теггера)
- `PUT /api/channel/{id}` — обновить канал
- `DELETE /api/channel/{id}` — удалить канал (каскадное удаление конфигов)
- `GET /api/channel/{id}/parser-config` — конфиг парсера для канала
- `PUT /api/channel/{id}/parser-config` — обновить конфиг парсера
- `GET /api/channel/{id}/tagger-config` — конфиг теггера для канала
- `PUT /api/channel/{id}/tagger-config` — обновить конфиг теггера
- `GET /api/channel/networks` — все сети
- `POST /api/channel/networks` — создать сеть
- `DELETE /api/channel/networks/{id}` — удалить сеть

### DataController
- `GET /api/data/images` — изображения (`?channelId=`, `?unpostedOnly=`)
- `POST /api/data/images` — добавить изображение
- `POST /api/data/images/{name}/posted` — пометить опубликованным
- `DELETE /api/data/images/{name}` — удалить
- `GET /api/data/schedule/pending` — pending-слоты (`?channelId=`)
- `PATCH /api/data/schedule/{key}/status` — обновить статус
- `GET /api/data/channels/active` — активные каналы для Publisher
- `GET /api/data/downloads/check` — проверить URL на дубликат
- `POST /api/data/downloads` — добавить запись скачивания
- `GET /api/data/downloads/count` — статистика (`?source=`)
- `GET /api/data/rules` — правила публикации (`?channelId=`)
