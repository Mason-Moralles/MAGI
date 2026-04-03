import argparse
import sqlite3
from pathlib import Path


PLACEHOLDERS = {
    "channel_id": -1,
    "channel_name": "",
    "slot_caption": "",
    "file_name": "",
    "source_url": "",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Run KT4 SQLite validation queries and print screenshot-friendly results."
    )
    parser.add_argument(
        "--db-path",
        default=str(Path("data") / "magi.db"),
        help="Path to SQLite database. Default: data/magi.db",
    )
    parser.add_argument(
        "--sql-file",
        default=str(Path("tests") / "sql" / "kt4_db_validation.sql"),
        help="Path to validation SQL file. Default: tests/sql/kt4_db_validation.sql",
    )
    parser.add_argument("--channel-id", help="Target channel id")
    parser.add_argument("--channel-name", default="", help="Expected channel name")
    parser.add_argument("--slot-caption", default="", help="Expected schedule slot caption")
    parser.add_argument("--file-name", default="", help="Expected image file name")
    parser.add_argument("--source-url", default="", help="Expected download source URL")
    parser.add_argument(
        "--auto-fill-from-db",
        action="store_true",
        help="Fill missing values from the latest records in the current database",
    )
    parser.add_argument(
        "--include-post-delete-checks",
        action="store_true",
        help="Include cascade delete validation queries intended for the state after channel deletion",
    )
    return parser.parse_args()


def split_sql_statements(sql_text: str) -> list[str]:
    statements: list[str] = []
    current: list[str] = []

    for line in sql_text.splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith("--"):
            continue
        current.append(line)
        if stripped.endswith(";"):
            statement = "\n".join(current).strip().rstrip(";").strip()
            if statement:
                statements.append(statement)
            current = []

    if current:
        statement = "\n".join(current).strip().rstrip(";").strip()
        if statement:
            statements.append(statement)

    return statements


def execute_scalar(connection: sqlite3.Connection, query: str, *params: tuple[str, str]) -> str:
    cursor = connection.execute(query, params)
    row = cursor.fetchone()
    if row is None or row[0] is None:
        return ""
    return str(row[0])


def auto_fill_params(connection: sqlite3.Connection, params: dict[str, str], channel_id: str) -> dict[str, str]:
    filled = dict(params)
    filled["channel_id"] = channel_id

    if not filled["channel_name"]:
        filled["channel_name"] = execute_scalar(
            connection,
            "SELECT Name FROM Channels WHERE Id = ?",
            channel_id,
        )

    if not filled["slot_caption"]:
        filled["slot_caption"] = execute_scalar(
            connection,
            "SELECT Caption FROM ScheduleSlots WHERE ChannelId = ? ORDER BY rowid DESC LIMIT 1",
            channel_id,
        )

    if not filled["file_name"]:
        filled["file_name"] = execute_scalar(
            connection,
            "SELECT FileName FROM Images WHERE ChannelId = ? ORDER BY rowid DESC LIMIT 1",
            channel_id,
        )

    if not filled["source_url"]:
        filled["source_url"] = execute_scalar(
            connection,
            "SELECT SourceUrl FROM DownloadRecords WHERE ChannelId = ? ORDER BY rowid DESC LIMIT 1",
            channel_id,
        )

    return filled


def select_channel_id(connection: sqlite3.Connection, explicit_channel_id: str | None) -> str:
    if explicit_channel_id:
        return explicit_channel_id

    channel_id = execute_scalar(connection, "SELECT Id FROM Channels ORDER BY rowid DESC LIMIT 1")
    if not channel_id:
        raise ValueError("No channels found in database. Run an API or UI scenario first.")
    return channel_id


def should_skip_statement(statement: str, params: dict[str, str], include_post_delete_checks: bool) -> str | None:
    if not include_post_delete_checks and "_after_delete" in statement:
        return "post_delete_checks_disabled"

    placeholders = {
        ":channel_name": "channel_name",
        ":slot_caption": "slot_caption",
        ":file_name": "file_name",
        ":source_url": "source_url",
    }

    for placeholder, key in placeholders.items():
        if placeholder in statement and not params.get(key):
            return key

    return None


def print_result(index: int, query: str, cursor: sqlite3.Cursor) -> None:
    rows = cursor.fetchall()
    columns = [description[0] for description in cursor.description] if cursor.description else []

    print(f"\n=== Query {index} ===")
    print(query)

    if not rows:
        print("(no rows)")
        return

    print(" | ".join(columns))
    print("-+-".join("-" * len(column) for column in columns))

    for row in rows:
        print(" | ".join("" if value is None else str(value) for value in row))


def main() -> int:
    args = parse_args()
    db_path = Path(args.db_path)
    sql_file = Path(args.sql_file)

    if not db_path.exists():
        print(f"Database not found: {db_path}")
        return 1

    if not sql_file.exists():
        print(f"SQL file not found: {sql_file}")
        return 1

    sql_text = sql_file.read_text(encoding="utf-8")
    statements = split_sql_statements(sql_text)

    with sqlite3.connect(db_path) as connection:
        channel_id = select_channel_id(connection, args.channel_id)

        params = dict(PLACEHOLDERS)
        params.update(
            {
                "channel_id": channel_id,
                "channel_name": args.channel_name,
                "slot_caption": args.slot_caption,
                "file_name": args.file_name,
                "source_url": args.source_url,
            }
        )

        if args.auto_fill_from_db:
            params = auto_fill_params(connection, params, channel_id)

        print("KT4 validation run")
        print(f"Database: {db_path}")
        print(f"SQL file: {sql_file}")
        print(f"Parameters: {params}")

        for index, statement in enumerate(statements, start=1):
            skipped_key = should_skip_statement(statement, params, args.include_post_delete_checks)
            if skipped_key:
                print(f"\n=== Query {index} skipped ===")
                print(statement)
                if skipped_key == "post_delete_checks_disabled":
                    print("Skipped because this is a post-delete validation block. Re-run with --include-post-delete-checks after deleting the channel.")
                else:
                    print(f"Skipped because parameter '{skipped_key}' is empty.")
                continue

            cursor = connection.execute(statement, params)
            print_result(index, statement, cursor)

    print("\nKT4 validation completed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())