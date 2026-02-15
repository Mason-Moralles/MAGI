# MAGI — Admin Panel & Microservices

Система для автоматического парсинга артов, их тегирования и публикации в Telegram-канал.

---

## Структура проекта

```
D:\MAGI\
├── AdmPanel\WpfApp1\          # GUI-панель управления (WPF C#)
├── Parser\                    # Микросервис парсинга (Python)
├── FilenameTagger\            # Микросервис тегирования (Python)
├── Auto-post\                 # Микросервис публикации (Python)
├── config\                    # Общий загрузчик конфига (Python)
└── data\
    └── json\
        ├── images\
        │   ├── images.json                        # БД артов (основная)
        │   └── posted_images.json                 # БД опубликованных артов
        ├── Parser\
        │   ├── config.json                        # Конфиг парсера
        │   ├── Pinterest_downloaded_images.json   # Уже скачанные пины
        │   └── Pixiv_downloaded_images.json       # Уже скачанные арты Pixiv
        ├── FilenameTagger\
        │   └── filename_tags.json                 # Маппинг ключевых слов → хэштеги
        ├── schedule.json                          # Расписание публикаций
        └── defaults.json                          # Дефолтные пути папок артов
```

Пользовательские настройки хранятся вне проекта:
```
%APPDATA%\MAGI\
├── user_settings.json     # Пути проекта, Telegram API, расписание, пути к JSON БД
└── posting_rules.json     # Правила постинга: дни недели, время, подписи
```

---

## Поток данных между микросервисами

```
Pinterest / Pixiv
        │
        ▼
   ┌─────────┐
   │  Parser  │  ──► New-Images\  (скачанные файлы)
   └─────────┘  ──► Parser/Pinterest_downloaded_images.json
                ──► Parser/Pixiv_downloaded_images.json
        │
        ▼
┌────────────────┐
│ FilenameTagger │  читает: New-Images\, FilenameTagger/filename_tags.json
└────────────────┘  пишет:  images/images.json  { person, posted:0, caption:"" }
                    перемещает: New-Images → Check-Images
        │
        ▼
┌───────────┐
│ Auto-post │  читает: images/images.json, posted_images.json,
└───────────┘           schedule.json, user_settings.json, posting_rules.json
                пишет:  images/images.json  (posted:1, post_time, caption)
                        images/posted_images.json
                        schedule.json
                перемещает: Check-Images → Post-Images
                публикует → Telegram Channel
```

---

## Папки с изображениями (arts_root = D:\MAGI-Images)

| Папка | Назначение |
|---|---|
| `New-Images\` | Свежескачанные парсером арты |
| `Check-Images\` | Арты после тегирования, готовые к публикации |
| `Post-Images\` | Архив опубликованных артов |

---

## Подробная документация

| Файл | Описание |
|---|---|
| [docs/README-Parser.md](docs/README-Parser.md) | Микросервис парсинга |
| [docs/README-FilenameTagger.md](docs/README-FilenameTagger.md) | Микросервис тегирования |
| [docs/README-Autopost.md](docs/README-Autopost.md) | Микросервис публикации |
| [docs/README-AdminPanel.md](docs/README-AdminPanel.md) | Панель управления |
| [docs/README-JSON.md](docs/README-JSON.md) | Схемы всех JSON файлов |
