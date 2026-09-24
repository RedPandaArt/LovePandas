// Каталог магазина — источник правды для цен. Клиент получает его в /state.

// Категории инвентаря в клиенте: Оружие = Hands, Шляпка = Head, Одежка = Neck/Back/Body/Face/Tail, Ботиночки = Feet.
export type Slot = "Head" | "Face" | "Neck" | "Body" | "Legs" | "Hands" | "Tail" | "Back" | "Feet";
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

// Только вещи с 3D-моделью (Art/build_panda.py → Items/<id>.fbx). Id вещи = имя файла модели.
// Шарф и накидки — один слот Back: они крепятся к шее и вместе накладываются друг на друга.
export const CATALOG: Item[] = [
  // Оружие
  item("staff_lantern", "Посох с фонарём", "Hands", "Rare", 650),
  item("wand_star", "Палочка-звезда", "Hands", "Rare", 650),
  item("sword_wood", "Деревянный меч", "Hands", "Uncommon", 150),
  item("bamboo_staff", "Бамбуковая палка", "Hands", "Common", 120),
  // Шляпки
  item("hat_wanderer", "Шляпа странника", "Head", "Uncommon", 300),
  item("hat_witch", "Шляпа волшебницы", "Head", "Uncommon", 300),
  item("hat_beanie", "Бини", "Head", "Common", 60),
  item("hat_crown", "Корона", "Head", "Legendary", 1200),
  // Одежка
  item("scarf_teal", "Бирюзовый шарф", "Back", "Common", 90),
  item("cape_cream", "Кремовая накидка", "Back", "Uncommon", 260),
  item("cape_red", "Красный плащ", "Back", "Uncommon", 200),
  item("bow_pink", "Розовый бант", "Neck", "Common", 80),
  // Ботиночки
  item("boots_brown", "Коричневые ботинки", "Feet", "Common", 60),
  item("boots_pink", "Розовые ботиночки", "Feet", "Common", 80),
  item("boots_yellow", "Резиновые сапожки", "Feet", "Uncommon", 120),
];

export const CATALOG_BY_ID = new Map(CATALOG.map((i) => [i.id, i]));

export const CHARACTERS = ["red_panda_m", "red_panda_f"];

/// Наряд с концепта персонажа — выдаётся и надевается при первом выборе персонажа.
export const STARTER_OUTFIT: Record<string, string[]> = {
  red_panda_m: ["hat_wanderer", "scarf_teal", "staff_lantern"],
  red_panda_f: ["hat_witch", "cape_cream", "bow_pink", "wand_star"],
};
