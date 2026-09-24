using System;
using System.Linq;
using System.Threading.Tasks;
using LovePandas.Core;
using LovePandas.View;
using UnityEngine;
using UnityEngine.UIElements;

namespace LovePandas.UI
{
    /// Весь интерфейс MVP: первый вход (профиль → пара), главный экран и шторки
    /// «Доска», «Магазин», «Гардероб» (GDD, раздел 3). Собирается кодом, стили — Resources/UI/App.uss.
    public class AppUI : MonoBehaviour
    {
        enum Screen { None, Board, Shop, Wardrobe }
        enum Stage { Connecting, Offline, Profile, Pair, Home }

        const float PollSeconds = 4f;

        GameService game;
        CharacterView character;
        VisualElement root, home, overlay, sheetHost;
        Label coinsLabel, partnerLabel, toast;
        Screen open = Screen.None;
        Stage stage = Stage.Connecting;
        float toastUntil, nextPoll;
        bool busy, polling;

        public async void Init(GameService game, CharacterView character, PanelSettings panel)
        {
            this.game = game;
            this.character = character;

            var doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            root = doc.rootVisualElement;
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/App"));
            BuildHome();

            game.Changed += Refresh;
            await Connect();
        }

        async Task Connect()
        {
            ShowStage(Stage.Connecting);
            var err = await game.Connect();
            if (err != null) { ShowStage(Stage.Offline); return; }
            Route();
            if (stage == Stage.Home) await ClaimDaily();
        }

        async Task ClaimDaily()
        {
            int bonus = await game.ClaimDaily();
            if (bonus > 0) Toast($"Ежедневный бонус: +{bonus} монет");
        }

        void Update()
        {
            if (toast != null && toastUntil > 0 && Time.time > toastUntil)
            {
                toast.AddToClassList("hidden");
                toastUntil = 0;
            }
            // Опрос нужен, чтобы увидеть действия партнёра. Push-уведомления придут позже.
            if (Time.time >= nextPoll && !polling && !busy && (stage == Stage.Home || stage == Stage.Pair))
                Poll();
        }

        async void Poll()
        {
            polling = true;
            nextPoll = Time.time + PollSeconds;
            await game.Poll();
            polling = false;
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused) nextPoll = 0; // вернулись в приложение — сразу обновиться
        }

        // ---------- Маршрут: что показывать по состоянию ----------

        void Route()
        {
            var s = game.State;
            var next = !s.me.HasProfile ? Stage.Profile : !s.HasPartner ? Stage.Pair : Stage.Home;
            bool enteredHome = next == Stage.Home && stage == Stage.Pair;
            if (next != stage || next == Stage.Pair) ShowStage(next);
            if (enteredHome) _ = ClaimDaily();
        }

        void ShowStage(Stage s)
        {
            stage = s;
            overlay.Clear();
            if (s == Stage.Home)
            {
                overlay.AddToClassList("hidden");
                home.RemoveFromClassList("hidden");
                return;
            }
            CloseSheet();
            home.AddToClassList("hidden");
            overlay.RemoveFromClassList("hidden");

            var card = new VisualElement();
            card.AddToClassList("sheet");
            card.AddToClassList("onboard-card");
            overlay.Add(card);

            switch (s)
            {
                case Stage.Connecting: Title(card, "Подключаемся…"); break;
                case Stage.Offline: BuildOffline(card); break;
                case Stage.Profile: BuildProfile(card); break;
                case Stage.Pair: BuildPair(card); break;
            }
        }

        void BuildOffline(VisualElement card)
        {
            Title(card, "Нет связи с сервером");
            Hint(card, "Проверь интернет и попробуй ещё раз.");
            card.Add(Button("Повторить", "action-btn big", () => _ = Connect()));
        }

        void BuildProfile(VisualElement card)
        {
            Title(card, "Привет! Как тебя зовут?");
            var name = new TextField { maxLength = Economy.MaxNameLength };
            name.textEdition.placeholder = "Имя";
            name.AddToClassList("input");
            card.Add(name);

            Hint(card, "Кто ты?");
            string chosen = null;
            var row = Row("choice-row");
            Button boy = null, girl = null;
            boy = Button("Панда", "choice-btn", () => { chosen = "red_panda_m"; boy.AddToClassList("selected"); girl.RemoveFromClassList("selected"); });
            girl = Button("Пандочка", "choice-btn", () => { chosen = "red_panda_f"; girl.AddToClassList("selected"); boy.RemoveFromClassList("selected"); });
            row.Add(boy);
            row.Add(girl);
            card.Add(row);

            card.Add(Button("Дальше", "action-btn big", () =>
            {
                if (chosen == null) { Toast("Выбери персонажа"); return; }
                Run(() => game.SetProfile(name.value, chosen), null);
            }));
        }

