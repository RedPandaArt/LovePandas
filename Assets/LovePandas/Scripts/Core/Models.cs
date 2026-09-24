using System;
using System.Collections.Generic;

namespace LovePandas.Core
{
    public enum QuestStatus { Created, Taken, Done, Confirmed, Rejected, Cancelled }

    public enum Slot { Head, Face, Neck, Body, Legs, Hands, Tail, Back }

    public enum Rarity { Common, Uncommon, Rare, Legendary, BossExclusive }

    [Serializable]
    public class EquippedEntry
    {
        public Slot slot;
        public string itemId;
    }

    [Serializable]
    public class Player
    {
        public string id;
        public string name;
        public string characterId;
        public int coins;
        public List<string> inventory = new List<string>();
        public List<EquippedEntry> equipped = new List<EquippedEntry>();
        public long lastDailyBonusTicks;

        public string GetEquipped(Slot slot)
        {
            foreach (var e in equipped)
                if (e.slot == slot) return e.itemId;
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
        public QuestStatus status;
        public long createdTicks;
    }

    [Serializable]
    public class Couple
    {
        public string id;
        public string inviteCode;
        public List<Player> players = new List<Player>();
        public List<Quest> quests = new List<Quest>();
    }

    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public Couple couple;
        public string activePlayerId;
    }

    public class ItemDef
    {
        public string id;
        public string name;
        public Slot slot;
        public Rarity rarity;
        public int price;
    }
}
