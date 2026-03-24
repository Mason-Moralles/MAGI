# MAGI Database — Архитектура данных

> Этот документ описывает текущую реализацию базы данных MAGI.
> Заменяет устаревший `README-JSON.md`. JSON-файлы больше не используются в runtime.

---

## 1. Общая концепция

### Отказ от JSON

Ранее MAGI хранила данные в разрозненных JSON-файлах:

| Старый файл | Проблемы |
|-------------|----------|
| `images.json` | Глобальный, без привязки к каналу |
| `posted_images.json` | Нет транзакционности при перемещении |
| `schedule.json` | Ключ — ISO-строка, сложно фильтровать |
| `posting_rules.json` | Лежал в `%APPDATA%`, не версионировался |
| `config.json` (Parser) | Один конфиг на все каналы |
| `filename_tags.json` | Один маппинг на все каналы |
| `user_settings.json` | Монолитный файл с путями, кредами, настройками |

### Текущая архитектура

```
Python-сервисы ──HTTP──► API Gateway (ASP.NET Core) ──EF Core──► SQLite
                              │
AdminPanel (WPF) ──HTTP──────►│
```

- **СУБД:** SQLite (файл `magi.db` рядом с Gateway)
- **ORM:** Entity Framework Core 8.0
- **Доступ из Python:** через `GatewayClient` (HTTP-клиент → REST API)
- **Доступ из AdminPanel:** через REST API Gateway
- **Multi-channel:** каждая сущность привязана к `ChannelId`

### Одноразовая миграция

При первом запуске `DataMigrationService` автоматически переносит данные из JSON в SQLite:
- Проверяет `if (table.Any()) return;` — миграция **идемпотентна**
- Мигрирует: images, posted_images, schedule, download records, posting rules, channels, filename tags

---

## 2. ER-диаграмма (связи сущностей)

```
                    ┌──────────────────┐
                    │  ChannelNetworks  │
                    │──────────────────│
                    │  Id (PK, string) │
                    │  Name            │
                    └────────┬─────────┘
                             │ 1
                             │
                             │ 0..N
                    ┌────────┴─────────┐
                    │     Channels     │
                    │──────────────────│
                    │  Id (PK, string) │
                    │  Name            │
                    │  Link            │
                    │  NetworkId (FK?) │──────── логическая связь, без FK constraint
                    │  PublishMode     │
                    │  IsActive        │
                    │  ApiId           │
                    │  ApiHash         │
                    │  BotToken        │
                    │  SessionFile     │
                    │  TimeZone        │
                    │  DelayBetweenPosts│
                    │  ArtsRootPath    │
                    └──┬──┬──┬──┬──┬───┘
          1:1 ┌────────┘  │  │  │  └────────┐ 1:N
              │     1:1   │  │  │  1:N      │
              ▼           │  │  │           ▼
  ┌───────────────────┐   │  │  │   ┌──────────────┐
  │ChannelParserConfigs│  │  │  │   │ FilenameTags │
  │───────────────────│   │  │  │   │──────────────│
  │ Id (PK, int)      │   │  │  │   │ Id (PK)      │
  │ ChannelId (UQ)    │   │  │  │   │ ChannelId    │
  │ Hashtags (JSON)   │   │  │  │   │ Keyword      │
  │ NegativeHashtags  │   │  │  │   │ Tag          │
  │ ImagesPerHashtag  │   │  │  │   └──────────────┘
  │ ScrollDelayMs     │   │  │  │
  │ ImageLoadDelayMs  │   │  │  └────────┐ 1:N
  │ Sources           │   │  │           ▼
  └───────────────────┘   │  │   ┌──────────────┐
                          │  │   │ PostingRules │
  ┌───────────────────┐   │  │   │──────────────│
  │ChannelTaggerConfigs│  │  │   │ Id (PK)      │
  │───────────────────│   │  │   │ ChannelId    │
  │ Id (PK, int)      │   │  │   │ Time         │
  │ ChannelId (UQ)    │   │  │   │ Days (CSV)   │
  │ RenameTemplate    │   │  │   │ Caption      │
  │ Separator         │   │  │   └──────────────┘
  │ OnlyNew           │   │  │
  │ Mode              │   │  └────────┐ 0..N (фильтр)
  └───────────────────┘   │           ▼
                          │   ┌────────────────┐
         0..N (фильтр)    │   │ScheduleSlots   │
              ┌───────────┘   │────────────────│
              ▼               │ Id (PK)        │
      ┌──────────────┐        │ IsoKey (UQ)    │
      │    Images    │        │ Date, Time     │
      │──────────────│        │ Status         │
      │ Id (PK)      │        │ File, Person   │
      │ FileName (UQ)│        │ Caption        │
      │ Person       │        │ ChannelId      │
      │ Posted       │        └────────────────┘
      │ Caption      │
      │ PostTime     │   ┌──────────────────┐
      │ ChannelId    │   │  PostedImages    │
      │ CreatedAt    │   │──────────────────│
      └──────────────┘   │ Id (PK)          │
                         │ FileName (UQ)    │
      ┌──────────────┐   │ Person           │
      │DownloadRecords│  │ PostedAt         │
      │──────────────│   │ Caption          │
      │ Id (PK)      │   │ ChannelId        │
      │ Source       │   └──────────────────┘
      │ SourceUrl(UQ)│
      │ ImageUrl     │
      │ FileName     │
      │ Hashtag      │
      │ DownloadedAt │
      │ ChannelId    │
      └──────────────┘
```

