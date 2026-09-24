using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LovePandas.Core
{
    /// Состояние игры на клиенте. Все правила и монеты — на сервере (server/src/game.ts);
    /// здесь только вызовы API и удобные выборки для UI. Методы возвращают null или текст ошибки.
    public class GameService
    {
        [Serializable] class ProfileBody { public string name; public string characterId; }
        [Serializable] class CodeBody { public string code; }
        [Serializable] class QuestBody { public string text; public int reward; }
        [Serializable] class ItemBody { public string itemId; }

        readonly ApiClient api;
        Dictionary<string, ItemDef> catalog = new Dictionary<string, ItemDef>();

        public GameState State { get; private set; }
        public event Action Changed;

        public GameService(ApiClient api) => this.api = api;

        public Player Me => State.me;
        public Player Partner => State.partner;
        public IReadOnlyCollection<ItemDef> Catalog => catalog.Values;

        public IEnumerable<Quest> MyQuests => State.quests.Where(q => q.authorId == Me.id);
        public IEnumerable<Quest> PartnerQuests => State.quests.Where(q => q.authorId != Me.id);

        public ItemDef Item(string id) => id != null && catalog.TryGetValue(id, out var d) ? d : null;
        public bool Owns(string itemId) => Me.inventory.Contains(itemId);

        // ---------- Синхронизация ----------

        /// Первый запуск: регистрация при необходимости и загрузка состояния.
        public async Task<string> Connect()
        {
            if (!api.HasToken)
            {
                var err = await api.Register();
                if (err != null) return err;
            }
            var r = await api.GetState(-1);
            if (r.error == "Нужен вход")
            {
                api.ForgetToken();
                return await Connect();
            }
            return Apply(r);
        }

        /// Опрос: сервер отвечает 304, если у пары ничего не менялось.
        public async Task Poll()
        {
            if (State == null) return;
            var r = await api.GetState(State.version);
            if (r != null && r.error == null) Apply(r);
        }

        public async Task<int> ClaimDaily()
        {
            var r = await api.Post("/daily");
            Apply(r);
            return r.error == null ? r.bonus : 0;
        }

        // ---------- Профиль и пара ----------

        public Task<string> SetProfile(string name, string characterId) =>
            Call("/profile", new ProfileBody { name = name, characterId = characterId });

        public Task<string> CreateCouple() => Call("/couple/create");
        public Task<string> JoinCouple(string code) => Call("/couple/join", new CodeBody { code = code });

        // ---------- Задания ----------

        public Task<string> CreateQuest(string text, int reward) =>
            Call("/quests", new QuestBody { text = text, reward = reward });

        public Task<string> CancelQuest(Quest q) => Call($"/quests/{q.id}/cancel");
        public Task<string> TakeQuest(Quest q) => Call($"/quests/{q.id}/take");
        public Task<string> MarkDone(Quest q) => Call($"/quests/{q.id}/done");
        public Task<string> Confirm(Quest q) => Call($"/quests/{q.id}/confirm");
        public Task<string> Reject(Quest q) => Call($"/quests/{q.id}/reject");

        // ---------- Магазин ----------

        public Task<string> Buy(ItemDef item) => Call("/shop/buy", new ItemBody { itemId = item.id });
        public Task<string> ToggleEquip(ItemDef item) => Call("/wardrobe/toggle", new ItemBody { itemId = item.id });

        // ---------- Внутреннее ----------

        async Task<string> Call(string path, object body = null) => Apply(await api.Post(path, body));

        string Apply(ApiResponse r)
        {
            if (r.error != null && r.error != "") return r.error;
            if (r.state == null) return ApiClient.OfflineError;
            State = r.state;
            catalog = State.catalog.ToDictionary(i => i.id);
            Changed?.Invoke();
            return null;
        }
    }
}
