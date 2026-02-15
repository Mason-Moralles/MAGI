# Admin Panel — Панель управления

WPF-приложение (.NET 8) для управления всеми микросервисами MAGI.

**Сборка:**
```bash
dotnet build AdmPanel/WpfApp1/WpfApp1.csproj
# exe: AdmPanel/WpfApp1/bin/Debug/net8.0-windows/MAGIAdmin.exe
```

---

## Вкладки

### ⚙ Микросервисы

Запуск и остановка Python-скриптов. Вывод (stdout/stderr) отображается в консоли логов.

| Сервис | Скрипт | Настройки (кнопка ⚙) | Читает конфиг |
|---|---|---|---|
| Parser Pinterest | `Parser/PinterestParser.py` | `ParserSettingsWindow` | `data/json/Parser/config.json` |
| Parser Pixiv | `Parser/PixivParser.py` | `ParserSettingsWindow` | `data/json/Parser/config.json` |
| Tagger | `FilenameTagger/FilenameTagger.py` | `TaggerSettingsWindow` | `user_settings.json["tagger"]` |
| Auto-post | `Auto-post/Auto-post.py` | `AutopostSettingsWindow` | `user_settings.json["telegram"]` |

> Путь к Python захардкожен в `MainWindow.xaml.cs → GetPythonExe()`. При переносе на другую машину изменить вручную.

---

### 📁 База артов

Галерея всех изображений из `arts_root` с метаданными.

**Читает:**
- Файлы из `New-Images`, `Check-Images`, `Post-Images` и других подпапок `arts_root`
- `images.json` → поля `person` (колонка «Персонаж») и `caption` (колонка «Подпись»)
- `posted_images.json` → метка «Опубликован»

**Режимы отображения:**
- **⊞ Сетка** — превью плитками
- **☰ Список** — таблица: Имя файла / Персонаж / Подпись / Опубликован

**Фильтры:** вкладки папок, поиск по имени, сортировка по дате / имени / размеру

**Контекстное меню (ПКМ на изображении):** Открыть · Удалить · Пометить опубликованным · Копировать путь

**Очистка:** удалить все видимые / удалить опубликованные

---

### 📅 Расписание

Управление расписанием публикаций и правилами постинга.

**Читает/пишет:** `data/json/schedule.json`, `%APPDATA%\MAGI\posting_rules.json`

| Действие | Результат |
|---|---|
| Добавить / Изменить / Удалить слот | Редактирование `schedule.json` в памяти |
| Сохранить | Запись `schedule.json` на диск |
| Применить правила | Генерация слотов по `posting_rules.json` |
| Сохранить правила | Запись `posting_rules.json` (дни, времена) |

> Часовой пояс и «Планировать дней» сохраняются в `user_settings.json["schedule"]` со страницы Расписание — **не** в окне AutopostSettings.

---

## Окна настроек

### ParserSettingsWindow
**Файл:** `data/json/Parser/config.json`

| Поле | JSON-ключ |
|---|---|
| Путь загрузки | `downloadPath` |
| Хэштеги | `hashtags` |
| Негативные хэштеги (только Pixiv) | `negativeHashtags` |
| Изображений на хэштег | `imagesPerHashtag` |
| Задержка прокрутки (мс) | `scrollDelayMs` |
| Задержка загрузки (мс) | `imageLoadDelayMs` |

### TaggerSettingsWindow
**Файл:** `%APPDATA%\MAGI\user_settings.json` → секция `tagger`

| Поле | JSON-ключ |
|---|---|
| Шаблон переименования | `tagger.rename_template` |
| Разделитель | `tagger.separator` |
| Только новые | `tagger.only_new` |
| Режим (rename/copy) | `tagger.mode` |

> Эти поля в текущей версии `FilenameTagger.py` не используются — он работает только через `filename_tags.json`.

### AutopostSettingsWindow
**Файл:** `%APPDATA%\MAGI\user_settings.json` → секция `telegram`

| Поле | JSON-ключ |
|---|---|
| Ссылка на канал | `telegram.channel_link` |
| API ID | `telegram.api_id` |
| API Hash | `telegram.api_hash` |
| Session file | `telegram.session_file` |
| Bot Token | `telegram.bot_token` |

---

## Файлы проекта

| Файл | Описание |
|---|---|
| `WpfApp1.csproj` | SDK-style .NET 8, NuGet: `Newtonsoft.Json 13.0.3` |
| `MainWindow.xaml / .cs` | Главное окно, навигация, запуск процессов |
| `Models.cs` | `ImageItem`, `ScheduleSlot`, `LogEntry` |
| `ParserSettingsWindow.xaml / .cs` | Настройки парсера |
| `TaggerSettingsWindow.xaml / .cs` | Настройки теггера |
| `AutopostSettingsWindow.xaml / .cs` | Настройки Telegram |
