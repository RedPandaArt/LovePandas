using System;
using System.Collections.Generic;

namespace LovePandas.Core
{
    // Зеркало JSON сервера (server/src/game.ts → state()). Поля — в camelCase, как в ответе,
    // потому что JsonUtility сопоставляет по имени. Перечисления приходят строками.

    public enum Slot { Head, Face, Neck, Body, Legs, Hands, Tail, Back }

    public enum Rarity { Common, Uncommon, Rare, Legendary, BossExclusive }

    public static class QuestStatus
    {
        public const string Created = "Created", Taken = "Taken", Done = "Done";
    }

    [Serializable]
    public class EquippedEntry
    {
        public string slot;
        public string itemId;
    }

    [Serializable]
    public class Player
    {
        public string id;
        public string name;
        public string characterId;
        public int coins;                                   // только у себя
        public List<string> inventory = new List<string>(); // только у себя
        public List<EquippedEntry> equipped = new List<EquippedEntry>();

        public bool Exists => !string.IsNullOrEmpty(id);
        public bool HasProfile => !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(characterId);

        public string GetEquipped(Slot slot)
        {
            var s = slot.ToString();
            foreach (var e in equipped)
                if (e.slot == s) return e.itemId;
            return null;
        }
    }

    [Serializable]
    public class Quest
    {
        public string id;
        public string authorId;
        public string assigneeId;
        public string text;
        public int reward;
        public string status;
        public long createdAt;
    }

    [Serializable]
    public class CoupleInfo
    {
        public string inviteCode;
    }

    [Serializable]
    public class ItemDef
    {
        public string id;
        public string name;
        public string slot;
        public string rarity;
        public int price;

        public Slot Slot => Enum.TryParse(slot, out Slot s) ? s : Slot.Head;
        public Rarity Rarity => Enum.TryParse(rarity, out Rarity r) ? r : Rarity.Common;
    }

    [Serializable]
    public class GameState
    {
        public int version;
        public Player me;
        public Player partner;
        public CoupleInfo couple;
        public List<Quest> quests = new List<Quest>();
        public List<ItemDef> catalog = new List<ItemDef>();

        public bool HasCouple => couple != null && !string.IsNullOrEmpty(couple.inviteCode);
        public bool HasPartner => partner != null && partner.Exists;
    }

    [Serializable]
    public class ApiResponse
    {
        public string error;
        public int bonus;
        public GameState state;
    }

    [Serializable]
    public class RegisterResponse
    {
        public string token;
        public string playerId;
    }
}
