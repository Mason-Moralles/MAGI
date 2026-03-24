# Admin Panel — Панель управления

WPF-приложение (.NET 8) для управления всеми микросервисами MAGI.

**Сборка:**
```bash
dotnet build AdmPanel/WpfApp1/WpfApp1.csproj
# exe: AdmPanel/WpfApp1/bin/Debug/net8.0-windows/MAGIAdmin.exe
```

---

## Архитектура: Канал = контейнер

**Все данные и настройки привязаны к конкретному каналу:**

```
[Глобальный ComboBox канала] -> выбран канал
  |
  ├── Вкладка "Микросервисы" -> запуск с channelId
  ├── Вкладка "База артов"   -> фильтр по ArtsRootPath канала
  ├── Вкладка "Расписание"   -> фильтр по channelId
  ├── ParserSettingsWindow    -> per-channel ChannelParserConfig
  ├── TaggerSettingsWindow    -> per-channel ChannelTaggerConfig
  └── AutopostSettingsWindow  -> Telegram-креденшалы канала
```

**Если канал не выбран** -> все окна и данные пустые, запуск сервисов заблокирован.

---

## Вкладки

### Микросервисы

Запуск и остановка Python-скриптов. Вывод (stdout/stderr) отображается в консоли логов.

| Сервис | Настройки (кнопка) | Источник конфига |
|---|---|---|
| Parser Pinterest/Pixiv | `ParserSettingsWindow` | Gateway: `ChannelParserConfig` (fallback: JSON) |
| Tagger | `TaggerSettingsWindow` | Gateway: `ChannelTaggerConfig` (fallback: JSON) |
| Auto-post | `AutopostSettingsWindow` | Gateway: данные канала |

Все сервисы запускаются с `channelId` в body запроса к Gateway.

> Путь к Python захардкожен в `MainWindow.xaml.cs -> GetPythonExe()`. При переносе на другую машину изменить вручную.

---

### База артов

Галерея изображений из `ArtsRootPath` выбранного канала с метаданными из Gateway.

**Источник данных:**
- Файлы из `New-Images`, `Check-Images`, `Post-Images` подпапок `ArtsRootPath` канала
- Метаданные из Gateway API (`GET /api/data/images?channelId=...`)

**Режимы отображения:**
- **Сетка** — превью плитками
- **Список** — таблица: Имя файла / Персонаж / Опубликован

**Фильтры:** вкладки папок, поиск по имени, сортировка по дате / имени / размеру

**Контекстное меню (ПКМ):** Открыть / Удалить / Пометить опубликованным / Копировать путь

**Очистка:** удалить все видимые / удалить опубликованные

---

### Расписание

Управление расписанием публикаций и правилами постинга.

**Источник данных:** Gateway API (`GET /api/schedule?channelId=...`), fallback: JSON

Столбцы: **Дата / День недели / Время / Изображение / Персонаж / Подпись**

| Действие | Результат |
|---|---|
| Добавить / Изменить / Удалить слот | Редактирование в памяти |
| Сохранить | Запись через Gateway API (с channelId) или JSON |
| Применить правила | Генерация `pending`-слотов по правилам постинга |

#### Панель правил постинга

Нижняя часть вкладки:

| Действие | Результат |
|---|---|
| **+ Добавить время** | Диалог: время, дни недели, подпись |
| Изменить/Удалить правило | Редактирование/удаление |
| Сохранить правила | Запись в файл |

---

## Окна настроек

### ParserSettingsWindow
**Источник:** Gateway `GET /api/channel/{id}/parser-config` (fallback: `data/json/Parser/config.json`)

| Поле | Ключ |
|---|---|
| Путь загрузки | `downloadPath` (только JSON) |
| Хэштеги | `hashtags` |
| Негативные хэштеги (Pixiv) | `negativeHashtags` |
| Изображений на хэштег | `imagesPerHashtag` |
| Задержка прокрутки (мс) | `scrollDelayMs` |
| Задержка загрузки (мс) | `imageLoadDelayMs` |

### TaggerSettingsWindow
**Источник:** Gateway `GET /api/channel/{id}/tagger-config` (fallback: `user_settings.json -> tagger`)

| Поле | Ключ |
|---|---|
| Шаблон переименования | `renameTemplate` |
| Разделитель | `separator` |
| Только новые | `onlyNew` |
| Режим (rename/copy) | `mode` |

### AutopostSettingsWindow
**Источник:** Gateway `GET /api/channel` -> данные канала по ID

| Поле | Описание |
|---|---|
| Ссылка на канал | `link` канала |
| API ID | `apiId` канала |
| API Hash | `apiHash` канала |
| Session file | `sessionFile` канала |
| Bot Token | `botToken` канала |

Изменения синхронизированы с ChannelManagementWindow (один источник данных — Gateway).

### ChannelManagementWindow
Полное управление каналами: создание, редактирование, удаление.

| Функция | Описание |
|---|---|
| Создание канала | Автосоздание дефолтных конфигов парсера/теггера |
| Session file | Browse через OpenFileDialog (.session) |
| Создать папку | Создаёт `{путь}/{Название}-Images/` с подпапками |
| Arts Root Path | Browse через FolderBrowserDialog |
| Удаление | Каскадное удаление конфигов, расписания |

---

## Файлы проекта

| Файл | Описание |
|---|---|
| `WpfApp1.csproj` | SDK-style .NET 8, NuGet: `Newtonsoft.Json 13.0.3` |
| `MainWindow.xaml / .cs` | Главное окно, навигация, channel context |
| `Models.cs` | `ImageItem`, `ScheduleSlot`, `PostTimeEntry`, `LogEntry`, `ChannelSelectorItem`, `ServiceStatus` |
| `Services/GatewayApiClient.cs` | HTTP-клиент к API Gateway (channels, configs, schedule, images) |
| `ViewModels/` | MVVM ViewModels (с поддержкой ChannelId) |
| `ParserSettingsWindow.xaml / .cs` | Per-channel настройки парсера |
| `TaggerSettingsWindow.xaml / .cs` | Per-channel настройки теггера |
| `AutopostSettingsWindow.xaml / .cs` | Telegram-креденшалы канала (из Gateway) |
| `ChannelManagementWindow.xaml / .cs` | CRUD управление каналами |