        void BuildPair(VisualElement card)
        {
            var s = game.State;
            if (!s.HasCouple)
            {
                Title(card, "Свяжитесь в пару");
                Hint(card, "Один создаёт пару и отправляет код, второй вводит его у себя.");
                card.Add(Button("Создать пару", "action-btn big", () => Run(game.CreateCouple, null)));

                Hint(card, "Или введи код партнёра:");
                var row = Row("form-row");
                var code = new TextField { maxLength = 6 };
                code.textEdition.placeholder = "Код";
                code.AddToClassList("input");
                code.AddToClassList("input-text");
                row.Add(code);
                row.Add(Button("Войти", "action-btn", () => Run(() => game.JoinCouple(code.value), null)));
                card.Add(row);
                return;
            }

            Title(card, "Ждём партнёра");
            Hint(card, "Отправь этот код своей половинке — пусть введёт его в LovePandas:");
            var codeLabel = new Label(s.couple.inviteCode);
            codeLabel.AddToClassList("invite-code");
            card.Add(codeLabel);
            card.Add(Button("Скопировать код", "action-btn big secondary", () =>
            {
                GUIUtility.systemCopyBuffer = s.couple.inviteCode;
                Toast("Код скопирован");
            }));
        }

        // ---------- Главный экран ----------

        void BuildHome()
        {
            home = new VisualElement();
            home.AddToClassList("root");
            home.AddToClassList("hidden");
            home.pickingMode = PickingMode.Ignore;
            root.Add(home);

            var top = Row("top-bar");
            var coinsChip = Row("chip");
            coinsChip.Add(Coin());
            coinsLabel = new Label();
            coinsChip.Add(coinsLabel);
            top.Add(coinsChip);

            var who = Row("chip");
            partnerLabel = new Label();
            partnerLabel.AddToClassList("player-name");
            who.Add(partnerLabel);
            top.Add(who);
            home.Add(top);

            var nav = Row("nav");
            nav.Add(Button("Доска", "nav-btn", () => OpenSheet(Screen.Board)));
            nav.Add(Button("Магазин", "nav-btn", () => OpenSheet(Screen.Shop)));
            nav.Add(Button("Гардероб", "nav-btn", () => OpenSheet(Screen.Wardrobe)));
            home.Add(nav);

            sheetHost = new VisualElement();
            sheetHost.AddToClassList("sheet-backdrop");
            sheetHost.AddToClassList("hidden");
            sheetHost.RegisterCallback<PointerDownEvent>(e => { if (e.target == sheetHost) CloseSheet(); });
            root.Add(sheetHost);

            overlay = new VisualElement();
            overlay.AddToClassList("sheet-backdrop");
            root.Add(overlay);

            toast = new Label();
            toast.AddToClassList("toast");
            toast.AddToClassList("hidden");
            toast.pickingMode = PickingMode.Ignore;
            root.Add(toast);
        }

        void Refresh()
        {
            var s = game.State;
            coinsLabel.text = s.me.coins.ToString();
            partnerLabel.text = s.HasPartner ? $"{s.me.name} и {s.partner.name}" : s.me.name;
            character.Apply(s.me, game.Item);
            if (stage != Stage.Connecting && stage != Stage.Offline) Route();
            if (open != Screen.None) RenderSheet();
        }

        // ---------- Шторки ----------

        void OpenSheet(Screen s)
        {
            open = s;
            sheetHost.RemoveFromClassList("hidden");
            // Камера рисует только полосу над шторкой (шторка — 62% высоты), панда остаётся видна.
            if (Camera.main != null) Camera.main.rect = new Rect(0, 0.56f, 1, 0.44f);
            RenderSheet();
        }

        void CloseSheet()
        {
            open = Screen.None;
            if (Camera.main != null) Camera.main.rect = new Rect(0, 0, 1, 1);
            sheetHost.AddToClassList("hidden");
            sheetHost.Clear();
        }

