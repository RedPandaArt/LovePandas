import type { DatabaseSync } from "node:sqlite";
import { randomBytes, randomUUID } from "node:crypto";
import { CATALOG, CATALOG_BY_ID, CHARACTERS } from "./catalog.ts";
import type { Slot } from "./catalog.ts";
import { tx } from "./db.ts";

// Баланс — стартовые ориентиры из GDD, раздел 7.
export const START_COINS = 100;
export const DAILY_BONUS = 10;
export const MAX_REWARD = 9999;
export const MAX_QUEST_TEXT = 140;
export const MAX_NAME = 20;

export type QuestStatus = "Created" | "Taken" | "Done" | "Confirmed" | "Cancelled";

/// Ошибка, которую можно показать игроку. status — HTTP-код ответа.
export class GameError extends Error {
  status: number;
  constructor(message: string, status = 400) {
    super(message);
    this.status = status;
  }
}

interface PlayerRow {
  id: string;
  token: string;
  name: string;
  character_id: string;
  coins: number;
  couple_id: string | null;
  last_daily: string | null;
}

interface QuestRow {
  id: string;
  couple_id: string;
  author_id: string;
  assignee_id: string | null;
  text: string;
  reward: number;
  status: QuestStatus;
  created_at: number;
}

// Без похожих символов (0/O, 1/I), чтобы код легко продиктовать партнёру.
const CODE_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

export class Game {
  db: DatabaseSync;
  tzOffsetMin: number;

  constructor(db: DatabaseSync, tzOffsetMin = 300) {
    this.db = db;
    this.tzOffsetMin = tzOffsetMin;
  }

  // ---------- Вход ----------

  register(): { token: string; playerId: string } {
    const id = randomUUID();
    const token = randomBytes(32).toString("base64url");
    this.db.prepare("INSERT INTO players (id, token, coins, created_at) VALUES (?, ?, ?, ?)")
      .run(id, token, START_COINS, Date.now());
    return { token, playerId: id };
  }

  auth(token: string | undefined): PlayerRow {
    if (!token) throw new GameError("Нужен вход", 401);
    const p = this.db.prepare("SELECT * FROM players WHERE token = ?").get(token) as PlayerRow | undefined;
    if (!p) throw new GameError("Нужен вход", 401);
    return p;
  }

  setProfile(me: PlayerRow, name: unknown, characterId: unknown) {
    const n = typeof name === "string" ? name.trim() : "";
    if (n.length === 0 || n.length > MAX_NAME) throw new GameError(`Имя от 1 до ${MAX_NAME} символов`);
    if (typeof characterId !== "string" || !CHARACTERS.includes(characterId)) throw new GameError("Нет такого персонажа");
    tx(this.db, () => {
      this.db.prepare("UPDATE players SET name = ?, character_id = ? WHERE id = ?").run(n, characterId, me.id);
      this.bump(me.couple_id);
    });
  }

  // ---------- Пара ----------

  createCouple(me: PlayerRow) {
    if (me.couple_id) return;
    tx(this.db, () => {
      const id = randomUUID();
      this.db.prepare("INSERT INTO couples (id, invite_code, created_at) VALUES (?, ?, ?)")
        .run(id, this.newInviteCode(), Date.now());
      this.db.prepare("UPDATE players SET couple_id = ? WHERE id = ?").run(id, me.id);
    });
  }

  joinCouple(me: PlayerRow, code: unknown) {
    if (me.couple_id) throw new GameError("Ты уже в паре");
    const c = typeof code === "string" ? code.trim().toUpperCase() : "";
    const couple = this.db.prepare("SELECT id FROM couples WHERE invite_code = ?").get(c) as { id: string } | undefined;
    if (!couple) throw new GameError("Код не найден");
    tx(this.db, () => {
      const count = (this.db.prepare("SELECT COUNT(*) AS n FROM players WHERE couple_id = ?").get(couple.id) as { n: number }).n;
      if (count >= 2) throw new GameError("В этой паре уже двое");
      this.db.prepare("UPDATE players SET couple_id = ? WHERE id = ?").run(couple.id, me.id);
      this.bump(couple.id);
    });
  }

  // ---------- Экономика ----------

  /// Ежедневный бонус. День считается по часовому поясу сервера (по умолчанию UTC+5).
  claimDaily(me: PlayerRow, now = Date.now()): number {
    const day = new Date(now + this.tzOffsetMin * 60_000).toISOString().slice(0, 10);
    if (me.last_daily === day) return 0;
    tx(this.db, () => {
      this.db.prepare("UPDATE players SET coins = coins + ?, last_daily = ? WHERE id = ?").run(DAILY_BONUS, day, me.id);
      this.bump(me.couple_id);
    });
    return DAILY_BONUS;
  }

