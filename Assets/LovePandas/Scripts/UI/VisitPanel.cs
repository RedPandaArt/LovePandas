using System;
using System.Threading.Tasks;
using LovePandas.Core;
using LovePandas.View;
using UnityEngine;
using UnityEngine.UIElements;

namespace LovePandas.UI
{
    /// «В гостях»: на сцене панда партнёра в её наряде. Нежности — жестами или через меню:
    ///  • провести пальцем по голове — погладить, тап по мордочке — поцелуй, тап по груди — обнимашки;
    ///  • зажать палец на панде — вокруг появятся 3 иконки (погладить / поцеловать / обнять).
    /// Панда реагирует сразу, на сервер уходит жест — партнёру прилетит пузырёк с сердечком.
    public class VisitPanel
    {
        const float LongPress = 0.45f;   // с
        const float TapSlop = 24f;       // px панели
        const float PetPath = 140f;      // сколько провести пальцем по голове, чтобы «погладить»

        readonly GameService game;
        readonly CharacterView character;
        readonly Func<Func<Task<string>>, string, Action, Task> run;
        readonly Action<string> toast;
        readonly VisualElement root, layer, touch, menu;
        readonly Label title, hint;

        Vector2 downPos, lastPos;
        float downTime, petPath;
        bool pressing, moved, petDone, menuShown;

        public bool IsOpen { get; private set; }
        public event Action Closed;

        static readonly (string kind, string icon, string label)[] Actions =
        {
            ("pet", "UI/icon_pet", "Погладить"), ("kiss", "UI/icon_kiss", "Поцеловать"), ("hug", "UI/icon_hug", "Обнять"),
        };

        public VisitPanel(VisualElement root, GameService game, CharacterView character,
                          Func<Func<Task<string>>, string, Action, Task> run, Action<string> toast)
        {
            this.root = root;
            this.game = game;
            this.character = character;
            this.run = run;
            this.toast = toast;

            layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.AddToClassList("visit-layer");
            layer.AddToClassList("hidden");
            root.Add(layer);

            // слой касаний на всю сцену
            touch = new VisualElement();
            touch.AddToClassList("visit-touch");
            touch.RegisterCallback<PointerDownEvent>(OnDown);
            touch.RegisterCallback<PointerMoveEvent>(OnMove);
            touch.RegisterCallback<PointerUpEvent>(OnUp);
            layer.Add(touch);

            title = new Label { pickingMode = PickingMode.Ignore };
            title.AddToClassList("visit-title");
            layer.Add(title);
            hint = new Label("Погладь по голове, коснись мордочки или обними.\nЗажми на панде — будут подсказки.") { pickingMode = PickingMode.Ignore };
            hint.AddToClassList("visit-hint");
            layer.Add(hint);

            var home = new Button(Close) { text = "Домой" };
            home.AddToClassList("nav-btn");
            home.AddToClassList("visit-home");
            layer.Add(home);

            // меню по зажатию: три круглые иконки вокруг панды
            menu = new VisualElement { pickingMode = PickingMode.Ignore };
            menu.AddToClassList("visit-menu");
            menu.AddToClassList("hidden");
            foreach (var (kind, icon, label) in Actions)
            {
                var b = new Button(() => { HideMenu(); Do(kind); });
                b.AddToClassList("visit-action");
                var img = new VisualElement { pickingMode = PickingMode.Ignore };
                img.AddToClassList("visit-action-icon");
                img.style.backgroundImage = Resources.Load<Texture2D>(icon) ?? Hearts.Texture;
                b.Add(img);
                var l = new Label(label) { pickingMode = PickingMode.Ignore };
                l.AddToClassList("visit-action-label");
                b.Add(l);
                menu.Add(b);
            }
            layer.Add(menu);
        }

        public void Open()
        {
            if (!game.State.HasPartner) return;
            IsOpen = true;
            title.text = $"В гостях у {game.Partner.name}";
            layer.RemoveFromClassList("hidden");
            HideMenu();
            character.Preview.Clear();
            character.UserYaw = 0;
            character.Apply(game.Partner, game.Item);
            Rig()?.Focus(0.5f, 0.92f);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            layer.AddToClassList("hidden");
            character.Apply(game.Me, game.Item);
            Rig()?.Reset();
            Closed?.Invoke();
        }

        static CameraRig Rig() => Camera.main != null ? Camera.main.GetComponent<CameraRig>() : null;

        /// Каждый кадр: зажатие открывает меню.
        public void Tick()
        {
            if (!IsOpen || !pressing || moved || menuShown) return;
            if (Time.time - downTime >= LongPress && Hit(downPos) != null) ShowMenu(downPos);
        }

        // ---------- Касания ----------

        void OnDown(PointerDownEvent e)
        {
            if (menuShown) { HideMenu(); return; }
            touch.CapturePointer(e.pointerId);
            pressing = true;
            moved = false;
            petDone = false;
            petPath = 0;
            downPos = lastPos = e.position;
            downTime = Time.time;
        }

        void OnMove(PointerMoveEvent e)
        {
            if (!pressing) return;
            Vector2 p = e.position;
            if ((p - downPos).magnitude > TapSlop) moved = true;
            // «погладить»: провести пальцем по голове
            if (!petDone && Near(p, character.HeadPoint, character.HeadRadius * 1.3f))
            {
                petPath += (p - lastPos).magnitude;
                if (petPath >= PetPath) { petDone = true; Do("pet"); }
            }
            lastPos = p;
        }

        void OnUp(PointerUpEvent e)
        {
            touch.ReleasePointer(e.pointerId);
            bool wasPressing = pressing;
            pressing = false;
            if (!wasPressing || moved || menuShown) return;
            var kind = Hit(e.position);
            if (kind != null) Do(kind);
        }

        /// Что под пальцем: мордочка → поцелуй, грудь → обнимашки, голова → погладить.
        string Hit(Vector2 p)
        {
            if (Near(p, character.FacePoint, character.FaceRadius)) return "kiss";
            if (Near(p, character.ChestPoint, character.ChestRadius)) return "hug";
            if (Near(p, character.HeadPoint, character.HeadRadius)) return "pet";
            return null;
        }

        bool Near(Vector2 p, Vector3 world, float radius)
        {
            var cam = Camera.main;
            var panel = root.panel;
            var a = RuntimePanelUtils.CameraTransformWorldToPanel(panel, world, cam);
            var b = RuntimePanelUtils.CameraTransformWorldToPanel(panel, world + cam.transform.right * radius, cam);
            return (p - a).magnitude <= (b - a).magnitude;
        }

        void ShowMenu(Vector2 at)
        {
            menuShown = true;
            pressing = false;
            menu.style.left = at.x;
            menu.style.top = at.y;
            menu.RemoveFromClassList("hidden");
        }

        void HideMenu()
        {
            menuShown = false;
            menu.AddToClassList("hidden");
        }

        // ---------- Жест ----------

        void Do(string kind)
        {
            bool iAmShe = game.Me.characterId == "red_panda_f"; // род глагола — по тому, кто гладит
            string name = game.Partner.name;
            switch (kind)
            {
                case "pet": character.PlayPet(); toast($"Ты погладил{(iAmShe ? "а" : "")} {name} по голове"); break;
                case "kiss": character.PlayKiss(); toast($"Чмок! {name} почувствует поцелуй"); break;
                case "hug": character.PlayHug(); toast($"Обнимашки для {name}"); break;
            }
            _ = run(() => game.Interact(kind), null, null);
        }
    }
}
