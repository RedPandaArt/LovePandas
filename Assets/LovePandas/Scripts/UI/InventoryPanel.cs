using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LovePandas.Core;
using LovePandas.View;
using UnityEngine;
using UnityEngine.UIElements;

namespace LovePandas.UI
{
    /// Инвентарь-магазин: дощечка слева, вкладки Оружие / Шляпка / Одежка / Ботиночки.
    /// Купленное — цветное (тап надевает/снимает), некупленное — серое с ценником (тап примеряет, «Купить» покупает).
    /// Примерка живёт только на клиенте (CharacterView.Preview) и снимается при закрытии.
    /// Справа от дощечки — зона, где панду крутят пальцем. Камера сдвигает панду вправо (CameraRig).
    public class InventoryPanel
    {
        const float CharacterViewportX = 0.74f; // панда у правого края, дощечка занимает левые ~55%
        const float Zoom = 1.25f;               // чуть дальше обычного: панда целиком помещается в правую полосу

        static readonly (Category cat, string title)[] Tabs =
        {
            (Category.Weapon, "Оружие"), (Category.Hat, "Шляпка"), (Category.Clothes, "Одежка"), (Category.Boots, "Ботиночки"),
        };

        readonly GameService game;
        readonly CharacterView character;
        readonly Func<Func<Task<string>>, string, Action, Task> run;
        readonly Action<string> toast;
        readonly VisualElement root, panel, rotateArea, tabsRow, buyBar;
        readonly ScrollView list;
        readonly Label buyLabel;
        readonly Button buyButton;
        Category category = Category.Hat;
        ItemDef trying; // примеряемая некупленная вещь (для кнопки «Купить»)
        float lastPointerX;

        public bool IsOpen { get; private set; }
        public event Action Closed;