        void RenderSheet()
        {
            // Сохраняем прокрутку и черновик формы, чтобы перерисовка после действия или опроса не сбивала их.
            var oldScroll = sheetHost.Q<ScrollView>();
            var offset = oldScroll?.scrollOffset ?? Vector2.zero;
            var draftText = sheetHost.Q<TextField>("quest-text")?.value;
            var draftPrice = sheetHost.Q<TextField>("quest-price")?.value;
            var focused = root.focusController?.focusedElement as VisualElement;
            var focusedName = focused?.name ?? focused?.parent?.name;

            sheetHost.Clear();
            var sheet = new VisualElement();
            sheet.AddToClassList("sheet");
            sheetHost.Add(sheet);

            var header = Row("sheet-header");
            var title = new Label(open == Screen.Board ? "Доска заданий" : open == Screen.Shop ? "Магазин" : "Гардероб");
            title.AddToClassList("sheet-title");
            header.Add(title);
            header.Add(Button("✕", "close-btn", CloseSheet));
            sheet.Add(header);

            var scroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            sheet.Add(scroll);

            switch (open)
            {
                case Screen.Board: RenderBoard(scroll, draftText, draftPrice); break;
                case Screen.Shop: RenderItems(scroll, shop: true); break;
                case Screen.Wardrobe: RenderItems(scroll, shop: false); break;
            }

            scroll.schedule.Execute(() =>
            {
                scroll.scrollOffset = offset;
                if (!string.IsNullOrEmpty(focusedName)) sheetHost.Q<TextField>(focusedName)?.Focus();
            });
        }

        void RenderBoard(VisualElement parent, string draftText, string draftPrice)
        {
            var form = new VisualElement();
            form.AddToClassList("form");
            var text = new TextField { name = "quest-text", maxLength = Economy.MaxQuestTextLength, multiline = true };
            text.textEdition.placeholder = $"Что {game.Partner.name} сделать для тебя?";
            text.AddToClassList("input");
            text.value = draftText ?? "";
            form.Add(text);

            var row = Row("form-row");
            var price = new TextField { name = "quest-price", maxLength = 4 };
            price.textEdition.placeholder = "Цена";
            price.AddToClassList("input");
            price.AddToClassList("input-price");
            price.value = draftPrice ?? "";
            row.Add(price);
            row.Add(new VisualElement { style = { flexGrow = 1 } });
            row.Add(Button("Разместить", "action-btn", () =>
            {
                int.TryParse(price.value, out var reward);
                Run(() => game.CreateQuest(text.value, reward), "Задание на доске", () =>
                {
                    var t = sheetHost.Q<TextField>("quest-text");
                    var p = sheetHost.Q<TextField>("quest-price");
                    if (t != null) t.value = "";
                    if (p != null) p.value = "";
                });
            }));
            form.Add(row);
            parent.Add(form);

            Section(parent, $"Задания от {game.Partner.name}");
            var partnerQuests = game.PartnerQuests.ToList();
            if (partnerQuests.Count == 0) Empty(parent, "Пока пусто — партнёр ещё ничего не поставил");
            foreach (var q in partnerQuests) parent.Add(QuestCard(q, mine: false));

            Section(parent, "Мои задания");
            var myQuests = game.MyQuests.ToList();
            if (myQuests.Count == 0) Empty(parent, "Поставь партнёру первое задание");
            foreach (var q in myQuests) parent.Add(QuestCard(q, mine: true));
        }

        VisualElement QuestCard(Quest q, bool mine)
        {
            var card = new VisualElement();
            card.AddToClassList("quest");
            var text = new Label(q.text);
            text.AddToClassList("quest-text");
            card.Add(text);

            var meta = Row("quest-meta");
            meta.Add(Price(q.reward, "quest-reward"));

            var status = new Label(StatusText(q, mine));
            status.AddToClassList("quest-status");
            meta.Add(status);

            var actions = Row("quest-actions");
            if (mine && q.status == QuestStatus.Created)
                actions.Add(Button("Отменить", "action-btn secondary", () => Run(() => game.CancelQuest(q), "Монеты вернулись")));
            if (mine && q.status == QuestStatus.Done)
            {
                actions.Add(Button("Не то", "action-btn secondary", () => Run(() => game.Reject(q), "Вернули в работу")));
                actions.Add(Button("Принять", "action-btn", () => Run(() => game.Confirm(q), "Спасибо!", character.PlayJoy)));
            }
            if (!mine && q.status == QuestStatus.Created)
                actions.Add(Button("Взять", "action-btn", () => Run(() => game.TakeQuest(q), "Задание взято")));
            if (!mine && q.status == QuestStatus.Taken)
                actions.Add(Button("Готово", "action-btn", () => Run(() => game.MarkDone(q), "Ждём подтверждения")));
            meta.Add(actions);
            card.Add(meta);
            return card;
        }