  // ---------- Задания: Created → Taken → Done → Confirmed ----------

  createQuest(me: PlayerRow, text: unknown, reward: unknown) {
    const couple = this.requirePartner(me);
    const t = typeof text === "string" ? text.trim() : "";
    if (!t) throw new GameError("Напиши, что нужно сделать");
    if (t.length > MAX_QUEST_TEXT) throw new GameError("Слишком длинное задание");
    if (!Number.isInteger(reward) || (reward as number) < 1 || (reward as number) > MAX_REWARD)
      throw new GameError(`Цена от 1 до ${MAX_REWARD}`);
    const r = reward as number;
    tx(this.db, () => {
      // Эскроу: списываем условно — если монет не хватает, строка не обновится.
      const res = this.db.prepare("UPDATE players SET coins = coins - ? WHERE id = ? AND coins >= ?").run(r, me.id, r);
      if (res.changes === 0) throw new GameError("Не хватает монет");
      const now = Date.now();
      this.db.prepare(`INSERT INTO quests (id, couple_id, author_id, text, reward, status, created_at, updated_at)
                       VALUES (?, ?, ?, ?, ?, 'Created', ?, ?)`).run(randomUUID(), couple, me.id, t, r, now, now);
      this.bump(couple);
    });
  }

  cancelQuest(me: PlayerRow, questId: string) {
    tx(this.db, () => {
      const q = this.quest(me, questId);
      if (q.author_id !== me.id) throw new GameError("Это не твоё задание");
      if (q.status !== "Created") throw new GameError("Задание уже взяли");
      this.setStatus(q, "Cancelled");
      this.db.prepare("UPDATE players SET coins = coins + ? WHERE id = ?").run(q.reward, me.id);
    });
  }

  takeQuest(me: PlayerRow, questId: string) {
    tx(this.db, () => {
      const q = this.quest(me, questId);
      if (q.author_id === me.id) throw new GameError("Своё задание брать нельзя");
      if (q.status !== "Created") throw new GameError("Задание уже взято");
      this.db.prepare("UPDATE quests SET assignee_id = ? WHERE id = ?").run(me.id, q.id);
      this.setStatus(q, "Taken");
    });
  }

  markDone(me: PlayerRow, questId: string) {
    tx(this.db, () => {
      const q = this.quest(me, questId);
      if (q.assignee_id !== me.id || q.status !== "Taken") throw new GameError("Нельзя отметить");
      this.setStatus(q, "Done");
    });
  }

  confirmQuest(me: PlayerRow, questId: string) {
    tx(this.db, () => {
      const q = this.quest(me, questId);
      if (q.author_id !== me.id || q.status !== "Done") throw new GameError("Нечего подтверждать");
      this.setStatus(q, "Confirmed");
      this.db.prepare("UPDATE players SET coins = coins + ? WHERE id = ?").run(q.reward, q.assignee_id);
    });
  }

  /// Автор не согласен, что сделано: задание возвращается исполнителю в работу.
  rejectQuest(me: PlayerRow, questId: string) {
    tx(this.db, () => {
      const q = this.quest(me, questId);
      if (q.author_id !== me.id || q.status !== "Done") throw new GameError("Нечего отклонять");
      this.setStatus(q, "Taken");
    });
  }

  // ---------- Магазин и гардероб ----------

  buy(me: PlayerRow, itemId: unknown) {
    const item = typeof itemId === "string" ? CATALOG_BY_ID.get(itemId) : undefined;
    if (!item) throw new GameError("Нет такого предмета");
    if (item.rarity === "BossExclusive") throw new GameError("Только с босса");
    tx(this.db, () => {
      if (this.owns(me.id, item.id)) throw new GameError("Уже куплено");
      const res = this.db.prepare("UPDATE players SET coins = coins - ? WHERE id = ? AND coins >= ?")
        .run(item.price, me.id, item.price);
      if (res.changes === 0) throw new GameError("Не хватает монет");
      this.db.prepare("INSERT INTO inventory (player_id, item_id) VALUES (?, ?)").run(me.id, item.id);
      // Купил — сразу примерил.
      this.db.prepare("INSERT OR REPLACE INTO equipped (player_id, slot, item_id) VALUES (?, ?, ?)").run(me.id, item.slot, item.id);
      this.bump(me.couple_id);
    });
  }

