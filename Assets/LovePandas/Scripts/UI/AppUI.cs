using System;
using System.Linq;
using System.Threading.Tasks;
using LovePandas.Core;
using LovePandas.View;
using UnityEngine;
using UnityEngine.UIElements;

namespace LovePandas.UI
{
    /// Весь интерфейс MVP: первый вход (профиль → пара), главный экран с двумя кнопками —
    /// «Инвентарь» (InventoryPanel: дощечка слева, примерка и покупка) и «Заказы» (шторка с доской заданий).
    /// Собирается кодом, стили — Resources/UI/App.uss.
    public class AppUI : MonoBehaviour
    {
        enum Stage { Connecting, Offline, Profile, Pair, Home }

        const float PollSeconds = 4f;

        GameService game;
        CharacterView character;
        VisualElement root, home, overlay;
        Label coinsLabel, partnerLabel, toast;
        VisualElement nav;
        InventoryPanel inventory;
        OrdersPanel orders;
        VisitPanel visit;
        InboxBubble inbox;
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
            inventory = new InventoryPanel(root, game, character, Run, Toast);
            inventory.Closed += () => nav.RemoveFromClassList("hidden");
            orders = new OrdersPanel(root, game, character, Run);
            orders.Closed += () => nav.RemoveFromClassList("hidden");
            visit = new VisitPanel(root, game, character, Run, Toast);
            visit.Closed += () => { nav.RemoveFromClassList("hidden"); home.RemoveFromClassList("visiting"); };
            inbox = new InboxBubble(root, game, character, Run);
            overlay.BringToFront();
            toast.BringToFront();

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
            inventory?.Tick();
            orders?.Tick();
            visit?.Tick();
            inbox?.Tick(stage == Stage.Home && !inventory.IsOpen && !orders.IsOpen && !visit.IsOpen);
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
            inventory?.Close();
            orders?.Close();
            visit?.Close();
            inbox?.Hide();
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

            // плашка партнёра — кнопка «В гости»: посмотреть его панду и погладить/поцеловать/обнять
            var who = Row("chip");
            who.AddToClassList("visit-chip");
            var houseIcon = new VisualElement { pickingMode = PickingMode.Ignore };
            houseIcon.AddToClassList("visit-chip-heart");
            houseIcon.style.backgroundImage = Hearts.Texture;
            who.Add(houseIcon);
            partnerLabel = new Label { pickingMode = PickingMode.Ignore };
            partnerLabel.AddToClassList("player-name");
            who.Add(partnerLabel);
            who.RegisterCallback<ClickEvent>(_ => OpenVisit());
            top.Add(who);
            home.Add(top);

            nav = Row("nav");
            nav.Add(Button("Инвентарь", "nav-btn", OpenInventory));
            nav.Add(Button("Заказы", "nav-btn", OpenOrders));
            home.Add(nav);


            overlay = new VisualElement();
            overlay.AddToClassList("sheet-backdrop");
            root.Add(overlay);

#if LP_DEV
            var dev = new Label("DEV · сервер на ПК");
            dev.AddToClassList("dev-badge");
            dev.pickingMode = PickingMode.Ignore;
            root.Add(dev);
#endif

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
            partnerLabel.text = s.HasPartner ? $"В гости к {s.partner.name}" : s.me.name;
            // в гостях на сцене панда партнёра
            character.Apply(visit != null && visit.IsOpen && s.HasPartner ? s.partner : s.me, game.Item);
            if (stage != Stage.Connecting && stage != Stage.Offline) Route();
            inventory?.Refresh();
            orders?.Refresh();
        }

        // ---------- Экраны поверх главного ----------

        void OpenOrders()
        {
            if (stage != Stage.Home) return;
            inventory.Close();
            nav.AddToClassList("hidden");
            orders.Open();
        }

        void OpenVisit()
        {
            if (stage != Stage.Home || !game.State.HasPartner) return;
            inventory.Close();
            orders.Close();
            inbox.Hide();
            nav.AddToClassList("hidden");
            home.AddToClassList("visiting"); // плашки сверху прячем: в гостях своя шапка
            visit.Open();
        }

        void OpenInventory()
        {
            if (stage != Stage.Home) return;
            orders.Close();
            nav.AddToClassList("hidden");
            inventory.Open();
        }

        /// Крестик из двух повёрнутых полосок: символа ✕ нет в системном шрифте некоторых телефонов (Samsung).
        public static Button CloseButton(Action onClick)
        {
            var b = new Button(onClick);
            b.AddToClassList("close-btn");
            foreach (var cls in new[] { "x-bar-a", "x-bar-b" })
            {
                var bar = new VisualElement { pickingMode = PickingMode.Ignore };
                bar.AddToClassList("x-bar");
                bar.AddToClassList(cls);
                b.Add(bar);
            }
            return b;
        }

        /// Для AutoShot: подключились и дошли до главного экрана.
        public bool IsHome => stage == Stage.Home;

        /// Для AutoShot: открыть экран по имени.
        public void DebugOpen(string screen)
        {
            if (stage != Stage.Home) return;
            switch (screen)
            {
                case "board": OpenOrders(); break;
                case "board-new": OpenOrders(); orders.OpenModal(); break;
                case "visit": OpenVisit(); break;
                case "inventory": OpenInventory(); break;
                default: inventory.Close(); orders.Close(); break;
            }
        }

        // ---------- Мелочи ----------

        /// Выполнить действие на сервере; пока ждём ответа, повторные нажатия игнорируются.
        async Task Run(Func<Task<string>> action, string success, Action onSuccess = null)
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
