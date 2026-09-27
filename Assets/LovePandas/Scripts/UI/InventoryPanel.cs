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
    /// Инвентарь-магазин поверх 3D-доски (View/BoardView): доска спускается сверху слева, в шапке —
    /// медальоны категорий Оружие / Шляпка / Одежка / Ботиночки, в гнёздах — модели вещей.
    /// Купленное — цветное (тап надевает/снимает), некупленное — серое с ценником (тап примеряет, «Купить» покупает).
    /// Примерка — только на клиенте (CharacterView.Preview), снимается при закрытии.
    /// UI здесь — прозрачный слой касаний, ценники/отметки над гнёздами, крестик и кнопка «Купить».
    /// Свайп справа от доски крутит панду; сама панда отходит вправо по неподвижному полу.
    public class InventoryPanel
    {
        const float CharacterViewportX = 0.75f; // куда отходит панда, чтобы доска слева её не закрывала
        const float Shrink = 0.8f;              // и чуть уменьшается «от лап», оставаясь на плитах
        const float TapSlop = 30f; // px панели: больше — это свайп, а не тап

        static readonly Category[] TabOrder = { Category.Weapon, Category.Hat, Category.Clothes, Category.Boots };
        static readonly Color EquippedRing = new Color(1f, 0.55f, 0.25f);
        static readonly Color TryingRing = new Color(0.2f, 0.85f, 0.8f);

        readonly GameService game;
        readonly CharacterView character;
        readonly BoardView board;
        readonly Func<Func<Task<string>>, string, Action, Task> run;
        readonly VisualElement root, hit, labelsLayer, shopLayer;
        readonly Button close, buyHit;
        readonly Label coinText, itemText, buyText;
        // нижняя 3D-плашка магазина: монета и баланс, название примеряемой вещи, кнопка «Купить»
        readonly HangingBoard shop;
        readonly Renderer buyBtn;
        readonly Vector3 buyBtnScale;
        float buyPunch = -10f;
        static readonly Color BuyOn = new Color(1f, 1f, 1f), BuyOff = new Color(0.55f, 0.52f, 0.5f);
        readonly VisualElement[] slotLabels = new VisualElement[BoardView.Slots]; // ценник или отметка под гнездом
        readonly Label[] slotTexts = new Label[BoardView.Slots];
        readonly List<ItemDef> shown = new List<ItemDef>();
        Category category = Category.Hat;
        ItemDef trying;
        Vector2 downPos, lastPos;
        bool dragging;

        public bool IsOpen { get; private set; }
        public event Action Closed;

        public InventoryPanel(VisualElement root, GameService game, CharacterView character,
                              Func<Func<Task<string>>, string, Action, Task> run, Action<string> toast)
        {
            this.root = root;
            this.game = game;
            this.character = character;
            this.run = run;
            board = BoardView.Create(Camera.main);

            // слой касаний на весь экран: тап по доске — выбор, свайп — вращение панды
            hit = new VisualElement();
            hit.AddToClassList("inv-hit");
            hit.AddToClassList("hidden");
            hit.RegisterCallback<PointerDownEvent>(e => { downPos = lastPos = e.position; dragging = false; hit.CapturePointer(e.pointerId); });
            hit.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!hit.HasPointerCapture(e.pointerId)) return;
                if (((Vector2)e.position - downPos).magnitude > TapSlop) dragging = true;
                if (dragging) character.UserYaw -= (e.position.x - lastPos.x) * 0.6f;
                lastPos = e.position;
            });
            hit.RegisterCallback<PointerUpEvent>(e =>
            {
                hit.ReleasePointer(e.pointerId);
                if (!dragging) Tap(e.position);
            });
            root.Add(hit);

            labelsLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            labelsLayer.AddToClassList("inv-labels");
            labelsLayer.AddToClassList("hidden");
            for (int i = 0; i < BoardView.Slots; i++)
            {
                slotLabels[i] = new VisualElement { pickingMode = PickingMode.Ignore };
                slotLabels[i].AddToClassList("inv-slot-label");
                var coin = new VisualElement { pickingMode = PickingMode.Ignore };
                coin.AddToClassList("coin");
                slotLabels[i].Add(coin);
                slotTexts[i] = new Label { pickingMode = PickingMode.Ignore };
                slotLabels[i].Add(slotTexts[i]);
                labelsLayer.Add(slotLabels[i]);
            }
            root.Add(labelsLayer);

            close = AppUI.CloseButton(Close);
            close.AddToClassList("inv-close");
            close.AddToClassList("hidden");
            root.Add(close);

            shop = HangingBoard.Create(Camera.main, "Models/UI/shop_plank", 2.4f, 3.1f, 0.62f,
                                       new Rect(0.5f, 0.075f, 1.0f, 0.14f), false, Materials.Lit(Color.white), fromBelow: true);
            buyBtn = shop.Root.GetComponentsInChildren<Renderer>(true).First(r => r.name == "BuyBtn");
            buyBtnScale = buyBtn.transform.localScale;

            shopLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            shopLayer.AddToClassList("inv-labels");
            shopLayer.AddToClassList("hidden");
            coinText = new Label { pickingMode = PickingMode.Ignore };
            coinText.AddToClassList("shop-coins");
            shopLayer.Add(coinText);
            itemText = new Label { pickingMode = PickingMode.Ignore };
            itemText.AddToClassList("shop-item");
            shopLayer.Add(itemText);
            buyHit = new Button(Buy);
            buyHit.AddToClassList("shop-buy");
            buyText = new Label { pickingMode = PickingMode.Ignore };
            buyHit.Add(buyText);
            shopLayer.Add(buyHit);
            root.Add(shopLayer);
        }

        public void Open()
        {
            IsOpen = true;
            root.AddToClassList("inv-open");
            foreach (var e in new[] { hit, labelsLayer, close, shopLayer }) e.RemoveFromClassList("hidden");
            character.UserYaw = 0;
            character.MoveTo(CharacterViewportX, Shrink);
            board.Show();
            shop.Show();
            Render();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            root.RemoveFromClassList("inv-open");
            foreach (var e in new[] { hit, labelsLayer, close, shopLayer }) e.AddToClassList("hidden");
            shop.Hide();
            trying = null;
            character.Preview.Clear();
            character.UserYaw = 0;
            character.Reapply();
            character.MoveTo(0.5f);
            board.Hide();
            Closed?.Invoke();
        }

        public void Refresh()
        {
            if (IsOpen) Render();
        }


        // ---------- Содержимое доски ----------

        void Render()
        {
            board.SetTab(Array.IndexOf(TabOrder, category));
            shown.Clear();
            shown.AddRange(game.Catalog.Where(i => i.Category == category)
                .OrderByDescending(i => game.Owns(i.id)).ThenBy(i => i.price).Take(BoardView.Slots));

            for (int i = 0; i < BoardView.Slots; i++)
            {
                var item = i < shown.Count ? shown[i] : null;
                var label = slotLabels[i];
                label.RemoveFromClassList("price");
                label.RemoveFromClassList("equipped");
                label.RemoveFromClassList("trying");
                if (item == null)
                {
                    board.SetSlot(i, null, BoardView.SlotState.Owned, null);
                    label.AddToClassList("hidden");
                    continue;
                }
                bool owned = game.Owns(item.id);
                bool previewing = character.Preview.TryGetValue(item.Slot, out var p) && p == item.id;
                bool equipped = !character.Preview.ContainsKey(item.Slot) && game.Me.GetEquipped(item.Slot) == item.id;
                board.SetSlot(i, item, owned ? BoardView.SlotState.Owned : BoardView.SlotState.Locked,
                              previewing ? TryingRing : equipped ? EquippedRing : (Color?)null);

                label.RemoveFromClassList("hidden");
                var text = slotTexts[i];
                if (!owned) { text.text = item.price.ToString(); label.AddToClassList("price"); }
                else if (equipped) { text.text = "надето"; label.AddToClassList("equipped"); }
                else if (previewing) { text.text = "примерка"; label.AddToClassList("trying"); }
                else label.AddToClassList("hidden");
            }

            // нижняя плашка: баланс, что примеряем, «Купить» (тусклая, если нечего купить или не хватает монет)
            coinText.text = game.Me.coins.ToString();
            bool canBuy = trying != null && !game.Owns(trying.id) && game.Me.coins >= trying.price;
            itemText.text = trying != null && !game.Owns(trying.id) ? trying.name : "Тапни вещь, чтобы примерить";
            buyText.text = trying != null && !game.Owns(trying.id) ? $"Купить · {trying.price}" : "Купить";
            buyHit.SetEnabled(canBuy);
            buyBtn.sharedMaterial = Materials.Lit(canBuy ? BuyOn : BuyOff);
        }

        /// Каждый кадр (из AppUI.Update): надписи и крестик едут за доской, пока она спускается и качается.
        public void Tick()
        {
            if (!board.Visible || root.panel == null) return;
            var panel = root.panel;
            var cam = board.Camera;
            for (int i = 0; i < BoardView.Slots; i++)
            {
                var p = RuntimePanelUtils.CameraTransformWorldToPanel(panel, board.SlotLabelWorld(i), cam);
                slotLabels[i].style.left = p.x;
                slotLabels[i].style.top = p.y;
            }
            var c = RuntimePanelUtils.CameraTransformWorldToPanel(panel, board.CloseWorld, cam);
            close.style.left = c.x;
            close.style.top = c.y;

            // нижняя плашка: надписи на пустышках модели; прозрачная кнопка ровно по 3D-кнопке
            if (!shop.Visible) return;
            Vector2 P(string anchor) => RuntimePanelUtils.CameraTransformWorldToPanel(panel, shop.Anchor(anchor).position, cam);
            var cp = P("CoinText"); coinText.style.left = cp.x; coinText.style.top = cp.y;
            var ip = P("ItemText"); itemText.style.left = ip.x; itemText.style.top = ip.y;
            var bp = P("BuyText");
            var b = buyBtn.bounds;
            var bmin = RuntimePanelUtils.CameraTransformWorldToPanel(panel, b.min, cam);
            var bmax = RuntimePanelUtils.CameraTransformWorldToPanel(panel, b.max, cam);
            buyHit.style.left = bp.x;
            buyHit.style.top = bp.y;
            buyHit.style.width = Mathf.Abs(bmax.x - bmin.x);
            buyHit.style.height = Mathf.Abs(bmax.y - bmin.y);
            // 3D-кнопка «проседает» при нажатии
            float since = Time.time - buyPunch;
            float s = since < 0.3f ? 1f - Mathf.Sin(since / 0.3f * Mathf.PI) * 0.12f : 1f;
            buyBtn.transform.localScale = buyBtnScale * s;
        }

        // ---------- Касания ----------

        void Tap(Vector2 pos)
        {
            var panel = root.panel;
            var cam = board.Camera;
            float Radius(Vector3 center, float r)
            {
                var a = RuntimePanelUtils.CameraTransformWorldToPanel(panel, center, cam);
                var b = RuntimePanelUtils.CameraTransformWorldToPanel(panel, center + board.CameraRight * r, cam);
                return (b - a).magnitude;
            }
            bool Near(Vector3 world, float r) =>
                (RuntimePanelUtils.CameraTransformWorldToPanel(panel, world, cam) - pos).magnitude <= Radius(world, r);

            for (int i = 0; i < TabOrder.Length; i++)
                if (Near(board.TabWorld(i), board.TabRadiusWorld * 1.3f))
                {
                    category = TabOrder[i];
                    Render();
                    return;
                }
            for (int i = 0; i < shown.Count; i++)
                if (Near(board.SlotWorld(i), board.SlotRadiusWorld * 1.15f))
                {
                    TapItem(shown[i]);
                    return;
                }
        }

        void TapItem(ItemDef item)
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
            buyPunch = Time.time;
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
