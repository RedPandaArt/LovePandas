import { test } from "node:test";
import assert from "node:assert/strict";
import { openDb } from "../src/db.ts";
import { Game, GameError, START_COINS, DAILY_BONUS } from "../src/game.ts";

function setup() {
  const game = new Game(openDb(":memory:"));
  const a = game.auth(game.register().token);
  const b = game.auth(game.register().token);
  game.setProfile(a, "Панда", "red_panda_m");
  game.createCouple(a);
  const code = game.state(game.auth(a.token)).couple!.inviteCode;
  game.joinCouple(b, code);
  // Строки игроков берутся заново: couple_id поменялся.
  return { game, A: () => game.auth(a.token), B: () => game.auth(b.token) };
}

const coins = (game: Game, p: ReturnType<Game["auth"]>) => (game.state(p).me as { coins: number }).coins;

test("полный цикл задания: эскроу и выплата", () => {
  const { game, A, B } = setup();
  game.createQuest(A(), "Помыть посуду", 30);
  assert.equal(coins(game, A()), START_COINS - 30);

  const q = game.state(B()).quests[0];
  game.takeQuest(B(), q.id);
  game.markDone(B(), q.id);
  game.rejectQuest(A(), q.id); // «не то» — обратно в работу
  game.markDone(B(), q.id);
  game.confirmQuest(A(), q.id);

  assert.equal(coins(game, A()), START_COINS - 30);
  assert.equal(coins(game, B()), START_COINS + 30);
  assert.equal(game.state(A()).quests.length, 0);
});

test("нельзя пообещать больше, чем есть", () => {
  const { game, A } = setup();
  assert.throws(() => game.createQuest(A(), "Дорого", START_COINS + 1), GameError);
  assert.equal(coins(game, A()), START_COINS);
});

test("отмена до взятия возвращает монеты, после — нельзя", () => {
  const { game, A, B } = setup();
  game.createQuest(A(), "Раз", 10);
  game.createQuest(A(), "Два", 10);
  const [q2, q1] = game.state(A()).quests;
  game.cancelQuest(A(), q1.id);
  assert.equal(coins(game, A()), START_COINS - 10);
  game.takeQuest(B(), q2.id);
  assert.throws(() => game.cancelQuest(A(), q2.id), /уже взяли/);
});

test("чужие задания и своё задание", () => {
  const { game, A, B } = setup();
  game.createQuest(A(), "Раз", 10);
  const q = game.state(A()).quests[0];
  assert.throws(() => game.takeQuest(A(), q.id), /Своё/);
  assert.throws(() => game.confirmQuest(B(), q.id), GameError);

  // Игрок из другой пары не видит и не трогает задание.
  const c = game.auth(game.register().token);
  game.createCouple(c);
  assert.throws(() => game.takeQuest(game.auth(c.token), q.id), /не найдено/);
});

test("без партнёра задания не ставятся, третий в пару не войдёт", () => {
  const game = new Game(openDb(":memory:"));
  const a = game.auth(game.register().token);
  game.createCouple(a);
  assert.throws(() => game.createQuest(game.auth(a.token), "x", 1), /Партнёр/);

  const code = game.state(game.auth(a.token)).couple!.inviteCode;
  game.joinCouple(game.auth(game.register().token), code);
  assert.throws(() => game.joinCouple(game.auth(game.register().token), code), /уже двое/);
  assert.throws(() => game.joinCouple(game.auth(game.register().token), "NOPE00"), /не найден/);
});

test("магазин: покупка, надевание, нехватка монет", () => {
  const { game, A } = setup();
  type Me = { coins: number; equipped: { slot: string; itemId: string }[] };
  const head = (m: Me) => m.equipped.find((e) => e.slot === "Head")?.itemId;
  game.buy(A(), "hat_beanie");
  let me = game.state(A()).me as Me;
  assert.equal(me.coins, START_COINS - 60);
  assert.equal(head(me), "hat_beanie"); // купил — сразу надел вместо стартовой шляпы
  game.toggleEquip(A(), "hat_beanie");
  me = game.state(A()).me as Me;
  assert.equal(head(me), undefined);
  assert.throws(() => game.buy(A(), "hat_beanie"), /Уже/);
  assert.throws(() => game.buy(A(), "hat_crown"), /Не хватает/);
  assert.throws(() => game.toggleEquip(A(), "hat_crown"), /нет/);
});

test("стартовый наряд выдаётся один раз при выборе персонажа", () => {
  const game = new Game(openDb(":memory:"));
  const t = game.register().token;
  game.setProfile(game.auth(t), "Аля", "red_panda_f");
  let me = game.state(game.auth(t)).me as { coins: number; inventory: string[]; equipped: unknown[] };
  assert.deepEqual([...me.inventory].sort(), ["bow_pink", "cape_cream", "hat_witch", "wand_star"]);
  assert.equal(me.equipped.length, 4);
  assert.equal(me.coins, START_COINS);
  game.setProfile(game.auth(t), "Аля", "red_panda_m"); // смена персонажа — без второго подарка
  me = game.state(game.auth(t)).me as typeof me;
  assert.equal(me.inventory.length, 4);
});

test("ежедневный бонус раз в день", () => {
  const { game, A } = setup();
  const day1 = Date.UTC(2026, 8, 24, 10);
  assert.equal(game.claimDaily(A(), day1), DAILY_BONUS);
  assert.equal(game.claimDaily(A(), day1 + 3600_000), 0);
  assert.equal(game.claimDaily(A(), day1 + 24 * 3600_000), DAILY_BONUS);
  assert.equal(coins(game, A()), START_COINS + 2 * DAILY_BONUS);
});

test("версия пары растёт при изменениях", () => {
  const { game, A, B } = setup();
  const v = game.version(A());
  game.createQuest(A(), "x", 1);
  assert.ok(game.version(B()) > v);
});
