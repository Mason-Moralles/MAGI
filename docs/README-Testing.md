# MAGI — Руководство по тестированию

## Структура тестов

```
MAGI/
├── tests/                          # Python-тесты (pytest)
│   ├── conftest.py                 # Общие фикстуры
│   ├── pytest.ini                  # Конфигурация pytest
│   ├── requirements-test.txt       # Зависимости для тестов
│   ├── unit/                       # Unit-тесты
│   │   ├── test_select_art.py      # Логика выбора арта (Auto-post)
│   │   ├── test_filename_tagger.py # FilenameTagger (матчинг, перемещение файлов)
│   │   ├── test_gateway_client.py  # GatewayClient (HTTP-запросы, мок)
│   │   └── test_service_endpoints.py # FastAPI-эндпоинты сервисов
│   ├── integration/                # Интеграционные тесты (требуют Gateway)
│   │   ├── test_channel_api.py     # CRUD каналов, конфигов, сетей
│   │   └── test_schedule_api.py    # Расписание, изображения, загрузки, правила
│   └── scenarios/                  # Сценарные E2E-тесты (требуют Gateway)
│       ├── test_full_publish_flow.py    # Полный цикл публикации
│       └── test_parse_and_tag_flow.py   # Парсинг + тегирование
├── ApiGateway.Tests/               # C# xUnit-тесты
│   ├── ApiGateway.Tests.csproj     # Проект тестов
│   ├── TestDbContextFactory.cs     # Фабрика InMemory DbContext
│   └── Unit/
│       ├── ChannelServiceTests.cs  # Каналы, конфиги, сети, теги
│       └── DataServiceTests.cs     # Изображения, расписание, загрузки, правила
```

---

## Описание тестов

### Python Unit-тесты (`tests/unit/`)

Запускаются **без внешних зависимостей** — все HTTP-вызовы замоканы, файловая система используется через tmpdir.

| Файл | Кол-во тестов | Что тестирует |
|------|---------------|---------------|
| `test_select_art.py` | 9 | Функция `select_art()` из Auto-post: выбор по forced_tag, избегание last_person, fallback, пустой пул, все posted, mixed posted |
| `test_filename_tagger.py` | 10 | `FilenameTagger.main()`: матчинг ключевых слов, перемещение файлов, пропуск обработанных, множественная обработка, регистронезависимость, пропуск не-изображений, получение arts_root из канала |
| `test_gateway_client.py` | 17 | `GatewayClient`: формирование URL/params/payload, парсинг ответов, health-check, обработка 404/500/ConnectionError, active channels, strips trailing slash |
| `test_service_endpoints.py` | 10 | FastAPI-эндпоинты `/health`, `/status`, `/run`, `/stop` для Parser, Tagger, Publisher сервисов через httpx TestClient |

### Python Integration-тесты (`tests/integration/`)

Требуют **запущенный API Gateway** на `http://localhost:5000`.

| Файл | Кол-во тестов | Что тестирует |
|------|---------------|---------------|
| `test_channel_api.py` | 6 | CRUD каналов (create → get → update → delete), получение 404, parser/tagger config CRUD, filename tags replace, сети каналов |
| `test_schedule_api.py` | 9 | Создание/обновление/удаление слотов, нормализация времени, CRUD изображений, mark as posted, download records, правила публикации, health-эндпоинты |

### Python Scenario-тесты (`tests/scenarios/`)

Сквозные E2E-сценарии. Требуют **запущенный API Gateway**.

| Файл | Сценарий |
|------|----------|
| `test_full_publish_flow.py` | Создание канала → теги → изображения → слоты → pending → scheduled → posted → каскадное удаление |
| `test_parse_and_tag_flow.py` | Создание канала → parser-config → tagger-config → filename-теги → download-записи → изображения → проверка привязки к каналу → удаление |

### C# xUnit-тесты (`ApiGateway.Tests/`)

Используют **EF Core InMemory** — не требуют реальной БД или запущенного Gateway.

