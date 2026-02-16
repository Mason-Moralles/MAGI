# JSON — Схемы всех файлов данных

Полная документация по всем JSON-файлам проекта: что хранят, кто читает, кто пишет.

---

## Карта файлов

| Файл | Читает | Пишет |
|---|---|---|
| `data/json/images/images.json` | Auto-post, FilenameTagger, AdminPanel (Gallery) | FilenameTagger, Auto-post |
| `data/json/images/posted_images.json` | Auto-post, AdminPanel (Gallery) | Auto-post, AdminPanel (контекстное меню) |
| `data/json/schedule.json` | Auto-post, AdminPanel (Schedule tab) | Auto-post, AdminPanel (Schedule tab) |
| `data/json/Parser/config.json` | PinterestParser, PixivParser | AdminPanel (ParserSettingsWindow) |
| `data/json/Parser/Pinterest_downloaded_images.json` | PinterestParser | PinterestParser |
| `data/json/Parser/Pixiv_downloaded_images.json` | PixivParser | PixivParser |
| `data/json/FilenameTagger/filename_tags.json` | FilenameTagger | Вручную |
| `data/json/defaults.json` | config_loader.py | Вручную |
| `%APPDATA%\MAGI\user_settings.json` | config_loader, AdminPanel, все окна настроек | AutopostSettingsWindow, TaggerSettingsWindow, AdminPanel (пути) |
| `%APPDATA%\MAGI\posting_rules.json` | Auto-post, AdminPanel (Schedule tab) | AdminPanel (Schedule tab) |

---

## images.json

**Путь:** `data/json/images/images.json`
**Ключ:** имя файла с расширением (`"asuka_langley_0001.jpg"`)

```json
{
  "asuka_langley_0001.jpg": {
    "person": "#Asuka_Langley",
    "posted": 0
  }
}
```

| Поле | Тип | Кто пишет | Описание |
|---|---|---|---|
| `person` | string | FilenameTagger | Хэштег персонажа (`#Name`) |
| `posted` | int (0/1) | — | 0 = ожидает публикации (всегда 0, запись удаляется после планирования) |

**Жизненный цикл записи:**
1. FilenameTagger создаёт запись (`person: "#Tag", posted: 0`)
2. Auto-post берёт арт, **удаляет** запись из файла
3. Запись с деталями публикации переходит в `posted_images.json`

---

## posted_images.json

**Путь:** `data/json/images/posted_images.json`
**Ключ:** имя файла с расширением

```json
{
  "asuka_langley_0001.jpg": {
    "person":    "#Asuka_Langley",
    "posted_at": "2026-02-14T19:59:00+03:00",
    "caption":   "Вечерний арт 🌙"
  }
}
```

| Поле | Тип | Кто пишет | Описание |
|---|---|---|---|
| `person` | string | Auto-post | Хэштег персонажа |
| `posted_at` | string (ISO) | Auto-post | Время публикации со смещением часового пояса |
| `caption` | string | Auto-post | Подпись к посту |

Также AdminPanel через «Пометить опубликованным» может записать:
```json
{
  "asuka_langley_0002.jpg": {
    "posted_at": "2026-02-15 13:00:00",
    "manual":    true
  }
}
```

---

## schedule.json

**Путь:** `data/json/schedule.json`
**Ключ:** ISO datetime со смещением часового пояса

```json
{
  "2026-02-14T19:59:00+03:00": {
    "date":    "2026-02-14",
    "time":    "19:59",
    "status":  "scheduled",
    "file":    "asuka_langley_0001.jpg",
    "person":  "#Asuka_Langley",
    "caption": "Вечерний арт 🌙"
  },
  "2026-02-15T07:29:00+03:00": {
    "date":    "2026-02-15",
    "time":    "07:29",
    "status":  "pending",
    "file":    "",
    "person":  "",
    "caption": ""
  }
}
```

| Поле | Тип | Описание |
|---|---|---|
| `date` | string | Дата `"YYYY-MM-DD"` |
| `time` | string | Время `"HH:MM"` |
| `status` | string | `pending` / `scheduled` / `posted` / `missed` / `error` |
| `file` | string | Имя файла арта (пусто пока `pending`) |
| `person` | string | Хэштег персонажа (пусто пока `pending`) |
| `caption` | string | Подпись поста (пусто пока `pending`) |

### Жизненный цикл слота

```
pending  ──(Auto-post привязывает арт)──►  scheduled  ──(публикация)──►  posted
  │
  └──(время прошло, арт не привязан)──►  missed
```

---

## posting_rules.json

**Путь:** `%APPDATA%\MAGI\posting_rules.json`
**Читают:** Auto-post (config_loader), AdminPanel (вкладка Расписание)
**Пишет:** AdminPanel → вкладка Расписание → «Сохранить правила»

```json
{
  "version": 2,
  "rules": [
    { "time": "07:29", "days": ["Monday","Tuesday","Wednesday","Thursday","Friday"], "caption": "Доброе утро!" },
    { "time": "19:59", "days": ["Monday","Tuesday","Wednesday","Thursday","Friday","Saturday","Sunday"], "caption": "" },
    { "time": "08:59", "days": ["Saturday"], "caption": "Шабат шалом!" },
    { "time": "08:59", "days": ["Sunday"],   "caption": "Доброе утро!" }
  ]
}
```

| Поле | Тип | Описание |
|---|---|---|
| `version` | int | Версия формата (текущая: 2) |
| `rules[].time` | string | Время публикации `"HH:MM"` |
| `rules[].days` | array[string] | Дни недели на английском (Monday … Sunday) |
| `rules[].caption` | string | Подпись к посту (может быть пустой) |

