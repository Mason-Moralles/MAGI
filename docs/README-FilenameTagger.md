# FilenameTagger — Микросервис тегирования

Сканирует папку `New-Images`, по ключевым словам в имени файла определяет персонажа,
записывает метаданные в `images.json` и перемещает файл в `Check-Images`.

---

## Файлы

| Файл | Описание |
|---|---|
| `FilenameTagger/FilenameTagger.py` | Основной скрипт |
| `config/config_loader.py` | Импортируется для загрузки путей |

---

## Запуск

Запускается из AdminPanel (вкладка **Микросервисы**, кнопка **START** у блока **Tagger**).

---

## Что читает

| Файл | Что берёт |
|---|---|
| `%APPDATA%\MAGI\user_settings.json` | `paths.project_root`, `paths.arts_root` → для построения путей |
| `data/json/FilenameTagger/filename_tags.json` | Маппинг: ключевое слово в имени файла → хэштег персонажа |
| `data/json/images/images.json` | Существующие записи (чтобы не перезаписывать уже обработанные) |
| `{arts_root}/New-Images/*.jpg/.png/.jpeg` | Файлы для обработки |

## Что пишет

| Файл | Что записывает |
|---|---|
| `data/json/images/images.json` | Новые записи: `{ "person": "#Tag", "posted": 0, "post_time": null, "caption": "" }` |

## Что перемещает

| Откуда | Куда |
|---|---|
| `{arts_root}/New-Images/{file}` | `{arts_root}/Check-Images/{file}` |

---

## Логика работы

1. Загрузить конфиг → определить пути `new_images_dir`, `check_images_dir`, `images_json`
2. Загрузить `filename_tags.json` (маппинг ключевых слов)
3. Загрузить существующий `images.json`
4. Для каждого файла в `New-Images`:
   - Пропустить не-изображения
   - Если файл уже есть в `images.json` с заполненным `person` → пропустить
   - Перебрать ключевые слова из `filename_tags.json`
   - Если ключевое слово найдено в имени файла → записать запись в `images.json`
   - Переместить файл `New-Images → Check-Images`
5. Сохранить обновлённый `images.json`

> **Важно:** если ни одно ключевое слово не совпало — файл остаётся в `New-Images` и не попадает в `images.json`. Нужно добавить нужное слово в `filename_tags.json`.

---

## filename_tags.json — схема и содержимое

Ключ — подстрока в нижнем регистре имени файла. Значение — хэштег персонажа.

```json
{
  "shinji":    "#Shinji_Ikari",
  "gendo":     "#Gendo_Ikari",
  "綾波レイ":   "#Rei_Ayanami",
  "rei":       "#Rei_Ayanami",
  "ayanami":   "#Rei_Ayanami",
  "asuka":     "#Asuka_Langley",
  "langley":   "#Asuka_Langley",
  "misato":    "#Misato_Katsuragi",
  "katsuragi": "#Misato_Katsuragi",
  "ritsuko":   "#Ritsuko_Akagi",
  "akagi":     "#Ritsuko_Akagi",
  "mari":      "#Mari_Makinami",
  "makinami":  "#Mari_Makinami"
}
```

> Файл редактируется **вручную** — в AdminPanel нет отдельного окна для этого.

---

## images.json — запись после FilenameTagger

```json
{
  "asuka_langley_0001.jpg": {
    "person": "#Asuka_Langley",
    "posted": 0,
    "post_time": null,
    "caption": ""
  }
}
```

| Поле | Тип | Кто пишет | Описание |
|---|---|---|---|
| `person` | string | FilenameTagger | Хэштег персонажа из filename_tags.json |
| `posted` | int (0/1) | Auto-post | 0 = не опубликовано, 1 = опубликовано |
| `post_time` | string/null | Auto-post | ISO datetime публикации |
| `caption` | string | Auto-post | Подпись поста |

---

## Настройка через AdminPanel

Вкладка **Микросервисы** → кнопка ⚙ рядом с **Tagger** → окно `TaggerSettingsWindow`:

| Поле в окне | Ключ в user_settings.json | Описание |
|---|---|---|
| Шаблон переименования | `tagger.rename_template` | Шаблон имени файла (не используется FilenameTagger.py) |
| Разделитель | `tagger.separator` | Разделитель в имени |
| Только новые | `tagger.only_new` | Обрабатывать только необработанные |
| Режим | `tagger.mode` | `rename` или `copy` |

> Эти поля сохраняются в `user_settings.json["tagger"]`, но **текущая версия FilenameTagger.py их не использует** — он работает только через `config_loader` и `filename_tags.json`.
