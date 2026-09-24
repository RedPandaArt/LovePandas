import { DatabaseSync } from "node:sqlite";
import { mkdirSync } from "node:fs";
import { dirname } from "node:path";

export function openDb(path: string): DatabaseSync {
  if (path !== ":memory:") mkdirSync(dirname(path), { recursive: true });
  const db = new DatabaseSync(path);
  db.exec(`
    PRAGMA journal_mode = WAL;
    PRAGMA foreign_keys = ON;

    CREATE TABLE IF NOT EXISTS couples (
      id          TEXT PRIMARY KEY,
      invite_code TEXT NOT NULL UNIQUE,
      version     INTEGER NOT NULL DEFAULT 1,
      created_at  INTEGER NOT NULL
    );

    CREATE TABLE IF NOT EXISTS players (
      id           TEXT PRIMARY KEY,
      token        TEXT NOT NULL UNIQUE,
      name         TEXT NOT NULL DEFAULT '',
      character_id TEXT NOT NULL DEFAULT '',
      coins        INTEGER NOT NULL,
      couple_id    TEXT REFERENCES couples(id),
      last_daily   TEXT,
      created_at   INTEGER NOT NULL
    );

    CREATE TABLE IF NOT EXISTS quests (
      id          TEXT PRIMARY KEY,
      couple_id   TEXT NOT NULL REFERENCES couples(id),
      author_id   TEXT NOT NULL REFERENCES players(id),
      assignee_id TEXT REFERENCES players(id),
      text        TEXT NOT NULL,
      reward      INTEGER NOT NULL,
      status      TEXT NOT NULL,
      created_at  INTEGER NOT NULL,
      updated_at  INTEGER NOT NULL
    );
    CREATE INDEX IF NOT EXISTS quests_couple ON quests(couple_id, status);

    CREATE TABLE IF NOT EXISTS inventory (
      player_id TEXT NOT NULL REFERENCES players(id),
      item_id   TEXT NOT NULL,
      PRIMARY KEY (player_id, item_id)
    );

    CREATE TABLE IF NOT EXISTS equipped (
      player_id TEXT NOT NULL REFERENCES players(id),
      slot      TEXT NOT NULL,
      item_id   TEXT NOT NULL,
      PRIMARY KEY (player_id, slot)
    );
  `);
  return db;
}

/// Всё внутри fn — одна транзакция. node:sqlite синхронный, так что гонок внутри процесса нет,
/// транзакция нужна, чтобы ошибка посреди операции не оставила полусписанные монеты.
export function tx<T>(db: DatabaseSync, fn: () => T): T {
  db.exec("BEGIN IMMEDIATE");
  try {
    const result = fn();
    db.exec("COMMIT");
    return result;
  } catch (e) {
    db.exec("ROLLBACK");
    throw e;
  }
}
