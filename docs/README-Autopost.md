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
| `%APPDATA%\MAGI\posting_rules.json` | `rules[]` — массив правил (time, days[], caption) |
| `data/json/images/images.json` | Список неопубликованных артов (`posted == 0`) с полем `person` |
| `data/json/images/posted_images.json` | Уже опубликованные арты (чтобы не повторяться) |
| `data/json/schedule.json` | Слоты со статусом `pending` — к ним привязываются арты |
| `{arts_root}/Check-Images/{file}` | Файл арта для отправки |

## Что пишет

| Файл | Что записывает |
|---|---|
| `data/json/images/images.json` | **Удаляет** запись арта после планирования |
| `data/json/images/posted_images.json` | Добавляет запись: `{ "person", "posted_at" (ISO), "caption" }` |
| `data/json/schedule.json` | Обновляет слот: `status` → `"scheduled"`, добавляет `file`, `person`, `caption` |

## Что перемещает

| Откуда | Куда |
|---|---|
| `{arts_root}/Check-Images/{file}` | `{arts_root}/Post-Images/{file}` |

---

## Логика работы

1. Загрузить конфиг (`user_settings.json` + `posting_rules.json`)
2. Загрузить `images.json`, `schedule.json`, `posted_images.json`
3. **Найти `pending`-слоты** в `schedule.json`
   - Если слотов нет → сгенерировать их из `posting_rules.json` на `schedule_days` вперёд
   - Прошедшие слоты пометить `"missed"`, не трогать `"scheduled"`/`"posted"` слоты
4. Для каждого `pending`-слота **выбрать арт** из `images.json`:
   - Приоритет: арт с `forced_tag` (если задан в правиле)
   - Иначе: первый неопубликованный арт, персонаж которого отличается от предыдущего
   - Фолбэк: если все оставшиеся арты одного персонажа — взять любой
5. Подпись берётся из поля `caption` правила, совпавшего с днём и временем слота
6. Запланировать публикацию в Telegram через `client.send_file(..., schedule=slot_dt)`
7. Обновить JSON БД:
   - `schedule.json`: `status="scheduled"`, `file`, `person`, `caption`
   - `images.json`: удалить запись арта
   - `posted_images.json`: добавить запись
   - Переместить файл в `Post-Images`
8. Ждать `DELAY_BETWEEN_POST_SEC` (5 сек) между постами

---

## schedule.json — схема (v2)

Ключ — ISO datetime со смещением часового пояса.

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

### Значения статуса

| Статус | Описание |
|---|---|
| `pending` | Слот создан, арт ещё не привязан |
| `scheduled` | Арт привязан, публикация запланирована в Telegram |
| `posted` | Опубликовано (выставляется вручную или в будущих версиях) |
| `missed` | Время слота прошло, арт не был привязан |
| `error` | Ошибка при отправке в Telegram |

---

## posting_rules.json — схема (v2)

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

> Одно и то же время может встречаться в нескольких правилах с разными днями — это позволяет задавать разные подписи для одного времени в разные дни недели.

---

## Настройка через AdminPanel

### Вкладка «Расписание»
Прямое редактирование слотов `schedule.json`:
- Добавить / Удалить слот, изменить дату, время, изображение, подпись
- Кнопка **Применить правила** — генерирует `pending`-слоты по `posting_rules.json`
- Кнопка **Сохранить** — записывает `schedule.json` на диск

Таблица слотов содержит колонки: **Дата · День недели · Время · Изображение · Персонаж · Подпись**

Настройка правил постинга (пишет в `posting_rules.json`):
- Правила редактируются в нижней панели «Правила постинга»
- Кнопка **+ Добавить время** — создаёт новое правило (время + дни + подпись)
- Кнопка **Сохранить правила**

Параметры расписания (пишет в `user_settings.json`):
- **Часовой пояс** (`schedule.time_zone`)
- **Планировать дней** (`schedule.schedule_days`)

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
