using System.Collections.Generic;

namespace LovePandas.Core
{
    /// Каталог магазина. Пока заглушки по CONTENT_MVP.md — у каждого предмета нет модели,
    /// CharacterView рисует его примитивом нужного цвета.
    public static class ItemCatalog
    {
        public static readonly List<ItemDef> All = new List<ItemDef>
        {
            Item("head_beanie",     "Бини",              Slot.Head,  Rarity.Common,    60),
            Item("head_bow",        "Бантик",            Slot.Head,  Rarity.Common,    50),
            Item("head_wreath",     "Венок из цветов",   Slot.Head,  Rarity.Uncommon, 250),
            Item("head_crown",      "Корона",            Slot.Head,  Rarity.Legendary,1200),
            Item("face_round",      "Круглые очки",      Slot.Face,  Rarity.Common,    80),
            Item("face_hearts",     "Очки-сердечки",     Slot.Face,  Rarity.Uncommon, 300),
            Item("neck_scarf",      "Шарфик",            Slot.Neck,  Rarity.Common,    70),
            Item("neck_bowtie",     "Бабочка",           Slot.Neck,  Rarity.Uncommon, 220),
            Item("body_hoodie",     "Худи",              Slot.Body,  Rarity.Uncommon, 350),
            Item("hands_mittens",   "Варежки",           Slot.Hands, Rarity.Common,    90),
            Item("tail_ribbon",     "Ленточка на хвост", Slot.Tail,  Rarity.Common,    50),
            Item("tail_lights",     "Гирлянда",          Slot.Tail,  Rarity.Rare,     700),
            Item("back_backpack",   "Рюкзачок",          Slot.Back,  Rarity.Uncommon, 280),
            Item("back_wings",      "Крылышки",          Slot.Back,  Rarity.Rare,     850),
        };

        static readonly Dictionary<string, ItemDef> byId = BuildIndex();

        public static ItemDef Get(string id) => id != null && byId.TryGetValue(id, out var d) ? d : null;

        static ItemDef Item(string id, string name, Slot slot, Rarity rarity, int price) =>
            new ItemDef { id = id, name = name, slot = slot, rarity = rarity, price = price };

        static Dictionary<string, ItemDef> BuildIndex()
        {
            var d = new Dictionary<string, ItemDef>();
            foreach (var i in All) d[i.id] = i;
            return d;
        }
    }
}