---

## 3. Таблицы — детальное описание

### 3.1 Channels

Центральная сущность системы. Каждый Telegram-канал — отдельная запись с индивидуальными настройками.

| Поле | Тип | Constraints | Описание |
|------|-----|-------------|----------|
| `Id` | string(50) | **PK** | Автогенерируемый 8-символьный hex (`Guid.NewGuid().ToString("N")[..8]`) |
| `Name` | string(200) | Required | Человекочитаемое имя канала |
| `Link` | string(200) | Required | Ссылка Telegram (`@channel_name` или `-100...`) |
| `NetworkId` | string(50)? | Index | Ссылка на `ChannelNetworks.Id` (логическая, без FK constraint) |
| `PublishMode` | string(20) | Default `"user"` | Режим публикации: `"user"` (Telethon) или `"bot"` (Bot API) |
| `IsActive` | bool | Default `true` | Только активные каналы участвуют в публикации |
| `ApiId` | int? | — | Telegram API ID |
| `ApiHash` | string(100)? | — | Telegram API Hash |
| `BotToken` | string(200)? | — | Bot Token (для режима `bot`) |
| `SessionFile` | string(200)? | — | Имя файла Telethon-сессии |
| `TimeZone` | string(50) | Default `"Europe/Moscow"` | IANA timezone для расчёта IsoKey слотов |
| `DelayBetweenPosts` | int | Default `5` | Задержка между постами (секунды) |
| `ArtsRootPath` | string(500) | Default `""` | Абсолютный путь к корневой папке артов |

**Структура папок канала** (создаётся автоматически при создании канала):
```
{ArtsRootPath}/
├── New-Images/     ← сюда Parser скачивает арты
├── Check-Images/   ← сюда Tagger перемещает обработанные
└── Post-Images/    ← (резерв для будущего использования)
```

**Ранее (JSON):** Поля `telegram.*`, `paths.arts_root`, `schedule.time_zone` хранились в `user_settings.json`. Один набор на всю систему. Теперь — per-channel.

---

### 3.2 ChannelNetworks

Группировка каналов в сети (для визуализации и пакетного управления).

| Поле | Тип | Constraints | Описание |
|------|-----|-------------|----------|
| `Id` | string(50) | **PK** | Автогенерируемый 8-символьный hex |
| `Name` | string(200) | Required | Название сети |

**Связь:** `Channel.NetworkId` → `ChannelNetworks.Id` (логическая, без каскада).
При удалении сети каналы **открепляются** (`NetworkId = null`), а не удаляются.

**Ранее (JSON):** Не существовало. Была только одна глобальная конфигурация.

---

### 3.3 ChannelParserConfigs

Конфигурация парсера для конкретного канала. **Связь 1:1** с `Channels`.

| Поле | Тип | Constraints | Default | Описание |
|------|-----|-------------|---------|----------|
| `Id` | int | **PK** (auto) | — | |
| `ChannelId` | string(50) | Required, **Unique** | — | FK на `Channels.Id` |
| `Hashtags` | string | — | `"[]"` | JSON-массив поисковых хэштегов |
| `NegativeHashtags` | string | — | `"[]"` | JSON-массив негативных хэштегов |
| `ImagesPerHashtag` | int | — | `50` | Лимит скачиваний на хэштег |
| `ScrollDelayMs` | int | — | `2000` | Задержка скролла страницы (мс) |
| `ImageLoadDelayMs` | int | — | `1000` | Задержка между скачиваниями (мс) |
| `Sources` | string(200) | — | `"pinterest"` | Источники через запятую (`pinterest`, `pixiv`) |

