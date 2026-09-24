import { createServer } from "node:http";
import type { IncomingMessage, ServerResponse } from "node:http";
import { openDb } from "./db.ts";
import { Game, GameError } from "./game.ts";

const PORT = Number(process.env.PORT ?? 8787);
const DB_PATH = process.env.DB_PATH ?? "data/lovepandas.db";
const TZ_OFFSET_MIN = Number(process.env.TZ_OFFSET_MIN ?? 300);

export function createApp(game: Game) {
  // Все ответы на действия — { state } или { error }: клиент после любого действия получает
  // свежее состояние целиком и перерисовывается, отдельного запроса не нужно.
  return async function handle(req: IncomingMessage, res: ServerResponse) {
    const url = new URL(req.url ?? "/", "http://x");
    const path = url.pathname;
    try {
      if (req.method === "GET" && path === "/health") return send(res, 200, { ok: true });
      if (req.method === "POST" && path === "/auth/register") return send(res, 200, game.register());

      const token = req.headers.authorization?.replace(/^Bearer\s+/i, "");
      const me = game.auth(token);

      if (req.method === "GET" && path === "/state") {
        const since = Number(url.searchParams.get("since") ?? -1);
        if (since >= 0 && since === game.version(me)) { res.writeHead(304).end(); return; }
        return send(res, 200, { state: game.state(me) });
      }

      if (req.method !== "POST") throw new GameError("Не найдено", 404);
      const body = await readJson(req);
      let bonus = 0;

      const quest = path.match(/^\/quests\/([\w-]+)\/(cancel|take|done|confirm|reject)$/);
      if (quest) {
        const [, id, action] = quest;
        if (action === "cancel") game.cancelQuest(me, id);
        else if (action === "take") game.takeQuest(me, id);
        else if (action === "done") game.markDone(me, id);
        else if (action === "confirm") game.confirmQuest(me, id);
        else game.rejectQuest(me, id);
      } else switch (path) {
        case "/profile": game.setProfile(me, body.name, body.characterId); break;
        case "/couple/create": game.createCouple(me); break;
        case "/couple/join": game.joinCouple(me, body.code); break;
        case "/daily": bonus = game.claimDaily(me); break;
        case "/quests": game.createQuest(me, body.text, body.reward); break;
        case "/shop/buy": game.buy(me, body.itemId); break;
        case "/wardrobe/toggle": game.toggleEquip(me, body.itemId); break;
        default: throw new GameError("Не найдено", 404);
      }
      send(res, 200, { bonus, state: game.state(me) });
    } catch (e) {
      if (e instanceof GameError) return send(res, e.status, { error: e.message });
      console.error(e);
      send(res, 500, { error: "Что-то сломалось, попробуй ещё раз" });
    }
  };
}

function send(res: ServerResponse, status: number, body: unknown) {
  const json = JSON.stringify(body);
  res.writeHead(status, { "Content-Type": "application/json; charset=utf-8", "Content-Length": Buffer.byteLength(json) });
  res.end(json);
}

async function readJson(req: IncomingMessage): Promise<Record<string, unknown>> {
  let size = 0;
  const chunks: Buffer[] = [];
  for await (const chunk of req) {
    size += chunk.length;
    if (size > 16 * 1024) throw new GameError("Слишком большой запрос", 413);
    chunks.push(chunk);
  }
  if (size === 0) return {};
  try {
    const v = JSON.parse(Buffer.concat(chunks).toString("utf8"));
    return v && typeof v === "object" ? v : {};
  } catch {
    throw new GameError("Кривой JSON");
  }
}

if (import.meta.main) {
  const game = new Game(openDb(DB_PATH), TZ_OFFSET_MIN);
  createServer(createApp(game)).listen(PORT, () => console.log(`LovePandas server on :${PORT}, db ${DB_PATH}`));
}
