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
    "person":    "#Asuka_Langley",
    "posted":    0,
    "post_time": null,
    "caption":   ""
  }
}
```

| Поле | Тип | Кто пишет | Описание |
|---|---|---|---|
| `person` | string | FilenameTagger | Хэштег персонажа (`#Name`) |
| `posted` | int (0/1) | Auto-post | 0 = ожидает, 1 = запланировано |
| `post_time` | string/null | Auto-post | ISO datetime публикации |
| `caption` | string | Auto-post | Подпись к посту |

**Жизненный цикл записи:**
1. FilenameTagger создаёт запись (`posted: 0, post_time: null, caption: ""`)
2. Auto-post обновляет (`posted: 1, post_time: "...", caption: "..."`) и **удаляет из файла**
3. Запись переходит в `posted_images.json`

---

## posted_images.json

**Путь:** `data/json/images/posted_images.json`
**Ключ:** имя файла с расширением

```json
{
  "asuka_langley_0001.jpg": {
    "person":    "#Asuka_Langley",
    "posted":    1,
    "post_time": "2026-02-14T19:59:00+03:00",
    "caption":   "Обедай с вайфу ☀️"
  }
}
```

Структура идентична `images.json`, но здесь только опубликованные записи (`posted: 1`).

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
    "file":    "asuka_langley_0001.jpg",
    "caption": "Обедай с вайфу ☀️",
    "person":  "#Asuka_Langley"
  }
}
```

| Поле | Тип | Описание |
|---|---|---|
| `file` | string | Имя файла арта |
| `caption` | string | Подпись поста |
| `person` | string | Хэштег персонажа |

> **Примечание:** AdminPanel на вкладке «Расписание» хранит слоты в другом формате (с полями `date`, `time`, `image`, `status`, `tags`, `repeat`). Это отдельная структура для UI — Auto-post читает только ISO-datetime формат выше.

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
| `databasePath` | string | Путь к БД скачанных (устаревший, парсеры используют свои пути) |
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

---

## %APPDATA%\MAGI\posting_rules.json

**Читают:** Auto-post (config_loader), AdminPanel (вкладка Расписание)
**Пишет:** AdminPanel → вкладка Расписание → «Сохранить правила»

```json
{
  "version": 1,
  "week_template": {
    "Monday":    ["13:00", "19:00"],
    "Wednesday": ["13:00"],
    "Friday":    ["21:00"]
  },
  "captions_by_time": {
    "13:00": "Обедай с вайфу ☀️"
  },
  "forced_posts": [
    { "day": "Monday", "time": "13:00", "tag": "#Rei_Ayanami" }
  ],
  "forced_captions": [
    { "day": "Friday", "time": "21:00", "caption": "Пятница! 🎉" }
  ]
}
```

| Поле | Описание |
|---|---|
| `week_template` | Дни и времена публикаций |
| `captions_by_time` | Подпись по умолчанию для конкретного времени |
| `forced_posts` | Принудительный персонаж в конкретный день/время |
| `forced_captions` | Принудительная подпись в конкретный день/время |