        public InventoryPanel(VisualElement root, GameService game, CharacterView character,
                              Func<Func<Task<string>>, string, Action, Task> run, Action<string> toast)
        {
            this.game = game;
            this.character = character;
            this.run = run;
            this.toast = toast;
            this.root = root;

            // Зона вращения панды — всё справа от дощечки
            rotateArea = new VisualElement();
            rotateArea.AddToClassList("inv-rotate");
            rotateArea.AddToClassList("hidden");
            rotateArea.RegisterCallback<PointerDownEvent>(e => { lastPointerX = e.position.x; rotateArea.CapturePointer(e.pointerId); });
            rotateArea.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!rotateArea.HasPointerCapture(e.pointerId)) return;
                character.UserYaw -= (e.position.x - lastPointerX) * 0.6f;
                lastPointerX = e.position.x;
            });
            rotateArea.RegisterCallback<PointerUpEvent>(e => rotateArea.ReleasePointer(e.pointerId));
            root.Add(rotateArea);

            panel = new VisualElement();
            panel.AddToClassList("inv-panel");
            panel.AddToClassList("hidden");
            panel.style.backgroundImage = Resources.Load<Texture2D>("UI/plank");
            root.Add(panel);

            var header = new VisualElement();
            header.AddToClassList("inv-header");
            var title = new Label("Инвентарь");
            title.AddToClassList("inv-title");
            header.Add(title);
            header.Add(AppUI.CloseButton(Close));
            panel.Add(header);

            tabsRow = new VisualElement();
            tabsRow.AddToClassList("inv-tabs");
            foreach (var (cat, name) in Tabs)
            {
                var tab = new Button(() => { category = cat; Render(); }) { text = name };
                tab.AddToClassList("inv-tab");
                tab.userData = cat;
                tabsRow.Add(tab);
            }
            panel.Add(tabsRow);

            list = new ScrollView(ScrollViewMode.Vertical);
            list.AddToClassList("inv-list");
            panel.Add(list);

            buyBar = new VisualElement();
            buyBar.AddToClassList("inv-buy");
            buyBar.AddToClassList("hidden");
            buyLabel = new Label();
            buyLabel.AddToClassList("inv-buy-label");
            buyBar.Add(buyLabel);
            buyButton = new Button(Buy);
            buyButton.AddToClassList("action-btn");
            buyButton.AddToClassList("inv-buy-btn");
            buyBar.Add(buyButton);
            panel.Add(buyBar);
        }

        public void Open()
        {
            IsOpen = true;
            root.AddToClassList("inv-open"); // всплывашки уезжают вправо, чтобы не закрывать дощечку
            panel.RemoveFromClassList("hidden");
            rotateArea.RemoveFromClassList("hidden");
            character.UserYaw = 0;
            Rig()?.Focus(CharacterViewportX, Zoom);
            Render();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            root.RemoveFromClassList("inv-open");
            panel.AddToClassList("hidden");
            rotateArea.AddToClassList("hidden");
            trying = null;
            character.Preview.Clear();
            character.UserYaw = 0;
            character.Reapply();
            Rig()?.Reset();
            Closed?.Invoke();
        }

        /// Состояние с сервера поменялось (покупка, надевание, опрос).
        public void Refresh()
        {
            if (IsOpen) Render();
        }

        static CameraRig Rig() => Camera.main != null ? Camera.main.GetComponent<CameraRig>() : null;

        void Render()
        {
            foreach (var tab in tabsRow.Children())
                tab.EnableInClassList("selected", (Category)tab.userData == category);

            var offset = list.scrollOffset;
            list.Clear();
            var grid = new VisualElement();
            grid.AddToClassList("inv-grid");
            var items = game.Catalog.Where(i => i.Category == category)
                .OrderByDescending(i => game.Owns(i.id)).ThenBy(i => i.price).ToList();
            foreach (var item in items) grid.Add(Card(item));
            list.Add(grid);
            list.schedule.Execute(() => list.scrollOffset = offset);

            bool showBuy = trying != null && !game.Owns(trying.id);
            buyBar.EnableInClassList("hidden", !showBuy);
            if (showBuy)
            {
                buyLabel.text = trying.name;
                buyButton.text = $"Купить · {trying.price}";
                buyButton.SetEnabled(game.Me.coins >= trying.price);
            }
        }

        VisualElement Card(ItemDef item)
        {
            bool owned = game.Owns(item.id);
            bool equipped = !character.Preview.ContainsKey(item.Slot) && game.Me.GetEquipped(item.Slot) == item.id;
            bool previewing = character.Preview.TryGetValue(item.Slot, out var p) && p == item.id;

            var card = new Button(() => Tap(item));
            card.AddToClassList("inv-card");
            if (!owned) card.AddToClassList("locked");
            if (equipped) card.AddToClassList("equipped");
            if (previewing) card.AddToClassList("trying");

            var (color, gray) = IconRenderer.Get(item);
            var icon = new VisualElement();
            icon.AddToClassList("inv-icon");
            if (color != null) icon.style.backgroundImage = owned ? color : gray;
            else icon.style.backgroundColor = CharacterView.RarityColor(item.Rarity);
            card.Add(icon);

            var name = new Label(item.name);
            name.AddToClassList("inv-name");
            card.Add(name);

            if (!owned)
            {
                var tag = new VisualElement();
                tag.AddToClassList("inv-price");
                var coin = new VisualElement();
                coin.AddToClassList("coin");
                tag.Add(coin);
                tag.Add(new Label(item.price.ToString()));
                card.Add(tag);
            }
            else if (equipped || previewing)
            {
                var badge = new Label(equipped ? "надето" : "примерка");
                badge.AddToClassList("inv-badge");
                card.Add(badge);
            }
            return card;
        }

        void Tap(ItemDef item)
        {
            var slot = item.Slot;
            if (game.Owns(item.id))
            {
                // своё — надеть/снять на сервере; примерка в этом слоте больше не нужна
                character.Preview.Remove(slot);
                if (trying != null && trying.Slot == slot) trying = null;
                _ = run(() => game.ToggleEquip(item), null, null);
                character.Reapply();
                Render();
                return;
            }

            // чужое — примерить или снять примерку
            if (character.Preview.TryGetValue(slot, out var current) && current == item.id)
            {
                character.Preview.Remove(slot);
                trying = null;
            }
            else
            {
                character.Preview[slot] = item.id;
                trying = item;
            }
            character.Reapply();
            Render();
        }

        void Buy()
        {
            var item = trying;
            if (item == null) return;
            _ = run(() => game.Buy(item), $"{item.name} — твоё!", () =>
            {
                character.Preview.Remove(item.Slot);
                trying = null;
                character.PlayJoy();
                Render();
            });
        }
    }
}
