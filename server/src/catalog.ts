// Каталог магазина — источник правды для цен. Клиент получает его в /state.

export type Slot = "Head" | "Face" | "Neck" | "Body" | "Legs" | "Hands" | "Tail" | "Back";
export type Rarity = "Common" | "Uncommon" | "Rare" | "Legendary" | "BossExclusive";

export interface Item {
  id: string;
  name: string;
  slot: Slot;
  rarity: Rarity;
  price: number;
}

const item = (id: string, name: string, slot: Slot, rarity: Rarity, price: number): Item =>
  ({ id, name, slot, rarity, price });

export const CATALOG: Item[] = [
  // Стартовые наряды (есть 3D-модели, Art/build_panda.py). Выдаются бесплатно при выборе персонажа.
  item("hat_wanderer", "Шляпа странника", "Head", "Uncommon", 300),
  item("scarf_teal", "Бирюзовый шарф", "Neck", "Common", 90),
  item("staff_lantern", "Посох с фонарём", "Hands", "Rare", 650),
  item("hat_witch", "Шляпа волшебницы", "Head", "Uncommon", 300),
  item("cape_cream", "Кремовая накидка", "Back", "Uncommon", 260),
  item("bow_pink", "Розовый бант", "Neck", "Common", 80),
  item("wand_star", "Палочка-звезда", "Hands", "Rare", 650),
  item("head_beanie", "Бини", "Head", "Common", 60),
  item("head_bow", "Бантик", "Head", "Common", 50),
  item("head_wreath", "Венок из цветов", "Head", "Uncommon", 250),
  item("head_crown", "Корона", "Head", "Legendary", 1200),
  item("face_round", "Круглые очки", "Face", "Common", 80),
  item("face_hearts", "Очки-сердечки", "Face", "Uncommon", 300),
  item("neck_scarf", "Шарфик", "Neck", "Common", 70),
  item("neck_bowtie", "Бабочка", "Neck", "Uncommon", 220),
  item("body_hoodie", "Худи", "Body", "Uncommon", 350),
  item("hands_mittens", "Варежки", "Hands", "Common", 90),
  item("tail_ribbon", "Ленточка на хвост", "Tail", "Common", 50),
  item("tail_lights", "Гирлянда", "Tail", "Rare", 700),
  item("back_backpack", "Рюкзачок", "Back", "Uncommon", 280),
  item("back_wings", "Крылышки", "Back", "Rare", 850),
];

export const CATALOG_BY_ID = new Map(CATALOG.map((i) => [i.id, i]));

export const CHARACTERS = ["red_panda_m", "red_panda_f"];

/// Наряд с концепта персонажа — выдаётся и надевается при первом выборе персонажа.
export const STARTER_OUTFIT: Record<string, string[]> = {
  red_panda_m: ["hat_wanderer", "scarf_teal", "staff_lantern"],
  red_panda_f: ["hat_witch", "cape_cream", "bow_pink", "wand_star"],
};
