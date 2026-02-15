# MAGI

Система автоматического парсинга аниме-артов с Pinterest и Pixiv, их тегирования и публикации в Telegram-канал по расписанию.

---

## Стек

| Компонент | Технология |
|---|---|
| Панель управления | C# / WPF / .NET 8 |
| Микросервисы | Python 3.13 |
| Браузерная автоматизация | Selenium (Chrome) |
| Telegram API | Telethon |

---

## Структура

```
D:\MAGI\
├── AdmPanel\WpfApp1\     # GUI-панель управления
├── Parser\               # Парсинг Pinterest и Pixiv
├── FilenameTagger\       # Тегирование по имени файла
├── Auto-post\            # Публикация в Telegram
├── config\               # Общий загрузчик конфига
└── data\json\            # JSON базы данных
```

Пользовательские настройки: `%APPDATA%\MAGI\user_settings.json`

---

## Поток данных

```
[Parser]  ──скачивает──►  New-Images\
               │
               ▼
[FilenameTagger]  ──тегирует──►  images.json  ──перемещает──►  Check-Images\
               │
               ▼
[Auto-post]  ──публикует──►  Telegram  ──архивирует──►  Post-Images\
```

---

## Сборка Admin Panel

```bash
dotnet build AdmPanel/WpfApp1/WpfApp1.csproj
# exe: AdmPanel/WpfApp1/bin/Debug/net8.0-windows/MAGIAdmin.exe
```

---

## Документация

| | |
|---|---|
| [Admin Panel](docs/README-AdminPanel.md) | Вкладки, окна настроек, сборка |
| [Parser](docs/README-Parser.md) | Pinterest и Pixiv, негативные теги, конфиг |
| [FilenameTagger](docs/README-FilenameTagger.md) | Маппинг тегов, алгоритм тегирования |
| [Auto-post](docs/README-Autopost.md) | Расписание, правила постинга, Telegram |
| [JSON схемы](docs/README-JSON.md) | Все файлы данных: структура, кто читает, кто пишет |