**Создаётся автоматически** при создании канала с дефолтными значениями.

**Ранее (JSON):** `data/json/Parser/config.json` — один на всю систему.

---

### 3.4 ChannelTaggerConfigs

Конфигурация теггера для конкретного канала. **Связь 1:1** с `Channels`.

| Поле | Тип | Constraints | Default | Описание |
|------|-----|-------------|---------|----------|
| `Id` | int | **PK** (auto) | — | |
| `ChannelId` | string(50) | Required, **Unique** | — | FK на `Channels.Id` |
| `RenameTemplate` | string(500) | — | `"{artist}_{title}_{id}"` | Шаблон переименования |
| `Separator` | string(10) | — | `"_"` | Разделитель тегов |
| `OnlyNew` | bool | — | `true` | Обрабатывать только новые файлы |
| `Mode` | string(20) | — | `"rename"` | Режим: `rename` или `copy` |

**Создаётся автоматически** при создании канала.

**Ранее (JSON):** `user_settings.json → tagger.*` — один набор, **FilenameTagger его не использовал**.

---

### 3.5 FilenameTags

Маппинг «ключевое слово в имени файла → хэштег персонажа». **Связь 1:N** с `Channels`.

| Поле | Тип | Constraints | Описание |
|------|-----|-------------|----------|
| `Id` | int | **PK** (auto) | |
| `Keyword` | string(100) | Required | Слово для поиска (lowercase, trimmed) |
| `Tag` | string(200) | Required | Хэштег персонажа (`#Asuka_Langley`) |
| `ChannelId` | string(50) | Required, Index | FK на `Channels.Id` |

**API:** `PUT /api/channel/{id}/filename-tags` — **полная замена** всех тегов канала (delete old + insert new).

**Ранее (JSON):** `data/json/FilenameTagger/filename_tags.json` — один `{ "keyword": "#Tag" }` на всю систему.

---

### 3.6 Images

Неопубликованные изображения (очередь на публикацию).

| Поле | Тип | Constraints | Описание |
|------|-----|-------------|----------|
| `Id` | int | **PK** (auto) | |
| `FileName` | string(500) | Required, **Unique** | Имя файла — бизнес-ключ |
| `Person` | string(200)? | — | Хэштег персонажа (`#Asuka_Langley`) |
| `Posted` | int | Default `0` | 0 = в очереди (всегда 0 в этой таблице) |
| `Caption` | string(1000) | Default `""` | Подпись к арту |
| `PostTime` | string(50)? | — | (не используется — legacy) |
| `ChannelId` | string(50)? | — | Канал, к которому привязан арт |
| `CreatedAt` | DateTime | Default `UtcNow` | Время добавления |

**Upsert-логика:** Если `FileName` уже существует — обновляются `Person`, `Caption`, `ChannelId`.

**Жизненный цикл:**
```
Tagger добавляет (POST /api/data/images)
       │
       ▼
  Images (posted=0)
       │
       │ Publisher вызывает POST /api/data/images/{fileName}/posted
       ▼
  DELETE из Images + INSERT в PostedImages
```

**Ранее (JSON):** `data/json/images/images.json` — dict `{ "filename.jpg": { "person": "...", "posted": 0 } }`.

---

### 3.7 PostedImages

Архив опубликованных изображений.

| Поле | Тип | Constraints | Описание |
|------|-----|-------------|----------|
| `Id` | int | **PK** (auto) | |
| `FileName` | string(500) | Required, **Unique** | Имя файла |
| `Person` | string(200)? | — | Хэштег персонажа |
| `PostedAt` | string(50)? | — | ISO-время публикации |
| `Caption` | string(1000) | Default `""` | Подпись поста |
| `ChannelId` | string(50)? | — | Канал публикации |

**Ранее (JSON):** `data/json/images/posted_images.json`.

---

### 3.8 ScheduleSlots

Слоты расписания публикаций.