| Файл | Кол-во тестов | Что тестирует |
|------|---------------|---------------|
| `ChannelServiceTests.cs` | 14 | Создание канала с автогенерацией ID, дефолтные конфиги, GetAll, Update partial fields, каскадное удаление (включая FilenameTags), CRUD parser/tagger config, filename tags (replace, skip empty), сети (create, delete unlinks channels) |
| `DataServiceTests.cs` | 17 | CRUD изображений, фильтрация по каналу, upsert, mark posted (перемещение images→posted), unposted count, слоты расписания (create, normalize time, update status, **изоляция слотов между каналами**), download records (дедупликация, фильтрация по source), posting rules (add, replace), active channels filter |

---

## Гайд по запуску тестов

### Предварительные требования

- **Python 3.11+** (для Python-тестов)
- **.NET 8 SDK** (для C# тестов)
- **API Gateway** запущен на `http://localhost:5000` (для integration и scenario тестов)

### 1. Установка зависимостей Python

```bash
cd MAGI
pip install -r tests/requirements-test.txt
```

### 2. Запуск Python unit-тестов (без Gateway)

```bash
# Все unit-тесты
pytest tests/unit/ -v

# Отдельный файл
pytest tests/unit/test_select_art.py -v

# Отдельный класс
pytest tests/unit/test_select_art.py::TestSelectArtForcedTag -v
```

### 3. Запуск Python integration-тестов (нужен Gateway)

```bash
# Запустить API Gateway
cd ApiGateway && dotnet run &

# Запустить тесты
pytest tests/integration/ -v

# С кастомным URL Gateway
MAGI_GATEWAY_URL=http://localhost:5000 pytest tests/integration/ -v
```

> Если Gateway не запущен, тесты будут автоматически пропущены (skip).

### 4. Запуск Python scenario-тестов (нужен Gateway)

```bash
pytest tests/scenarios/ -v
```

### 5. Запуск всех Python-тестов

```bash
# Все тесты (unit пройдут всегда, integration/scenarios — если Gateway доступен)
pytest tests/ -v

# С отчётом о покрытии (нужен pytest-cov)
pip install pytest-cov
pytest tests/unit/ --cov=config --cov=FilenameTagger -v
```

### 6. Запуск C# xUnit-тестов (без Gateway)

```bash
# Восстановить зависимости и запустить
cd MAGI
dotnet test ApiGateway.Tests/ -v

# Отдельный класс
dotnet test ApiGateway.Tests/ --filter "FullyQualifiedName~ChannelServiceTests" -v

# Отдельный тест
dotnet test ApiGateway.Tests/ --filter "FullyQualifiedName~CreateChannel_ReturnsDto_WithGeneratedId" -v
```

### 7. Запуск всех тестов (Python + C#)

```bash
cd MAGI

# Python
pytest tests/ -v

# C#
dotnet test ApiGateway.Tests/ -v
```

---

## Переменные окружения

| Переменная | По умолчанию | Описание |
|------------|-------------|----------|
| `MAGI_GATEWAY_URL` | `http://localhost:5000` | URL API Gateway для integration/scenario тестов |

---

## Использование Swagger для ручного тестирования

API Gateway предоставляет Swagger UI для ручного тестирования:

1. Запустите Gateway: `cd ApiGateway && dotnet run`
2. Откройте в браузере: `http://localhost:5000/swagger`
3. Swagger содержит все эндпоинты с описаниями и примерами
4. Можно выполнять запросы прямо из интерфейса (кнопка "Try it out")

### Примеры ручных сценариев через Swagger

**Создание канала:**
```
POST /api/channel
{
  "name": "Test Channel",
  "link": "@test_channel",
  "publishMode": "bot",
  "timeZone": "Europe/Moscow"
}
```

**Добавление изображения:**
```
POST /api/data/images
{
  "fileName": "asuka_001.jpg",
  "person": "#Asuka",
  "posted": 0,
  "channelId": "<id из шага выше>"
}
```

**Создание слота:**
```
POST /api/schedule
{
  "date": "2026-12-25",
  "time": "14:30",
  "caption": "Test post",
  "channelId": "<id>"
}
```