  toggleEquip(me: PlayerRow, itemId: unknown) {
    const item = typeof itemId === "string" ? CATALOG_BY_ID.get(itemId) : undefined;
    if (!item || !this.owns(me.id, item.id)) throw new GameError("Этого предмета у тебя нет");
    tx(this.db, () => {
      const cur = this.db.prepare("SELECT item_id FROM equipped WHERE player_id = ? AND slot = ?").get(me.id, item.slot) as
        { item_id: string } | undefined;
      if (cur?.item_id === item.id)
        this.db.prepare("DELETE FROM equipped WHERE player_id = ? AND slot = ?").run(me.id, item.slot);
      else
        this.db.prepare("INSERT OR REPLACE INTO equipped (player_id, slot, item_id) VALUES (?, ?, ?)").run(me.id, item.slot, item.id);
      this.bump(me.couple_id);
    });
  }

  // ---------- Состояние для клиента ----------

  /// version — счётчик изменений пары: клиент опрашивает /state?since=N и получает 304, если нового нет.
  version(me: PlayerRow): number {
    if (!me.couple_id) return 0;
    return (this.db.prepare("SELECT version FROM couples WHERE id = ?").get(me.couple_id) as { version: number }).version;
  }

  state(me: PlayerRow) {
    const fresh = this.db.prepare("SELECT * FROM players WHERE id = ?").get(me.id) as PlayerRow;
    const couple = fresh.couple_id
      ? this.db.prepare("SELECT id, invite_code, version FROM couples WHERE id = ?").get(fresh.couple_id) as
        { id: string; invite_code: string; version: number }
      : null;
    const partner = couple
      ? this.db.prepare("SELECT * FROM players WHERE couple_id = ? AND id != ?").get(couple.id, fresh.id) as PlayerRow | undefined
      : undefined;
    const quests = couple
      ? this.db.prepare(`SELECT id, author_id, assignee_id, text, reward, status, created_at FROM quests
                         WHERE couple_id = ? AND status IN ('Created', 'Taken', 'Done') ORDER BY created_at DESC`)
          .all(couple.id) as unknown as QuestRow[]
      : [];

    return {
      version: couple?.version ?? 0,
      me: this.publicPlayer(fresh, true),
      partner: partner ? this.publicPlayer(partner, false) : null,
      couple: couple ? { inviteCode: couple.invite_code } : null,
      quests: quests.map((q) => ({
        id: q.id, authorId: q.author_id, assigneeId: q.assignee_id ?? "",
        text: q.text, reward: q.reward, status: q.status, createdAt: q.created_at,
      })),
      catalog: CATALOG,
    };
  }

  // ---------- Внутреннее ----------

  private publicPlayer(p: PlayerRow, self: boolean) {
    const equipped = this.db.prepare("SELECT slot, item_id FROM equipped WHERE player_id = ?").all(p.id) as
      { slot: Slot; item_id: string }[];
    const base = {
      id: p.id, name: p.name, characterId: p.character_id,
      equipped: equipped.map((e) => ({ slot: e.slot, itemId: e.item_id })),
    };
    if (!self) return base;
    const inventory = (this.db.prepare("SELECT item_id FROM inventory WHERE player_id = ?").all(p.id) as { item_id: string }[])
      .map((r) => r.item_id);
    return { ...base, coins: p.coins, inventory };
  }

  private requirePartner(me: PlayerRow): string {
    if (!me.couple_id) throw new GameError("Сначала свяжитесь в пару");
    const n = (this.db.prepare("SELECT COUNT(*) AS n FROM players WHERE couple_id = ?").get(me.couple_id) as { n: number }).n;
    if (n < 2) throw new GameError("Партнёр ещё не присоединился");
    return me.couple_id;
  }

  private quest(me: PlayerRow, id: string): QuestRow {
    const q = this.db.prepare("SELECT * FROM quests WHERE id = ?").get(id) as QuestRow | undefined;
    if (!q || q.couple_id !== me.couple_id) throw new GameError("Задание не найдено", 404);
    return q;
  }

  private setStatus(q: QuestRow, status: QuestStatus) {
    this.db.prepare("UPDATE quests SET status = ?, updated_at = ? WHERE id = ?").run(status, Date.now(), q.id);
    this.bump(q.couple_id);
  }

  private owns(playerId: string, itemId: string): boolean {
    return !!this.db.prepare("SELECT 1 FROM inventory WHERE player_id = ? AND item_id = ?").get(playerId, itemId);
  }

  private bump(coupleId: string | null) {
    if (coupleId) this.db.prepare("UPDATE couples SET version = version + 1 WHERE id = ?").run(coupleId);
  }

  private newInviteCode(): string {
    for (;;) {
      const bytes = randomBytes(6);
      const code = Array.from(bytes, (b) => CODE_ALPHABET[b % CODE_ALPHABET.length]).join("");
      if (!this.db.prepare("SELECT 1 FROM couples WHERE invite_code = ?").get(code)) return code;
    }
  }
}