| Поле | Тип | Constraints | Описание |
|------|-----|-------------|----------|
| `Id` | int | **PK** (auto) | |
| `IsoKey` | string(50) | Required, **Unique** | ISO-8601 ключ: `2026-02-17T07:29:00+03:00` |
| `Date` | string(20) | — | Дата `YYYY-MM-DD` |
| `Time` | string(10) | — | Время `HH:MM` |
| `Status` | string(20) | Required, Default `"pending"` | Статус слота |
| `File` | string(500)? | — | Привязанный файл арта |
| `Person` | string(200)? | — | Хэштег персонажа |
| `Caption` | string(1000) | Default `""` | Подпись поста |
| `ChannelId` | string(50)? | — | Канал |

**Статусы слота:**
```
pending ──(Publisher привязывает арт)──► scheduled ──(публикация)──► posted
   │
   └──(время прошло, арт не привязан)──► missed
   │
   └──(ошибка публикации)──► error
```

**Генерация IsoKey:**
```
Date="2026-06-15", Time="7:29", TimeZone="Europe/Moscow"
  → нормализация: "07:29"
  → offset: "+03:00"
  → IsoKey: "2026-06-15T07:29:00+03:00"
```

**Upsert:** Если слот с таким `IsoKey` уже существует — обновляется, а не дублируется.

**Ранее (JSON):** `data/json/schedule.json` — dict `{ "ISO_KEY": { date, time, status, ... } }`.

---

### 3.9 PostingRules

Правила автоматической генерации слотов расписания.

| Поле | Тип | Constraints | Описание |
|------|-----|-------------|----------|
| `Id` | int | **PK** (auto) | |
| `Time` | string(10) | Required | Время публикации `HH:MM` |
| `Days` | string(200) | Required | Дни недели CSV: `Monday,Wednesday,Friday` |
| `Caption` | string(1000) | Default `""` | Подпись к посту |
| `ChannelId` | string(50)? | Index | Канал |

**API замены:** `PUT /api/data/rules?channelId=X` — удаляет все правила канала и вставляет новые (batch replace).

**Ранее (JSON):** `%APPDATA%\MAGI\posting_rules.json` — `{ "rules": [{ time, days[], caption }] }`. Один набор на всю систему.

---

### 3.10 DownloadRecords

Записи о скачанных изображениях (дедупликация парсера).

| Поле | Тип | Constraints | Описание |
|------|-----|-------------|----------|
| `Id` | int | **PK** (auto) | |
| `Source` | string(50) | Required | Источник: `pinterest`, `pixiv` |
| `SourceUrl` | string(2000) | Required, **Unique** | URL пина/арта (ключ дедупликации) |
| `ImageUrl` | string(2000) | Default `""` | Прямой URL изображения |
| `FileName` | string(500) | Required | Имя сохранённого файла |
| `Hashtag` | string(200) | Default `""` | Хэштег поискового запроса |
| `DownloadedAt` | DateTime | Default `UtcNow` | Время скачивания |
| `ChannelId` | string(50)? | — | Канал, для которого скачан |

**Проверка дубликата:** `GET /api/data/downloads/check?sourceUrl=...` — Parser вызывает перед каждым скачиванием.

**Ранее (JSON):** `Pinterest_downloaded_images.json`, `Pixiv_downloaded_images.json` — два отдельных массива.

---

## 4. Сопоставление JSON → DB

### user_settings.json → Channels + Configs

| JSON-секция | JSON-ключ | DB-таблица | DB-поле |
|---|---|---|---|
| `paths.arts_root` | `"D:\\MAGI-Images"` | `Channels` | `ArtsRootPath` |
| `telegram.channel_link` | `"@magi_test"` | `Channels` | `Link` |
| `telegram.api_id` | `20860465` | `Channels` | `ApiId` |
| `telegram.api_hash` | `"fa75..."` | `Channels` | `ApiHash` |
| `telegram.bot_token` | `""` | `Channels` | `BotToken` |
| `telegram.session_file` | `"session"` | `Channels` | `SessionFile` |
| `schedule.time_zone` | `"Europe/Moscow"` | `Channels` | `TimeZone` |
| `schedule.schedule_days` | `5` | — | **Не мигрировано** (см. п.6) |
| `tagger.rename_template` | `"{artist}_{title}_{id}"` | `ChannelTaggerConfigs` | `RenameTemplate` |
| `tagger.separator` | `"_"` | `ChannelTaggerConfigs` | `Separator` |
| `tagger.only_new` | `true` | `ChannelTaggerConfigs` | `OnlyNew` |
| `tagger.mode` | `"rename"` | `ChannelTaggerConfigs` | `Mode` |
| `db.images_json` | `"data/json/..."` | — | **Удалено** (данные в SQLite) |
| `db.posted_images_json` | `"data/json/..."` | — | **Удалено** |
| `db.schedule_json` | `"data/json/..."` | — | **Удалено** |
| `parser.source_path` | `"C:\\...\\Downloads"` | — | **Не мигрировано** (не использовалось) |