> Одно и то же время может встречаться в нескольких правилах с разными днями — для задания разных подписей.

---

## Parser/config.json

**Путь:** `data/json/Parser/config.json`
**Читают:** PinterestParser.py, PixivParser.py
**Пишет:** AdminPanel → ParserSettingsWindow

```json
{
  "hashtags":          ["綾波レイ", "asuka langley"],
  "negativeHashtags":  ["AI-generated"],
  "imagesPerHashtag":  2,
  "downloadPath":      "D:\\MAGI-Images\\New-Images",
  "databasePath":      "D:\\MAGI\\data\\json\\parser\\Pinterest_downloaded_images.json",
  "scrollDelayMs":     2000,
  "imageLoadDelayMs":  1000
}
```

| Поле | Тип | Описание |
|---|---|---|
| `hashtags` | array[string] | Поисковые хэштеги |
| `negativeHashtags` | array[string] | Только Pixiv: арты с этими тегами пропускаются |
| `imagesPerHashtag` | int | Максимум скачиваний на хэштег за сессию |
| `downloadPath` | string | Абсолютный путь куда скачивать |
| `scrollDelayMs` | int | Задержка между прокрутками страницы (мс) |
| `imageLoadDelayMs` | int | Задержка между скачиваниями (мс) |

---

## Parser/Pinterest_downloaded_images.json

**Читает/Пишет:** PinterestParser.py

```json
[
  {
    "pinUrl":        "https://www.pinterest.com/pin/12345/",
    "imageUrl":      "https://i.pinimg.com/.../img.jpg",
    "fileName":      "asuka_langley_0001.jpg",
    "hashtag":       "asuka langley",
    "downloadedAt":  "2026-02-14T06:05:37.431230"
  }
]
```

---

## Parser/Pixiv_downloaded_images.json

**Читает/Пишет:** PixivParser.py

```json
[
  {
    "artworkUrl":   "https://www.pixiv.net/en/artworks/12345678",
    "imageUrl":     "https://i.pximg.net/img-original/...",
    "fileName":     "asuka_langley_0001.jpg",
    "hashtag":      "asuka langley",
    "downloadedAt": "2026-02-15T13:25:20"
  }
]
```

Для пропущенных (негативный тег):
```json
{
  "artworkUrl":   "https://www.pixiv.net/en/artworks/140987963",
  "imageUrl":     "",
  "fileName":     "_skipped_",
  "hashtag":      "asuka langley",
  "downloadedAt": "2026-02-15T13:25:20"
}
```

---

## FilenameTagger/filename_tags.json

**Читает:** FilenameTagger.py
**Пишет:** вручную

```json
{
  "asuka":     "#Asuka_Langley",
  "langley":   "#Asuka_Langley",
  "rei":       "#Rei_Ayanami",
  "ayanami":   "#Rei_Ayanami",
  "綾波レイ":   "#Rei_Ayanami",
  "misato":    "#Misato_Katsuragi",
  "katsuragi": "#Misato_Katsuragi",
  "ritsuko":   "#Ritsuko_Akagi",
  "akagi":     "#Ritsuko_Akagi",
  "shinji":    "#Shinji_Ikari",
  "gendo":     "#Gendo_Ikari",
  "mari":      "#Mari_Makinami",
  "makinami":  "#Mari_Makinami"
}
```

---

## %APPDATA%\MAGI\user_settings.json

**Читают:** все Python-скрипты (через config_loader), AdminPanel (все окна настроек)
**Пишут:** AdminPanel → AutopostSettingsWindow, TaggerSettingsWindow, BrowseArtsPath

```json
{
  "version": 1,
  "paths": {
    "project_root": "D:\\MAGI",
    "arts_root":    "D:\\MAGI-Images"
  },
  "telegram": {
    "channel_link": "@magi_test",
    "api_id":       20860465,
    "api_hash":     "fa751154c5d459169d7fa49bd193cb88",
    "session_file": "session",
    "bot_token":    ""
  },
  "db": {
    "images_json":        "data/json/images/images.json",
    "posted_images_json": "data/json/images/posted_images.json",
    "schedule_json":      "data/json/schedule.json"
  },
  "schedule": {
    "time_zone":     "Europe/Moscow",
    "schedule_days": 5
  },
  "tagger": {
    "rename_template": "{artist}_{title}_{id}",
    "separator":       "_",
    "only_new":        true,
    "mode":            "rename"
  },
  "parser": {
    "source_path": "C:\\Users\\Георгий\\Downloads",
    "dest_path":   "",
    "extensions":  ".jpg .png .webp"
  }
}
```

| Секция | Кто читает | Где редактируется |
|---|---|---|
| `paths.project_root` | config_loader, AdminPanel | Захардкожено / при первом запуске |
| `paths.arts_root` | config_loader, AdminPanel (Gallery) | AdminPanel → кнопка 📁 |
| `telegram.*` | Auto-post (config_loader) | AdminPanel → ⚙ Auto-post |
| `db.*` | config_loader, AdminPanel (GetJsonDbPath) | Вручную в файле |
| `schedule.time_zone` | Auto-post | AdminPanel → вкладка Расписание |
| `schedule.schedule_days` | Auto-post | AdminPanel → вкладка Расписание |
| `tagger.*` | Не используется FilenameTagger.py | AdminPanel → ⚙ Tagger |
| `parser.*` | Не используется парсерами | AdminPanel → ⚙ Parser (не сохраняется) |
