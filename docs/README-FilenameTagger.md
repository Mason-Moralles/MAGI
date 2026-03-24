# FilenameTagger — Микросервис тегирования

Сканирует папку `New-Images`, по ключевым словам в имени файла определяет персонажа,
записывает метаданные в базу данных через Gateway и перемещает файл в `Check-Images`.

---

## Файлы

| Файл | Описание |
|---|---|
| `FilenameTagger/service.py` | FastAPI HTTP-сервер (порт 5002) |
| `FilenameTagger/FilenameTagger.py` | Основной скрипт тегирования (v2.0) |
| `FilenameTagger/requirements.txt` | Зависимости Python |
| `config/gateway_client.py` | HTTP-клиент для взаимодействия с Gateway |

---

## Запуск

### Как HTTP-сервис (рекомендуется)

```bash
cd FilenameTagger
pip install -r requirements.txt
python service.py
# → http://localhost:5002
```

API-эндпоинты:

| Метод | URL | Описание |
|---|---|---|
| GET | `/health` | Health-check сервиса |
| GET | `/status` | Статус текущей задачи |
| POST | `/run` | Запуск тегирования |
| POST | `/stop` | Остановка |

### Через AdminPanel

Запускается из AdminPanel (вкладка **Микросервисы**, кнопка **START** у блока **Tagger**).

### Через API Gateway

```bash
POST http://localhost:5000/api/tagger/run
```

---

## Источники данных

Все данные читаются и записываются **через API Gateway** (HTTP REST → SQLite).

### Что читает (из Gateway)

| Endpoint | Что берёт |
|---|---|
| `GET /api/channel/{id}` | Данные канала (`ArtsRootPath` → пути к папкам) |
| `GET /api/channel/{id}/filename-tags` | Маппинг: ключевое слово → хэштег персонажа (per-channel) |
| `GET /api/data/images?channelId=X` | Существующие записи (чтобы не перезаписывать уже обработанные) |

### Что читает (локальная ФС)

| Путь | Описание |
|---|---|
| `{ArtsRootPath}/New-Images/*.jpg/.png/.jpeg` | Файлы для обработки |

### Что пишет (в Gateway)

| Endpoint | Что записывает |
|---|---|
| `POST /api/data/images` | Новая запись изображения: `{ fileName, person: "#Tag", posted: 0, channelId }` |

### Что перемещает (локальная ФС)

| Откуда | Куда |
|---|---|
| `{ArtsRootPath}/New-Images/{file}` | `{ArtsRootPath}/Check-Images/{file}` |

---

## Логика работы

1. Получить данные канала из Gateway → определить `ArtsRootPath`
2. Построить пути: `new_images_dir = ArtsRootPath/New-Images`, `check_images_dir = ArtsRootPath/Check-Images`
3. Получить filename-теги из Gateway (`GET /api/channel/{id}/filename-tags`)
4. Получить существующие записи изображений из Gateway
5. Для каждого файла в `New-Images`:
   - Пропустить не-изображения (не `.jpg`, `.png`, `.jpeg`)
   - Если файл уже есть в БД с заполненным `person` → пропустить
   - Перебрать ключевые слова из filename-тегов
   - Если ключевое слово найдено в имени файла (регистронезависимо) → записать в Gateway через `POST /api/data/images`
   - Переместить файл `New-Images → Check-Images`
6. Вернуть количество обработанных файлов

> **Важно:** если ни одно ключевое слово не совпало — файл остаётся в `New-Images` и не попадает в БД. Нужно добавить нужное ключевое слово в filename-теги канала.

---

## Filename-теги

Маппинг хранится в БД (таблица `FilenameTags`), привязан к конкретному каналу.
Ключ — подстрока для поиска (регистронезависимо). Значение — хэштег персонажа.

Пример тегов:

| Keyword | Tag |
|---|---|
| `shinji` | `#Shinji_Ikari` |
| `rei` | `#Rei_Ayanami` |
| `ayanami` | `#Rei_Ayanami` |
| `asuka` | `#Asuka_Langley` |
| `langley` | `#Asuka_Langley` |
| `misato` | `#Misato_Katsuragi` |
| `mari` | `#Mari_Makinami` |
| `綾波レイ` | `#Rei_Ayanami` |

**Управление через AdminPanel:** вкладка **Микросервисы** → ⚙ Tagger → `TaggerSettingsWindow` → секция «Filename Tags».

**API:** `PUT /api/channel/{id}/filename-tags` — **полная замена** всех тегов канала.

---

## Результат тегирования (запись в Images)

После обработки файла `asuka_langley_0001.jpg` в БД создаётся запись:

| Поле | Значение |
|---|---|
| `FileName` | `asuka_langley_0001.jpg` |
| `Person` | `#Asuka_Langley` |
| `Posted` | `0` |
| `ChannelId` | `<id канала>` |

> После планирования публикации Publisher **перемещает** запись из `Images` в `PostedImages`.

---

## Настройка через AdminPanel

Вкладка **Микросервисы** → кнопка ⚙ рядом с **Tagger** → окно `TaggerSettingsWindow`:

| Поле | Поле конфига (Gateway) | Описание |
|---|---|---|
| Шаблон переименования | `renameTemplate` | Шаблон имени файла |
| Разделитель | `separator` | Разделитель в имени |
| Только новые | `onlyNew` | Обрабатывать только необработанные |
| Режим | `mode` | `rename` или `copy` |

Filename-теги редактируются в отдельной секции того же окна.

---

## Зависимости Python

```
fastapi       # HTTP-сервер
uvicorn       # ASGI-сервер
pydantic      # Валидация данных
```
