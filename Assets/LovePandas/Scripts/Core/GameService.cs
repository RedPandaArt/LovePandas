using System;
using System.Collections.Generic;
using System.Linq;

namespace LovePandas.Core
{
    /// Все операции с монетами и заданиями. Сейчас выполняются на устройстве поверх локального
    /// хранилища; с Firebase эта же логика переедет в Cloud Functions (GDD 8.1).
    /// Методы возвращают null при успехе или текст ошибки для игрока.
    public class GameService
    {
        readonly IGameStore store;
        public SaveData Data { get; private set; }
        public event Action Changed;

        public GameService(IGameStore store)
        {
            this.store = store;
            var loaded = store.Load();
            Data = loaded?.couple != null ? loaded : CreateLocalCouple();
        }

        public Player Me => Data.couple.players.First(p => p.id == Data.activePlayerId);
        public Player Partner => Data.couple.players.First(p => p.id != Data.activePlayerId);

        public IEnumerable<Quest> MyQuests => Data.couple.quests
            .Where(q => q.authorId == Me.id && IsOpen(q)).OrderByDescending(q => q.createdTicks);

        public IEnumerable<Quest> PartnerQuests => Data.couple.quests
            .Where(q => q.authorId == Partner.id && IsOpen(q)).OrderByDescending(q => q.createdTicks);

        static bool IsOpen(Quest q) =>
            q.status == QuestStatus.Created || q.status == QuestStatus.Taken || q.status == QuestStatus.Done;

        // ---------- Экономика ----------

        /// Возвращает начисленный бонус (0, если сегодня уже был).
        public int ClaimDailyBonus(DateTime now)
        {
            var last = new DateTime(Me.lastDailyBonusTicks);
            if (Me.lastDailyBonusTicks != 0 && last.Date >= now.Date) return 0;
            Me.lastDailyBonusTicks = now.Ticks;
            Me.coins += Economy.DailyBonus;
            Commit();
            return Economy.DailyBonus;
        }

        // ---------- Задания: Created → Taken → Done → Confirmed ----------

        public string CreateQuest(string text, int reward)
        {
            text = text?.Trim();
            if (string.IsNullOrEmpty(text)) return "Напиши, что нужно сделать";
            if (text.Length > Economy.MaxQuestTextLength) return "Слишком длинное задание";
            if (reward < Economy.MinReward || reward > Economy.MaxReward) return "Цена от 1 до 9999";
            if (Me.coins < reward) return "Не хватает монет";

            Me.coins -= reward; // эскроу: монеты заморожены в задании
            Data.couple.quests.Add(new Quest
            {
                id = Guid.NewGuid().ToString("N"),
                authorId = Me.id,
                text = text,
                reward = reward,
                status = QuestStatus.Created,
                createdTicks = DateTime.UtcNow.Ticks,
            });
            Commit();
            return null;
        }

        public string CancelQuest(Quest q)
        {
            if (q.authorId != Me.id) return "Это не твоё задание";
            if (q.status != QuestStatus.Created) return "Задание уже взяли";
            q.status = QuestStatus.Cancelled;
            Me.coins += q.reward;
            Commit();
            return null;
        }

        public string TakeQuest(Quest q)
        {
            if (q.authorId == Me.id) return "Своё задание брать нельзя";
            if (q.status != QuestStatus.Created) return "Задание уже взято";
            q.status = QuestStatus.Taken;
            q.assigneeId = Me.id;
            Commit();
            return null;
        }

        public string MarkDone(Quest q)
        {
            if (q.assigneeId != Me.id || q.status != QuestStatus.Taken) return "Нельзя отметить";
            q.status = QuestStatus.Done;
            Commit();
            return null;
        }

        public string Confirm(Quest q)
        {
            if (q.authorId != Me.id || q.status != QuestStatus.Done) return "Нечего подтверждать";
            q.status = QuestStatus.Confirmed;
            Find(q.assigneeId).coins += q.reward;
            Commit();
            return null;
        }

        /// Автор не согласен, что сделано: задание возвращается исполнителю в работу.
        public string Reject(Quest q)
        {
            if (q.authorId != Me.id || q.status != QuestStatus.Done) return "Нечего отклонять";
            q.status = QuestStatus.Taken;
            Commit();
            return null;
        }

        // ---------- Магазин и гардероб ----------

        public bool Owns(string itemId) => Me.inventory.Contains(itemId);

        public string Buy(ItemDef item)
        {
            if (Owns(item.id)) return "Уже куплено";
            if (item.rarity == Rarity.BossExclusive) return "Только с босса";
            if (Me.coins < item.price) return "Не хватает монет";
            Me.coins -= item.price;
            Me.inventory.Add(item.id);
            Commit();
            return null;
        }

        public void ToggleEquip(ItemDef item)
        {
            if (!Owns(item.id)) return;
            var current = Me.equipped.FirstOrDefault(e => e.slot == item.slot);
            if (current != null && current.itemId == item.id)
                Me.equipped.Remove(current);
            else if (current != null)
                current.itemId = item.id;
            else
                Me.equipped.Add(new EquippedEntry { slot = item.slot, itemId = item.id });
            Commit();
        }

        // ---------- Локальный режим: два игрока на одном телефоне ----------

        public void SwitchPlayer()
        {
            Data.activePlayerId = Partner.id;
            Commit();
        }

        Player Find(string id) => Data.couple.players.First(p => p.id == id);

        void Commit()
        {
            store.Save(Data);
            Changed?.Invoke();
        }

        static SaveData CreateLocalCouple()
        {
            var a = new Player { id = "p1", name = "Панда", characterId = "red_panda_m", coins = Economy.StartCoins };
            var b = new Player { id = "p2", name = "Пандочка", characterId = "red_panda_f", coins = Economy.StartCoins };
            return new SaveData
            {
                couple = new Couple { id = "local", inviteCode = "LOCAL", players = { a, b } },
                activePlayerId = a.id,
            };
        }
    }
}