### posting_rules.json → PostingRules

| JSON | DB |
|---|---|
| `rules[].time` | `PostingRules.Time` |
| `rules[].days` (array) | `PostingRules.Days` (CSV-строка `"Monday,Wednesday"`) |
| `rules[].caption` | `PostingRules.Caption` |
| — (глобально) | `PostingRules.ChannelId` (per-channel) |

### Parser/config.json → ChannelParserConfigs

| JSON-ключ | DB-поле |
|---|---|
| `hashtags` (array) | `Hashtags` (JSON-строка `'["tag1","tag2"]'`) |
| `negativeHashtags` (array) | `NegativeHashtags` (JSON-строка) |
| `imagesPerHashtag` | `ImagesPerHashtag` |
| `scrollDelayMs` | `ScrollDelayMs` |
| `imageLoadDelayMs` | `ImageLoadDelayMs` |
| `downloadPath` | **Удалено** — путь вычисляется из `Channel.ArtsRootPath + "/New-Images"` |
| `databasePath` | **Удалено** — данные в `DownloadRecords` |
| — | `Sources` (новое: `"pinterest"`, `"pinterest,pixiv"`) |

### filename_tags.json → FilenameTags

| JSON | DB |
|---|---|
| ключ `"asuka"` | `FilenameTags.Keyword` |
| значение `"#Asuka_Langley"` | `FilenameTags.Tag` |
| — (глобально) | `FilenameTags.ChannelId` (per-channel) |

### defaults.json → Удалён

Содержал пути к подпапкам (`check-images`, `new-images`) и `delay_between_post`.
Теперь:
- Подпапки — хардкод `New-Images`, `Check-Images`, `Post-Images` в `ChannelService.EnsureArtsFolderStructure()`
- `delay_between_post` → `Channels.DelayBetweenPosts`

---

## 5. Поток данных (Pipeline)

```
┌─────────┐   GET /api/channel/{id}/parser-config    ┌────────────┐
│ Parser  │◄─────────────────────────────────────────│ API Gateway│
│ Service │                                          │  (SQLite)  │
│ :5001   │──POST /api/data/downloads───────────────►│   :5000    │
│         │──GET /api/data/downloads/check──────────►│            │
└─────────┘                                          │            │
     │ скачивает файлы в {ArtsRootPath}/New-Images   │            │
     ▼                                               │            │
┌─────────┐   GET /api/channel/{id}/filename-tags    │            │
│ Tagger  │◄─────────────────────────────────────────│            │
│ Service │                                          │            │
│ :5002   │──POST /api/data/images──────────────────►│            │
│         │  перемещает файлы New-Images→Check-Images │            │
└─────────┘                                          │            │
     │                                               │            │
     ▼                                               │            │
┌──────────┐  GET /api/data/images?channelId=X       │            │
│Publisher │◄─────────────────────────────────────────│            │
│ Service  │  GET /api/data/schedule/pending          │            │
│ :5003    │  GET /api/data/rules?channelId=X         │            │
│          │──POST /api/data/images/{f}/posted──────►│            │
│          │──PATCH /api/data/schedule/{k}/status───►│            │
└──────────┘                                          └────────────┘
     │
     ▼
  Telegram
```

### Детальный поток

1. **Parser** получает конфиг канала (`ChannelParserConfigs`) → скачивает изображения → пишет `DownloadRecords` → файлы попадают в `{ArtsRootPath}/New-Images/`

2. **Tagger** получает filename-теги (`FilenameTags`) → сканирует `New-Images/` → матчит ключевые слова → пишет `Images` → перемещает файлы в `Check-Images/`

3. **Publisher** получает pending-слоты (`ScheduleSlots`) + неопубликованные арты (`Images`) + правила (`PostingRules`) → выбирает арт → обновляет слот (status → `scheduled`) → публикует в Telegram → перемещает запись (`Images` → `PostedImages`)

