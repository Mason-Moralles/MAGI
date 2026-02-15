# Admin Panel — Панель управления (WPF C#)

GUI-приложение для управления всеми микросервисами MAGI.

---

## Файлы

| Файл | Описание |
|---|---|
| `AdmPanel\WpfApp1\MainWindow.xaml` | Главное окно (XAML разметка) |
| `AdmPanel\WpfApp1\MainWindow.xaml.cs` | Code-behind главного окна |
| `AdmPanel\WpfApp1\Models.cs` | Модели данных: `ImageItem`, `ScheduleSlot`, `LogEntry` |
| `AdmPanel\WpfApp1\ParserSettingsWindow.xaml` | Окно настроек парсера (XAML) |
| `AdmPanel\WpfApp1\ParserSettingsWindow.xaml.cs` | Code-behind настроек парсера |
| `AdmPanel\WpfApp1\TaggerSettingsWindow.xaml` | Окно настроек теггера (XAML) |
| `AdmPanel\WpfApp1\TaggerSettingsWindow.xaml.cs` | Code-behind настроек теггера |
| `AdmPanel\WpfApp1\AutopostSettingsWindow.xaml` | Окно настроек автопостинга (XAML) |
| `AdmPanel\WpfApp1\AutopostSettingsWindow.xaml.cs` | Code-behind настроек автопостинга |

---

## Вкладки главного окна

### Вкладка 0: Микросервисы

Три блока-карточки: **Parser**, **Tagger**, **Auto-post**.

Каждый блок содержит:
- Статус (серый/зелёный индикатор + текст `Status: Stopped / Running`)
- Кнопку ⚙ (открывает окно настроек)
- Кнопку `START / STOP`

**Что читает при старте каждого сервиса:**

| Сервис | Запускает Python-скрипт |
|---|---|
| Parser (Pinterest) | `Parser/PinterestParser.py` |
| Parser (Pixiv) | `Parser/PixivParser.py` |
| Tagger | `FilenameTagger/FilenameTagger.py` |
| Auto-post | `Auto-post/Auto-post.py` |

Вывод Python-скриптов (stdout/stderr) отображается в **Консоли логов** в нижней части вкладки.

---

### Вкладка 1: База артов

Отображает все изображения из `arts_root` с метаданными из `images.json`.

**Что читает при открытии вкладки (`LoadArtsGallery`):**

| Источник | Ключ в user_settings.json | Что берёт |
|---|---|---|
| `arts_root` (папки) | `paths.arts_root` | Сканирует `New-Images`, `Check-Images`, `Post-Images` и другие подпапки |
| `data/json/images/images.json` | `db.images_json` | `person` → колонка «Персонаж», `caption` → колонка «Подпись» |
| `data/json/images/posted_images.json` | `db.posted_images_json` | Метка «Опубликован» |

**Режимы отображения:**
- **⊞ Сетка** — превью изображений плитками
- **☰ Список** — таблица с колонками:

| Колонка | Источник поля | JSON-ключ |
|---|---|---|
| Имя файла | `ImageItem.FileName` | — |
| Персонаж | `ImageItem.Tags` | `images.json[filename]["person"]` |
| Подпись | `ImageItem.Caption` | `images.json[filename]["caption"]` |
| Опубликован | `ImageItem.StatusText` | `posted_images.json[filename]` (есть/нет) |

**Фильтрация и сортировка:**
- Вкладки папок (Все / New-Images / Check-Images / Post-Images / Корень)
- Поиск по имени файла
- Сортировка: по дате / по имени / по размеру

**Контекстное меню (правая кнопка на изображении):**
- Открыть — открывает файл через системный просмотрщик
- Удалить — удаляет файл с диска
- Пометить опубликованным — записывает в `posted_images.json`
- Копировать путь — в буфер обмена

**Кнопки очистки:**
- Удалить все — удаляет все видимые файлы
- Удалить опубликованные — удаляет только с `IsPublished == true`

---

### Вкладка 2: Расписание

Управление расписанием публикаций.

**Что читает при открытии:**

| Источник | Ключ в user_settings.json | Что берёт |
|---|---|---|
| `data/json/schedule.json` | `db.schedule_json` | Все слоты расписания |
| `%APPDATA%\MAGI\posting_rules.json` | — | Дни недели, времена, правила |

**Таблица слотов (ScheduleDataGrid):**

| Колонка | Источник | JSON-ключ |
|---|---|---|
| Дата | `ScheduleSlot.Date` | `schedule.json[slot]["date"]` |
| Время | `ScheduleSlot.Time` | `schedule.json[slot]["time"]` |
| Изображение | `ScheduleSlot.ImageName` | `schedule.json[slot]["image"]` |
| Статус | `ScheduleSlot.StatusText` | `schedule.json[slot]["status"]` |

**Действия:**
- **Добавить слот** — создаёт новую пустую запись
- **Изменить** (кнопка в строке) — открывает правую панель редактирования
- **Удалить** (кнопка в строке) — удаляет слот из памяти
- **Сохранить** — записывает `schedule.json`
- **Применить правила** — генерирует слоты по `posting_rules.json`, пишет `schedule.json`
- **Сохранить правила** — записывает `posting_rules.json`

**Секция правил постинга (левая часть):**

| Элемент UI | Записывает в |
|---|---|
| Чекбоксы Пн–Вс | `posting_rules.json["week_template"]` (ключи дней) |
| Список времён | `posting_rules.json["week_template"][day]` (массив времён) |
| Кнопка Add time | Добавляет время в список |

---

## Окна настроек

### ParserSettingsWindow — настройки парсера

**Читает:** `data/json/Parser/config.json`
**Пишет:** `data/json/Parser/config.json`

| Поле | JSON-ключ |
|---|---|
| Путь загрузки | `downloadPath` |
| Хэштеги | `hashtags` (массив) |
| Негативные хэштеги (Pixiv) | `negativeHashtags` (массив) |
| Изображений на хэштег | `imagesPerHashtag` |
| Задержка прокрутки (мс) | `scrollDelayMs` |
| Задержка загрузки (мс) | `imageLoadDelayMs` |

---

### TaggerSettingsWindow — настройки теггера

**Читает:** `%APPDATA%\MAGI\user_settings.json`
**Пишет:** `%APPDATA%\MAGI\user_settings.json`

| Поле | JSON-ключ |
|---|---|
| Шаблон переименования | `tagger.rename_template` |
| Разделитель | `tagger.separator` |
| Только новые | `tagger.only_new` |
| Режим (rename/copy) | `tagger.mode` |

---

### AutopostSettingsWindow — настройки Telegram

**Читает:** `%APPDATA%\MAGI\user_settings.json`
**Пишет:** `%APPDATA%\MAGI\user_settings.json` (только секция `telegram`)

| Поле | JSON-ключ |
|---|---|
| Ссылка на канал | `telegram.channel_link` |
| API ID | `telegram.api_id` |
| API Hash | `telegram.api_hash` |
| Session file | `telegram.session_file` |
| Bot Token | `telegram.bot_token` |

> Часовой пояс и «Планировать дней» редактируются **только на вкладке «Расписание»** (сохраняются в `user_settings.json["schedule"]`).

---

## Где хранится путь Python

В `MainWindow.xaml.cs`, метод `GetPythonExe()`:
```csharp
return @"C:\Users\Георгий\AppData\Local\Programs\Python\Python313\python.exe";
```
Захардкожено. При переносе на другую машину нужно изменить вручную.
