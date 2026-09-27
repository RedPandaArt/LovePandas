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
    /// Заказы поверх 3D-доски (View/QuestBoardView): доска опускается на весь экран, панда прячется.
    /// На листках — текст, награда, статус и кнопки действий; внизу «Новый заказ» открывает дощечку-модалку
    /// с полем текста и ценой. Все надписи каждый кадр едут за листками и наклоняются вместе с ними.
    public class OrdersPanel
    {
        readonly GameService game;
        readonly CharacterView character;
        readonly QuestBoardView view;
        readonly Func<Func<Task<string>>, string, Action, Task> run;
        readonly VisualElement root, layer, modalLayer;
        readonly Label title, headerL, headerR, emptyL, emptyR, modalTitle;
        readonly Button close, newButton, modalClose, submit;
        readonly TextField questText, questPrice;
        readonly NoteUI[] left = new NoteUI[QuestBoardView.PerColumn], right = new NoteUI[QuestBoardView.PerColumn];

        class NoteUI
        {
            public VisualElement box;
            public Label text, reward, status;
            public VisualElement actions, coin;
            public string questId, stateKey;
        }

        public bool IsOpen { get; private set; }
        public event Action Closed;

        public OrdersPanel(VisualElement root, GameService game, CharacterView character,
                           Func<Func<Task<string>>, string, Action, Task> run)
        {
            this.root = root;
            this.game = game;
            this.character = character;
            this.run = run;
            view = new QuestBoardView(Camera.main);

            // слой доски: ловит касания, чтобы они не проходили к главному экрану
            layer = new VisualElement();
            layer.AddToClassList("orders-layer");
            layer.AddToClassList("hidden");
            root.Add(layer);

            title = Text(layer, "Доска заказов", "orders-title");
            headerL = Text(layer, "Для тебя", "orders-header");
            headerR = Text(layer, "Твои заказы", "orders-header");
            emptyL = Text(layer, "Партнёр пока ничего не просил", "orders-empty");
            emptyR = Text(layer, "Повесь первый заказ", "orders-empty");
            for (int k = 0; k < QuestBoardView.PerColumn; k++)
            {
                left[k] = MakeNote();
                right[k] = MakeNote();
            }

            newButton = new Button(OpenModal) { text = "Новый заказ" };
            newButton.AddToClassList("orders-new");
            layer.Add(newButton);
            close = AppUI.CloseButton(Close);
            close.AddToClassList("inv-close");
            layer.Add(close);

            // модалка «Новый заказ»
            modalLayer = new VisualElement();
            modalLayer.AddToClassList("orders-modal");
            modalLayer.AddToClassList("hidden");
            root.Add(modalLayer);
            modalTitle = Text(modalLayer, "Новый заказ", "orders-title");
            questText = new TextField { name = "quest-text", maxLength = Economy.MaxQuestTextLength, multiline = true };
            questText.AddToClassList("input");
            questText.AddToClassList("modal-field");
            modalLayer.Add(questText);
            questPrice = new TextField { name = "quest-price", maxLength = 4 };
            questPrice.textEdition.placeholder = "Цена";
            questPrice.AddToClassList("input");
            questPrice.AddToClassList("modal-price");
            modalLayer.Add(questPrice);
            submit = new Button(Submit) { text = "Повесить" };
            submit.AddToClassList("action-btn");
            submit.AddToClassList("modal-submit");
            modalLayer.Add(submit);
            modalClose = AppUI.CloseButton(CloseModal);
            modalClose.AddToClassList("inv-close");
            modalLayer.Add(modalClose);
        }

        // ---------- Открытие ----------

        public void Open()
        {
            IsOpen = true;
            layer.RemoveFromClassList("hidden");
            character.gameObject.SetActive(false); // на доске панды нет
            view.Board.Show();
            Render();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            CloseModal();
            layer.AddToClassList("hidden");
            view.Board.Hide();
            character.gameObject.SetActive(true);
            Closed?.Invoke();
        }

        public void OpenModal()
        {
            questText.textEdition.placeholder = $"Что {game.Partner.name} сделать для тебя?";
            modalLayer.RemoveFromClassList("hidden");
            layer.AddToClassList("hidden"); // надписи листков не должны просвечивать сквозь дощечку
            view.Modal.Show();
        }

        void CloseModal()
        {
            modalLayer.AddToClassList("hidden");
            if (IsOpen) layer.RemoveFromClassList("hidden");
            view.Modal.Hide();
        }

        void Submit()
        {
            int.TryParse(questPrice.value, out var reward);
            _ = run(() => game.CreateQuest(questText.value, reward), "Заказ висит на доске", () =>
            {
                questText.value = "";
                questPrice.value = "";
                CloseModal();
            });
        }

        public void Refresh()
        {
            if (IsOpen) Render();
        }

        // ---------- Листки ----------

        void Render()
        {
            // сначала то, где нужно действие: у партнёра — свободные и взятые мной, у себя — на проверку
            var partner = game.PartnerQuests.OrderBy(q => q.status == QuestStatus.Done ? 1 : 0).Take(QuestBoardView.PerColumn).ToList();
            var mine = game.MyQuests.OrderBy(q => q.status == QuestStatus.Done ? 0 : 1).Take(QuestBoardView.PerColumn).ToList();
            view.SetNotes(partner, mine);
            Bind(left, view.Left, mine: false);
            Bind(right, view.Right, mine: true);
            emptyL.EnableInClassList("hidden", partner.Count > 0);
            emptyR.EnableInClassList("hidden", mine.Count > 0);
        }

        void Bind(NoteUI[] uis, QuestBoardView.Note[] notes, bool mine)
        {
            for (int k = 0; k < uis.Length; k++)
            {
                var ui = uis[k];
                var note = notes[k];
                ui.box.EnableInClassList("hidden", note == null);
                if (note == null) { ui.questId = null; continue; }
                var q = note.quest;
                string key = q.id + q.status + mine;
                if (ui.stateKey == key) continue;
                ui.stateKey = key;
                ui.questId = q.id;
                ui.text.text = q.text;
                ui.reward.text = q.reward.ToString();
                ui.status.text = StatusText(q, mine);
                ui.actions.Clear();
                if (mine && q.status == QuestStatus.Created)
                    ui.actions.Add(Act("Снять", true, () => run(() => game.CancelQuest(q), "Монеты вернулись", null)));
                if (mine && q.status == QuestStatus.Done)
                {
                    ui.actions.Add(Act("Не то", true, () => run(() => game.Reject(q), "Вернули в работу", null)));
                    ui.actions.Add(Act("Принять", false, () => run(() => game.Confirm(q), "Спасибо!", character.PlayJoy)));
                }
                if (!mine && q.status == QuestStatus.Created)
                    ui.actions.Add(Act("Взять", false, () => run(() => game.TakeQuest(q), "Заказ взят", null)));
                if (!mine && q.status == QuestStatus.Taken)
                    ui.actions.Add(Act("Готово", false, () => run(() => game.MarkDone(q), "Ждём проверки", null)));
            }
        }

        static string StatusText(Quest q, bool mine)
        {
            switch (q.status)
            {
                case QuestStatus.Created: return mine ? "ждёт исполнителя" : "свободен";
                case QuestStatus.Taken: return mine ? "в работе" : "ты делаешь";
                case QuestStatus.Done: return mine ? "проверь!" : "на проверке";
                default: return "";
            }
        }

        // ---------- Привязка к 3D каждый кадр ----------

        public void Tick()
        {
            view.Tick();
            if (!view.Board.Visible || root.panel == null) return;
            var panel = root.panel;
            var cam = view.Board.Camera;
            Vector2 P(Vector3 w) => RuntimePanelUtils.CameraTransformWorldToPanel(panel, w, cam);
            float Px(Vector3 w, float worldLen) => (P(w + cam.transform.right * worldLen) - P(w)).magnitude;

            var b = view.Board;
            Place(title, P(b.Anchor("Title").position));
            Place(headerL, P(b.Anchor("Header_L").position));
            Place(headerR, P(b.Anchor("Header_R").position));
            Place(emptyL, P(b.Anchor("Note_L0").position));
            Place(emptyR, P(b.Anchor("Note_R0").position));
            Place(newButton, P(b.Anchor("New").position));
            Place(close, P(b.Anchor("Close").position));

            float noteW = Px(b.Anchor("Note_L0").position, view.PaperWidthWorld);
            PlaceNotes(left, view.Left, noteW, P);
            PlaceNotes(right, view.Right, noteW, P);

            var m = view.Modal;
            if (m.Visible)
            {
                float mw = Px(m.Anchor("Field").position, 1.1f * m.Scale);
                Place(modalTitle, P(m.Anchor("Title").position));
                Place(questText, P(m.Anchor("Field").position));
                questText.style.width = mw;
                Place(questPrice, P(m.Anchor("Price").position));
                Place(submit, P(m.Anchor("Submit").position));
                Place(modalClose, P(m.Anchor("Close").position));
            }
        }

        static void PlaceNotes(NoteUI[] uis, QuestBoardView.Note[] notes, float width, Func<Vector3, Vector2> P)
        {
            for (int k = 0; k < uis.Length; k++)
            {
                if (notes[k] == null) continue;
                var box = uis[k].box;
                Place(box, P(notes[k].root.position));
                float w = width * 0.88f;
                box.style.width = w;
                box.style.height = width * 0.7f;
                box.style.paddingTop = w * 0.15f;   // под гвоздиком
                box.style.paddingBottom = w * 0.04f;
                box.style.paddingLeft = w * 0.07f;
                box.style.paddingRight = w * 0.06f;
                box.style.rotate = new Rotate(-notes[k].tilt);
                float f = Mathf.Clamp(width * 0.078f, 18f, 38f);
                box.style.fontSize = f;
                uis[k].status.style.fontSize = f * 0.8f;
                uis[k].coin.style.width = f;
                uis[k].coin.style.height = f;
            }
        }

        static void Place(VisualElement e, Vector2 p)
        {
            e.style.left = p.x;
            e.style.top = p.y;
        }

        // ---------- Сборка элементов ----------

        NoteUI MakeNote()
        {
            var ui = new NoteUI { box = new VisualElement() };
            ui.box.AddToClassList("note");
            ui.box.AddToClassList("hidden");
            // сверху текст (отступ под гвоздик), под ним строка «награда — статус», внизу кнопки справа
            ui.text = new Label();
            ui.text.AddToClassList("note-text");
            ui.box.Add(ui.text);
            var meta = new VisualElement();
            meta.AddToClassList("note-bottom");
            var reward = new VisualElement();
            reward.AddToClassList("note-reward");
            ui.coin = new VisualElement();
            ui.coin.AddToClassList("coin");
            reward.Add(ui.coin);
            ui.reward = new Label();
            reward.Add(ui.reward);
            meta.Add(reward);
            ui.status = new Label();
            ui.status.AddToClassList("note-status");
            meta.Add(ui.status);
            ui.box.Add(meta);
            ui.actions = new VisualElement();
            ui.actions.AddToClassList("note-actions");
            ui.box.Add(ui.actions);
            layer.Add(ui.box);
            return ui;
        }

        static Button Act(string text, bool secondary, Func<Task> onClick)
        {
            var b = new Button(() => _ = onClick()) { text = text };
            b.AddToClassList("note-btn");
            if (secondary) b.AddToClassList("secondary");
            return b;
        }

        static Label Text(VisualElement parent, string text, string cls)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.AddToClassList(cls);
            parent.Add(l);
            return l;
        }
    }
}
