# Auto-post — Микросервис публикации

Автоматически планирует публикацию артов в Telegram-канал по расписанию.

---

## Файлы

| Файл | Описание |
|---|---|
| `Auto-post/Auto-post.py` | Основной скрипт |
| `config/config_loader.py` | Импортируется для загрузки конфига |

---

## Запуск

Запускается из AdminPanel (вкладка **Микросервисы**, кнопка **START** у блока **Auto-post**).
Можно выбрать режим: 🔘 Личный (user API) / 🔘 Бот (bot_token).

---

## Что читает

| Файл | Что берёт |
|---|---|
| `%APPDATA%\MAGI\user_settings.json` | `telegram.*` (api_id, api_hash, bot_token, channel_link, session_file), `schedule.time_zone`, `schedule.schedule_days`, пути к JSON БД |
| `%APPDATA%\MAGI\posting_rules.json` | `week_template` (дни/время), `captions_by_time`, `forced_posts`, `forced_captions` |
| `data/json/images/images.json` | Список неопубликованных артов (`posted == 0`) с `person` и `caption` |
| `data/json/images/posted_images.json` | Уже опубликованные арты (чтобы не повторяться) |
| `data/json/schedule.json` | Уже запланированные слоты (не создавать дубли) |
| `{arts_root}/Check-Images/{file}` | Файл арта для отправки |

## Что пишет

| Файл | Что записывает |
|---|---|
| `data/json/images/images.json` | Обновляет запись: `posted: 1`, `post_time: "ISO datetime"`, `caption: "..."`, затем **удаляет** запись из файла |
| `data/json/images/posted_images.json` | Добавляет запись опубликованного арта |
| `data/json/schedule.json` | Добавляет слот: `{ "file", "caption", "person" }` по ключу ISO datetime |

## Что перемещает

| Откуда | Куда |
|---|---|
| `{arts_root}/Check-Images/{file}` | `{arts_root}/Post-Images/{file}` |

---

## Логика работы

1. Загрузить конфиг (`user_settings.json` + `posting_rules.json`)
2. Загрузить `images.json`, `schedule.json`, `posted_images.json`
3. **Создать временны́е слоты** на `schedule_days` вперёд по шаблону `week_template`:
   - Пропустить прошедшие и уже запланированные слоты
   - Применить `forced_captions` и `captions_by_time`
4. Для каждого слота **выбрать арт** из `images.json`:
   - Приоритет: `forced_posts` (принудительный персонаж на конкретный день/время)
   - Иначе: первый неопубликованный арт, персонаж которого отличается от предыдущего
5. Запланировать публикацию в Telegram через `client.send_file(..., schedule=slot)`
6. Обновить все три JSON БД, переместить файл в `Post-Images`
7. Ждать `DELAY_BETWEEN_POST_SEC` (5 сек) между постами

---

## schedule.json — схема

Ключ — ISO datetime со смещением часового пояса.

```json
{
  "2026-02-14T19:59:00+03:00": {
    "file": "asuka_langley_0001.jpg",
    "caption": "",
    "person": "#Asuka_Langley"
  }
}
```

---

## posting_rules.json — схема

```json
{
  "version": 1,
  "week_template": {
    "Monday":    ["13:00", "19:00"],
    "Wednesday": ["13:00"],
    "Friday":    ["21:00"]
  },
  "captions_by_time": {
    "13:00": "Обедай с вайфу ☀️",
    "19:00": "Вечерний арт 🌙"
  },
  "forced_posts": [
    { "day": "Monday", "time": "13:00", "tag": "#Rei_Ayanami" }
  ],
  "forced_captions": [
    { "day": "Friday", "time": "21:00", "caption": "Пятница! 🎉" }
  ]
}
```

---

## Настройка через AdminPanel

### Вкладка «Расписание»
Прямое редактирование слотов `schedule.json`:
- Добавить слот / Удалить слот / Изменить дату, время, изображение, подпись
- Кнопка **Применить правила** — генерирует слоты автоматически по `posting_rules.json`
- Кнопка **Сохранить** — записывает `schedule.json`

Настройка дней недели и времени постинга (пишет в `posting_rules.json`):
- Чекбоксы Пн–Вс
- Список времён (Add time)
- Кнопка **Сохранить правила**

### Вкладка «Микросервисы» → ⚙ Auto-post → `AutopostSettingsWindow`

| Поле в окне | Ключ в user_settings.json | Описание |
|---|---|---|
| Ссылка на канал | `telegram.channel_link` | `@username` или `-100xxxxxxx` |
| API ID | `telegram.api_id` | Из my.telegram.org |
| API Hash | `telegram.api_hash` | Из my.telegram.org |
| Session file | `telegram.session_file` | Имя `.session` файла (хранится в `%APPDATA%\MAGI\`) |
| Bot Token | `telegram.bot_token` | Если режим «Бот» (от @BotFather) |

> **Часовой пояс** и **Планировать дней** редактируются **только на вкладке «Расписание»** главного окна.

---

## Зависимости Python

```
telethon      # Telegram MTProto клиент
pytz          # Часовые пояса
```
