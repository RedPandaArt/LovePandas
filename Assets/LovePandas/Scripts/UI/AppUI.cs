using System;
using System.Linq;
using LovePandas.Core;
using LovePandas.View;
using UnityEngine;
using UnityEngine.UIElements;

namespace LovePandas.UI
{
    /// Весь интерфейс MVP: главный экран + шторки «Доска», «Магазин», «Гардероб» (GDD, раздел 3).
    /// Собирается кодом, стили — Resources/UI/App.uss.
    public class AppUI : MonoBehaviour
    {
        enum Screen { None, Board, Shop, Wardrobe }

        GameService game;
        CharacterView character;
        VisualElement root, sheetHost;
        Label coinsLabel, nameLabel, toast;
        Screen open = Screen.None;
        float toastUntil;

        public void Init(GameService game, CharacterView character, PanelSettings panel)
        {
            this.game = game;
            this.character = character;

            var doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            root = doc.rootVisualElement;
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/App"));
            BuildHome();

            game.Changed += Refresh;
            Refresh();

            int bonus = game.ClaimDailyBonus(DateTime.Now);
            if (bonus > 0) Toast($"Ежедневный бонус: +{bonus} монет");
        }

        void Update()
        {
            if (toast != null && toastUntil > 0 && Time.time > toastUntil)
            {
                toast.AddToClassList("hidden");
                toastUntil = 0;
            }
        }

        // ---------- Главный экран ----------

        void BuildHome()
        {
            var home = new VisualElement();
            home.AddToClassList("root");
            home.pickingMode = PickingMode.Ignore;
            root.Add(home);

            var top = Row("top-bar");
            var coinsChip = Row("chip");
            coinsChip.Add(Coin());
            coinsLabel = new Label();
            coinsChip.Add(coinsLabel);
            top.Add(coinsChip);

            var who = Row("chip");
            nameLabel = new Label();
            nameLabel.AddToClassList("player-name");
            who.Add(nameLabel);
            var sw = Button("сменить", "switch-btn", () => { game.SwitchPlayer(); CloseSheet(); });
            sw.tooltip = "Сменить игрока (тест на одном телефоне)";
            who.Add(sw);
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

            toast = new Label();
            toast.AddToClassList("toast");
            toast.AddToClassList("hidden");
            toast.pickingMode = PickingMode.Ignore;
            root.Add(toast);
        }

        void Refresh()
        {
            coinsLabel.text = game.Me.coins.ToString();
            nameLabel.text = game.Me.name;
            character.Apply(game.Me);
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
            // Сохраняем прокрутку и черновик формы, чтобы перерисовка после действия не сбивала их.
            var oldScroll = sheetHost.Q<ScrollView>();
            var offset = oldScroll?.scrollOffset ?? Vector2.zero;
            var draftText = sheetHost.Q<TextField>("quest-text")?.value;
            var draftPrice = sheetHost.Q<TextField>("quest-price")?.value;

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

            scroll.schedule.Execute(() => scroll.scrollOffset = offset);
        }

        void RenderBoard(VisualElement parent, string draftText, string draftPrice)
        {
            // Форма нового задания
            var form = new VisualElement();
            form.AddToClassList("form");
            var text = new TextField { name = "quest-text", maxLength = Economy.MaxQuestTextLength, multiline = true };
            text.textEdition.placeholder = "Что сделать для тебя?";
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
                var err = game.CreateQuest(text.value, reward);
                if (err != null) { Toast(err); return; }
                text.value = ""; price.value = "";
                Toast("Задание на доске");
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
                actions.Add(Button("Отменить", "action-btn secondary", () => Do(game.CancelQuest(q), "Монеты вернулись")));
            if (mine && q.status == QuestStatus.Done)
            {
                actions.Add(Button("Не то", "action-btn secondary", () => Do(game.Reject(q), "Вернули в работу")));
                actions.Add(Button("Принять", "action-btn", () => { Do(game.Confirm(q), "Спасибо! ❤️"); character.PlayJoy(); }));
            }
            if (!mine && q.status == QuestStatus.Created)
                actions.Add(Button("Взять", "action-btn", () => Do(game.TakeQuest(q), "Задание взято")));
            if (!mine && q.status == QuestStatus.Taken)
                actions.Add(Button("Готово", "action-btn", () => Do(game.MarkDone(q), "Ждём подтверждения")));
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
            var items = shop ? ItemCatalog.All : ItemCatalog.All.Where(i => game.Owns(i.id)).ToList();
            if (!shop && items.Count == 0)
            {
                Empty(parent, "Гардероб пуст — загляни в магазин");
                return;
            }

            var grid = Row("grid");
            foreach (var item in items)
            {
                bool owned = game.Owns(item.id);
                bool equipped = game.Me.GetEquipped(item.slot) == item.id;

                var cell = new Button();
                cell.AddToClassList("item");
                if (owned) cell.AddToClassList("owned");
                if (equipped) cell.AddToClassList("equipped");

                var swatch = new VisualElement { style = { backgroundColor = CharacterView.RarityColor(item.rarity) } };
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
                    if (shop && !owned)
                    {
                        var err = game.Buy(item);
                        if (err != null) { Toast(err); return; }
                        game.ToggleEquip(item); // купил — сразу примерил
                        character.PlayJoy();
                        Toast($"{item.name} — твоё!");
                    }
                    else game.ToggleEquip(item);
                };
                grid.Add(cell);
            }
            parent.Add(grid);
        }

        /// Для AutoShot: открыть экран по имени.
        public void DebugOpen(string screen)
        {
            switch (screen)
            {
                case "board": OpenSheet(Screen.Board); break;
                case "shop": OpenSheet(Screen.Shop); break;
                case "wardrobe": OpenSheet(Screen.Wardrobe); break;
                default: CloseSheet(); break;
            }
        }

        // ---------- Мелочи ----------

        void Do(string error, string success)
        {
            Toast(error ?? success);
        }

        void Toast(string message)
        {
            toast.text = message;
            toast.RemoveFromClassList("hidden");
            toastUntil = Time.time + 2f;
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
