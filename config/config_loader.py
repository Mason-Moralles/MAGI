from __future__ import annotations

import json
import os
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Dict, Optional


APP_NAME = "MAGI"


def _get_appdata_dir() -> Path:
    """
    Windows: %APPDATA%/MAGI
    """
    override = os.getenv("MAGI_APPDATA_DIR")
    if override:
        return Path(override).expanduser().resolve()

    if os.name == "nt":
        base = os.getenv("APPDATA") or os.getenv("LOCALAPPDATA") or str(Path.home())
        return Path(base) / APP_NAME

def _load_json(path: Path, default: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
    if default is None:
        default = {}
    if not path.exists():
        return default
    try:
        with path.open("r", encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return default


def _require(cond: bool, msg: str) -> None:
    if not cond:
        raise ValueError(msg)


def _to_int(value: Any, default: int) -> int:
    try:
        if value is None:
            return default
        if isinstance(value, int):
            return value
        if isinstance(value, str) and value.strip() != "":
            return int(value)
    except Exception:
        pass
    return default


def _as_path(value: str | Path) -> Path:
    return Path(value).expanduser()


@dataclass(frozen=True)
class EffectiveConfig:
    # roots
    project_root: Path
    arts_root: Path

    # folders (derived from defaults + arts_root)
    check_images_dir: Path
    new_images_dir: Path
    post_images_dir: Path

    # db files (derived from user_settings.db + project_root)
    images_json: Path
    posted_images_json: Path
    schedule_json: Path

    # telegram
    channel_link: str
    api_id: Optional[int]
    api_hash: Optional[str]
    session_file: Path
    bot_token: Optional[str]
    telegram_mode: str  # "user" or "bot"

    # schedule settings
    time_zone: str
    schedule_days: int
    delay_between_post_sec: int

    # posting rules
    rules: list[dict]               # v2: [{time, days, caption}, ...]
    week_template: Dict[str, list[str]]   # совместимость v1
    captions_by_time: Dict[str, str]      # совместимость v1
    forced_posts: list[dict]
    forced_captions: list[dict]


def load_effective_config(
    *,
    fallback_project_root: Optional[Path] = None
) -> EffectiveConfig:
    """
    1) Читает AppData/user_settings.json и AppData/posting_rules.json
    2) Находит project_root (из user_settings.paths.project_root или fallback)
    3) Читает defaults.json из project_root/data/defaults.json
    4) Собирает итоговую конфигурацию и нормализует пути
    """
    appdata = _get_appdata_dir()
    user_settings_path = appdata / "user_settings.json"
    posting_rules_path = appdata / "posting_rules.json"

    user = _load_json(user_settings_path, {})
    rules = _load_json(posting_rules_path, {})

    # project_root: берём из user_settings, иначе fallback, иначе cwd
    pr_value = (user.get("paths") or {}).get("project_root") or ""
    if pr_value.strip():
        project_root = _as_path(pr_value).resolve()
    else:
        project_root = (fallback_project_root or Path.cwd()).resolve()

    defaults_path = project_root / "data" / "defaults.json"
    defaults = _load_json(defaults_path, {})

    # arts_root обязателен для Auto-post (где лежат папки check/new/post)
    # Приоритет: env MAGI_ARTS_ROOT > user_settings.json > fallback (project/arts)
    arts_root_env = os.getenv("MAGI_ARTS_ROOT", "").strip()
    arts_root_value = arts_root_env or (user.get("paths") or {}).get("arts_root") or ""
    arts_root = _as_path(arts_root_value).resolve() if arts_root_value.strip() else (project_root / "arts").resolve()

    # defaults paths -> папки внутри arts_root
    def_paths = defaults.get("paths") or {}
    check_dir = arts_root / (def_paths.get("check-images") or "check-images")
    new_dir = arts_root / (def_paths.get("new-images") or "new-images")
    post_dir = arts_root / (def_paths.get("post-images") or "post-images")

    # db paths из user_settings -> относительно project_root
    db = user.get("db") or {}
    images_json = (project_root / (db.get("images_json") or "data/json/images/images.json")).resolve()
    posted_images_json = (project_root / (db.get("posted_images_json") or "data/json/images/posted_images.json")).resolve()
    schedule_json = (project_root / (db.get("schedule_json") or "data/json/schedule/schedule.json")).resolve()

    # telegram
    tg = user.get("telegram") or {}
    channel_link = (tg.get("channel_link") or "").strip()
    api_id_raw = tg.get("api_id")
    api_id = _to_int(api_id_raw, default=0) or None
    api_hash = (tg.get("api_hash") or "").strip() or None
    bot_token = (tg.get("bot_token") or "").strip() or None

    session_file_name = (tg.get("session_file") or "session").strip()
    # session лучше хранить в appdata, чтобы не мусорить проект
    session_file = (appdata / session_file_name).resolve()

    telegram_mode = "bot" if bot_token else "user"

    # schedule settings (из user_settings + defaults)
    sch_user = user.get("schedule") or {}
    sch_def = defaults.get("schedule") or {}

    time_zone = (sch_user.get("time_zone") or "Europe/Moscow").strip()
    schedule_days = _to_int(sch_user.get("schedule_days") or sch_user.get("shedule_day"), default=7)
    delay_between = _to_int(sch_def.get("delay_between_post"), default=5)

    # posting rules — поддерживаем оба формата
    # v2: {"rules": [{time, days, caption}, ...]}
    # v1: {"week_template": {...}, "captions_by_time": {...}}
    rules_list = rules.get("rules")  # v2
    if rules_list is not None:
        # Новый формат — используем напрямую
        posting_rules = rules_list
        # Для обратной совместимости строим week_template и captions_by_time на лету
        week_template: Dict[str, list] = {}
        captions_by_time: Dict[str, str] = {}
        for rule in posting_rules:
            t = (rule.get("time") or "").strip()
            cap = (rule.get("caption") or "").strip()
            if cap and t:
                captions_by_time[t] = cap
            for day in rule.get("days") or []:
                week_template.setdefault(day, [])
                if t and t not in week_template[day]:
                    week_template[day].append(t)
    else:
        # Старый формат v1
        posting_rules = []
        week_template = rules.get("week_template") or {}
        captions_by_time = rules.get("captions_by_time") or {}
        # Строим rules[] из week_template + captions_by_time для Auto-post
        time_days: Dict[str, list] = {}
        for day, times in week_template.items():
            for t in times:
                time_days.setdefault(t, []).append(day)
        for t, days in time_days.items():
            posting_rules.append({
                "time": t,
                "days": days,
                "caption": captions_by_time.get(t, ""),
            })

    forced_posts = rules.get("forced_posts") or []
    forced_captions = rules.get("forced_captions") or []

    # БАЗОВАЯ ВАЛИДАЦИЯ
    # В мультиканальном режиме (Gateway) credentials берутся из БД, а не из JSON.
    # Строгая валидация только если Gateway недоступен (JSON fallback).
    gateway_available = False
    try:
        from config.gateway_client import GatewayClient
        gateway_available = GatewayClient().is_gateway_available()
    except Exception:
        pass

    if not gateway_available:
        # JSON fallback — credentials обязательны в user_settings.json
        _require(channel_link != "", "user_settings.json: telegram.channel_link пустой")
        _require(api_id is not None and api_hash is not None, "user_settings.json: telegram.api_id/api_hash должны быть заполнены")
        if telegram_mode == "bot":
            _require(bot_token is not None, "user_settings.json: telegram.bot_token пустой (режим bot)")

    # Пути — мягкая проверка, папки могут не существовать если arts_root из канала
    if arts_root.exists() and not check_dir.exists():
        check_dir.mkdir(parents=True, exist_ok=True)
    # new_dir/post_dir могут создаваться по ходу — не заставляем существовать

    return EffectiveConfig(
        project_root=project_root,
        arts_root=arts_root,
        check_images_dir=check_dir,
        new_images_dir=new_dir,
        post_images_dir=post_dir,
        images_json=images_json,
        posted_images_json=posted_images_json,
        schedule_json=schedule_json,
        channel_link=channel_link,
        api_id=api_id,
        api_hash=api_hash,
        session_file=session_file,
        bot_token=bot_token,
        telegram_mode=telegram_mode,
        time_zone=time_zone,
        schedule_days=schedule_days,
        delay_between_post_sec=delay_between,
        rules=posting_rules,
        week_template=week_template,
        captions_by_time=captions_by_time,
        forced_posts=forced_posts,
        forced_captions=forced_captions,
    )
