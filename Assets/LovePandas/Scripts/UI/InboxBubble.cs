using System;
using System.Linq;
using System.Threading.Tasks;
using LovePandas.Core;
using LovePandas.View;
using UnityEngine;
using UnityEngine.UIElements;

namespace LovePandas.UI
{
    /// Пузырёк «входящих»: когда партнёр заглядывал в гости и гладил/целовал/обнимал, над головой своей панды
    /// парит мыльный пузырь с сердечком. Тап — карточка «К вам заглядывал…», «Спасибо!» — прочитано, панда радуется.
    public class InboxBubble
    {
        readonly GameService game;
        readonly CharacterView character;
        readonly Func<Func<Task<string>>, string, Action, Task> run;
        readonly VisualElement root, bubble, card;
        readonly Label cardTitle, cardText;
        float appearAt;
        bool wasVisible;

        public InboxBubble(VisualElement root, GameService game, CharacterView character,
                           Func<Func<Task<string>>, string, Action, Task> run)
        {
            this.root = root;
            this.game = game;
            this.character = character;
            this.run = run;

            bubble = new VisualElement();
            bubble.AddToClassList("inbox-bubble");
            bubble.AddToClassList("hidden");
            bubble.style.backgroundImage = Hearts.BubbleTexture;
            var heart = new VisualElement { pickingMode = PickingMode.Ignore };
            heart.AddToClassList("inbox-heart");
            heart.style.backgroundImage = Hearts.Texture;
            bubble.Add(heart);
            bubble.RegisterCallback<ClickEvent>(_ => OpenCard());
            root.Add(bubble);

            card = new VisualElement();
            card.AddToClassList("inbox-card");
            card.AddToClassList("hidden");
            var cardHeart = new VisualElement { pickingMode = PickingMode.Ignore };
            cardHeart.AddToClassList("inbox-card-heart");
            cardHeart.style.backgroundImage = Hearts.Texture;
            card.Add(cardHeart);
            cardTitle = new Label();
            cardTitle.AddToClassList("inbox-card-title");
            card.Add(cardTitle);
            cardText = new Label();
            cardText.AddToClassList("inbox-card-text");
            card.Add(cardText);
            var thanks = new Button(Thanks) { text = "Спасибо!" };
            thanks.AddToClassList("action-btn");
            thanks.AddToClassList("big");
            card.Add(thanks);
            root.Add(card);
        }

        /// Каждый кадр: показать/спрятать и держать пузырёк над головой своей панды, покачивая.
        public void Tick(bool allowed)
        {
            var inbox = game.State?.inbox;
            bool show = allowed && inbox != null && inbox.Count > 0 && card.ClassListContains("hidden");
            if (show && !wasVisible) appearAt = Time.time;
            wasVisible = show;
            bubble.EnableInClassList("hidden", !show);
            if (!show || root.panel == null) return;

            float t = Time.time;
            float rise = Mathf.Clamp01((t - appearAt) / 0.8f);               // всплывает снизу
            var cam = Camera.main;
            var head = character.HeadPoint + Vector3.up * (0.95f * 1f);
            var p = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, head, cam);
            float bob = Mathf.Sin(t * 1.6f) * 14f;
            bubble.style.left = p.x + Mathf.Sin(t * 0.9f) * 10f;
            bubble.style.top = p.y + bob + (1f - Mathf.SmoothStep(0, 1, rise)) * 500f;
            float wobble = 1f + Mathf.Sin(t * 2.4f) * 0.03f;
            bubble.style.scale = new Scale(new Vector3(wobble, 2f - wobble, 1));
            bubble.style.opacity = rise;
        }

        void OpenCard()
        {
            var inbox = game.State.inbox;
            if (inbox.Count == 0) return;
            bool she = game.State.HasPartner && game.Partner.characterId == "red_panda_f"; // род — по тому, кто заглядывал
            string from = inbox[inbox.Count - 1].fromName;
            cardTitle.text = $"К вам заглядывал{(she ? "а" : "")} {from}";
            cardText.text = string.Join("\n", inbox.GroupBy(i => i.kind).Select(g =>
            {
                string verb = g.Key == "pet" ? (she ? "погладила по голове" : "погладил по голове")
                            : g.Key == "kiss" ? (she ? "поцеловала" : "поцеловал")
                            : (she ? "обняла" : "обнял");
                return g.Count() > 1 ? $"{verb} ×{g.Count()}" : verb;
            }));
            card.RemoveFromClassList("hidden");
            character.PlayJoy();
        }

        void Thanks()
        {
            card.AddToClassList("hidden");
            character.PlayHug();
            _ = run(game.AckInbox, null, null);
        }

        public void Hide() => card.AddToClassList("hidden");
    }
}