        static string StatusText(Quest q, bool mine)
        {
            switch (q.status)
            {
                case QuestStatus.Created: return mine ? "ждёт исполнителя" : "свободно";
                case QuestStatus.Taken: return mine ? "в работе" : "ты делаешь";
                case QuestStatus.Done: return mine ? "проверь" : "на проверке";
                default: return "";
            }
        }

        void RenderItems(VisualElement parent, bool shop)
        {
            var items = game.Catalog.Where(i => shop || game.Owns(i.id)).ToList();
            if (!shop && items.Count == 0)
            {
                Empty(parent, "Гардероб пуст — загляни в магазин");
                return;
            }

            var grid = Row("grid");
            foreach (var item in items)
            {
                bool owned = game.Owns(item.id);
                bool equipped = game.Me.GetEquipped(item.Slot) == item.id;

                var cell = new Button();
                cell.AddToClassList("item");
                if (owned) cell.AddToClassList("owned");
                if (equipped) cell.AddToClassList("equipped");

                var swatch = new VisualElement { style = { backgroundColor = CharacterView.RarityColor(item.Rarity) } };
                swatch.AddToClassList("item-swatch");
                cell.Add(swatch);
                var name = new Label(item.name);
                name.AddToClassList("item-name");
                cell.Add(name);
                if (shop && !owned) cell.Add(Price(item.price, "item-price"));
                else
                {
                    var state = new Label(shop ? "куплено" : equipped ? "надето" : "надеть");
                    state.AddToClassList("item-price");
                    cell.Add(state);
                }

                cell.clicked += () =>
                {
                    if (shop && !owned) Run(() => game.Buy(item), $"{item.name} — твоё!", character.PlayJoy);
                    else Run(() => game.ToggleEquip(item), null);
                };
                grid.Add(cell);
            }
            parent.Add(grid);
        }

        /// Для AutoShot: открыть экран по имени.
        public void DebugOpen(string screen)
        {
            if (stage != Stage.Home) return;
            switch (screen)
            {
                case "board": OpenSheet(Screen.Board); break;
                case "shop": OpenSheet(Screen.Shop); break;
                case "wardrobe": OpenSheet(Screen.Wardrobe); break;
                default: CloseSheet(); break;
            }
        }

        // ---------- Мелочи ----------

        /// Выполнить действие на сервере; пока ждём ответа, повторные нажатия игнорируются.
        async void Run(Func<Task<string>> action, string success, Action onSuccess = null)
        {
            if (busy) return;
            busy = true;
            string error;
            try { error = await action(); }
            finally { busy = false; }

            if (error != null) { Toast(error); return; }
            onSuccess?.Invoke();
            if (success != null) Toast(success);
        }

        void Toast(string message)
        {
            toast.text = message;
            toast.RemoveFromClassList("hidden");
            toastUntil = Time.time + 2f;
        }

        static void Title(VisualElement parent, string text)
        {
            var l = new Label(text);
            l.AddToClassList("sheet-title");
            parent.Add(l);
        }

        static void Hint(VisualElement parent, string text)
        {
            var l = new Label(text);
            l.AddToClassList("hint");
            parent.Add(l);
        }

        /// Монетка рисуется элементом: эмодзи 🪙 в шрифте нет.
        static VisualElement Coin()
        {
            var c = new VisualElement();
            c.AddToClassList("coin");
            return c;
        }

        static VisualElement Price(int amount, string cls)
        {
            var row = Row("price");
            row.Add(Coin());
            var l = new Label(amount.ToString());
            l.AddToClassList(cls);
            row.Add(l);
            return row;
        }

        static void Section(VisualElement parent, string text)
        {
            var l = new Label(text);
            l.AddToClassList("section-title");
            parent.Add(l);
        }

        static void Empty(VisualElement parent, string text)
        {
            var l = new Label(text);
            l.AddToClassList("empty");
            parent.Add(l);
        }

        static VisualElement Row(string cls)
        {
            var v = new VisualElement();
            v.AddToClassList(cls);
            return v;
        }

        static Button Button(string text, string classes, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            foreach (var c in classes.Split(' ')) b.AddToClassList(c);
            return b;
        }
    }
}