---

## 6. Индексы и ограничения

| Таблица | Тип | Поле(я) | Описание |
|---------|-----|---------|----------|
| `Images` | Unique Index | `FileName` | Нет дубликатов файлов |
| `PostedImages` | Unique Index | `FileName` | Нет дубликатов файлов |
| `ScheduleSlots` | Unique Composite | `(IsoKey, ChannelId)` | Разные каналы могут иметь слоты в одно время |
| `ScheduleSlots` | Index | `IsoKey` | Быстрый поиск по времени |
| `ScheduleSlots` | Index | `ChannelId` | Быстрая фильтрация по каналу |
| `DownloadRecords` | Unique Index | `SourceUrl` | Нет повторных скачиваний |
| `Channels` | Index | `NetworkId` | Быстрая фильтрация по сети |
| `PostingRules` | Index | `ChannelId` | Быстрая фильтрация по каналу |
| `ChannelParserConfigs` | Unique Index | `ChannelId` | 1:1 с каналом |
| `ChannelTaggerConfigs` | Unique Index | `ChannelId` | 1:1 с каналом |
| `FilenameTags` | Index | `ChannelId` | 1:N с каналом |

**Каскадное удаление** (реализовано в `ChannelService.DeleteChannelAsync`):
При удалении канала удаляются:
- `ChannelParserConfigs` (1:1)
- `ChannelTaggerConfigs` (1:1)
- `PostingRules` (1:N)
- `ScheduleSlots` (1:N)
- `Images` (1:N)
- `PostedImages` (1:N)
- `FilenameTags` (1:N)

> Каскад реализован **в коде сервиса**, а не через FK constraints в SQLite.

---

## 7. Привязка к ChannelId — полный аудит

| Таблица | ChannelId | Тип поля | Required? | Статус |
|---------|:---------:|----------|:---------:|--------|
| `Channels` | — (это сам канал) | PK | — | — |
| `ChannelParserConfigs` | **да** | string | **Required** | OK |
| `ChannelTaggerConfigs` | **да** | string | **Required** | OK |
| `FilenameTags` | **да** | string | **Required** | OK |
| `PostingRules` | **да** | string? | Nullable | **Потенциальная проблема** |
| `ScheduleSlots` | **да** | string? | Nullable | **Потенциальная проблема** |
| `Images` | **да** | string? | Nullable | **Потенциальная проблема** |
| `PostedImages` | **да** | string? | Nullable | OK (legacy-данные) |
| `DownloadRecords` | **да** | string? | Nullable | **Потенциальная проблема** |

---

## 8. Выявленные проблемы и edge cases

### 8.1. Nullable ChannelId в ключевых таблицах

`PostingRules`, `ScheduleSlots`, `Images`, `DownloadRecords` имеют `ChannelId` как **nullable**.

**Следствие:** возможно существование «бесхозных» записей без привязки к каналу:
- Правила публикации без канала не будут применяться корректно
- Слоты без канала не получат правильный TimeZone

**Причина:** обратная совместимость с мигрированными JSON-данными (до multi-channel).

**Рекомендация:** после полной миграции сделать `ChannelId` Required в этих таблицах.

### 8.2. Нет FK constraints в SQLite

Связи между таблицами реализованы **только на уровне кода** (в `ChannelService`, `DataService`). SQLite не проверяет целостность при прямых SQL-запросах.

**Следствие:** при ручном редактировании БД возможны осиротевшие записи.

**Рекомендация:** добавить `PRAGMA foreign_keys = ON` и настоящие FK в `OnModelCreating`.

### 8.3. Hashtags/NegativeHashtags хранятся как JSON-строки

В `ChannelParserConfigs` поля `Hashtags` и `NegativeHashtags` — это `string`, содержащий JSON-массив (`'["tag1","tag2"]'`).

**Следствие:** невозможен SQL-запрос «найди все каналы с хэштегом X». Десериализация только в коде.

**Компромисс:** допустимо для SQLite с малым количеством каналов. При масштабировании — вынести в отдельную таблицу.

### 8.4. Days в PostingRules — CSV-строка

Дни недели хранятся как `"Monday,Wednesday,Friday"` вместо нормализованной таблицы.

**Компромисс:** упрощает API и маппинг. Допустимо при текущем масштабе.

### 8.5. Отсутствие поля schedule_days

Из `user_settings.json → schedule.schedule_days` (на сколько дней вперёд генерировать слоты) не мигрировано в БД. Publisher использует захардкоженное значение или параметр из Gateway.

### 8.6. PostTime в Images — legacy

Поле `PostTime` в таблице `Images` не используется в текущем коде. Публикация перемещает запись в `PostedImages.PostedAt`.

---

## 9. API-эндпоинты для работы с данными

### Channel Management (`/api/channel`)

| Метод | Путь | Описание |
|-------|------|----------|
| `GET` | `/api/channel` | Все каналы |
| `GET` | `/api/channel/{id}` | Канал по ID |
| `POST` | `/api/channel` | Создать канал (+ дефолтные конфиги) |
| `PUT` | `/api/channel/{id}` | Обновить канал (partial) |
| `DELETE` | `/api/channel/{id}` | Удалить канал (каскад) |
| `GET` | `/api/channel/{id}/parser-config` | Конфиг парсера |
| `PUT` | `/api/channel/{id}/parser-config` | Обновить конфиг парсера (partial) |
| `GET` | `/api/channel/{id}/tagger-config` | Конфиг теггера |
| `PUT` | `/api/channel/{id}/tagger-config` | Обновить конфиг теггера (partial) |
| `GET` | `/api/channel/{id}/filename-tags` | Filename-теги |
| `PUT` | `/api/channel/{id}/filename-tags` | Заменить все теги |
| `GET` | `/api/channel/networks` | Все сети |
| `POST` | `/api/channel/networks` | Создать сеть |
| `DELETE` | `/api/channel/networks/{id}` | Удалить сеть |

### Data API (`/api/data`)

| Метод | Путь | Описание |
|-------|------|----------|
| `GET` | `/api/data/images` | Изображения (фильтр: `channelId`, `unpostedOnly`) |
| `GET` | `/api/data/images/{fileName}` | Конкретное изображение |
| `POST` | `/api/data/images` | Добавить изображение |
| `POST` | `/api/data/images/{fileName}/posted` | Пометить как опубликованное |
| `DELETE` | `/api/data/images/{fileName}` | Удалить изображение |
| `GET` | `/api/data/schedule/pending` | Pending-слоты (фильтр: `channelId`) |
| `PATCH` | `/api/data/schedule/{isoKey}/status` | Обновить статус слота |
| `GET` | `/api/data/channels/active` | Активные каналы |
| `GET` | `/api/data/rules` | Правила публикации (фильтр: `channelId`) |
| `POST` | `/api/data/rules` | Добавить правило |
| `PUT` | `/api/data/rules` | Заменить все правила канала |
| `DELETE` | `/api/data/rules/{id}` | Удалить правило |
| `GET` | `/api/data/downloads/check` | Проверить дубликат URL |
| `POST` | `/api/data/downloads` | Добавить запись о скачивании |
| `GET` | `/api/data/downloads/count` | Статистика скачиваний |

### Schedule CRUD (`/api/schedule`)

| Метод | Путь | Описание |
|-------|------|----------|
| `GET` | `/api/schedule` | Все слоты (фильтр: `channelId`) |
| `GET` | `/api/schedule/{isoKey}` | Слот по ключу |
| `POST` | `/api/schedule` | Создать слот |
| `PUT` | `/api/schedule/{isoKey}` | Обновить слот |
| `DELETE` | `/api/schedule/{isoKey}` | Удалить слот |
| `PUT` | `/api/schedule/update` | Обновить (isoKey в body) |
| `POST` | `/api/schedule/delete` | Удалить (isoKey в body) |
| `GET` | `/api/schedule/pending` | Только pending |
| `GET` | `/api/schedule/images` | Все изображения |
| `GET` | `/api/schedule/posted` | Опубликованные |

---

## 10. Доступ из Python

Python-сервисы **не обращаются к SQLite напрямую**. Весь доступ — через HTTP:

```python
from config.gateway_client import GatewayClient

gw = GatewayClient()  # http://localhost:5000

# Получить filename-теги для канала
tags = gw.get_filename_tags("channel_id")

# Добавить изображение
gw.add_image("art.jpg", "#Asuka", channel_id="channel_id")

# Проверить дубликат скачивания
if not gw.is_downloaded("https://pinterest.com/pin/123"):
    gw.add_download_record(source="pinterest", source_url="...", ...)
```

Все методы `GatewayClient` задокументированы в `config/gateway_client.py`.
