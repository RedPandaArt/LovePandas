using System.Collections.Generic;
using LovePandas.Core;
using UnityEngine;

namespace LovePandas.View
{
    /// 3D-доска заказов на весь экран: две колонки пергаментных листков на гвоздиках
    /// (слева — задания партнёра, справа — свои) и дощечка-модалка «Новый заказ».
    /// Надписи и кнопки на листках — в UI (OrdersPanel), он берёт отсюда положение и наклон листков.
    public class QuestBoardView
    {
        public const int PerColumn = 4;
        const float PaperW = 0.62f;

        public readonly HangingBoard Board, Modal;
        readonly Material paperMine, paperPartner, plain;
        readonly Dictionary<string, Transform> notes = new Dictionary<string, Transform>();
        readonly Dictionary<string, float> popUntil = new Dictionary<string, float>();

        public class Note
        {
            public Quest quest;
            public Transform root;
            public float tilt;   // наклон листка, градусы (по Z)
        }

        public readonly Note[] Left = new Note[PerColumn], Right = new Note[PerColumn];

        public QuestBoardView(Camera cam)
        {
            plain = Materials.Lit(Color.white);
            Board = HangingBoard.Create(cam, "Models/UI/quest_board", 3.2f, 1.62f, 3.12f,
                                        new Rect(0.5f, 0.465f, 0.98f, 0.93f), false, plain);
            Modal = HangingBoard.Create(cam, "Models/UI/modal_board", 2.3f, 1.4f, 1.1f,
                                        new Rect(0.5f, 0.68f, 0.94f, 0.5f), false, plain);
            paperPartner = PaperMaterial(new Color(1f, 0.98f, 0.92f));
            paperMine = PaperMaterial(new Color(0.9f, 0.96f, 1f)); // свои — чуть голубоватые, чтобы различать
        }

        static Material PaperMaterial(Color tint)
        {
            var m = new Material(Resources.Load<Material>("Materials/Base"));
            m.SetTexture("_BaseMap", Resources.Load<Texture2D>("Textures/paper"));
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Cutoff", 0.5f);
            m.SetFloat("_OutlineWidth", 0f);
            m.SetFloat("_RimStrength", 0.15f);
            return m;
        }

        /// Развесить листки: left — задания партнёра, right — свои (не больше PerColumn в колонке).
        public void SetNotes(IList<Quest> left, IList<Quest> right)
        {
            var keep = new HashSet<string>();
            Fill(Left, left, "L", paperPartner, keep);
            Fill(Right, right, "R", paperMine, keep);
            foreach (var id in new List<string>(notes.Keys))
                if (!keep.Contains(id)) { Object.Destroy(notes[id].gameObject); notes.Remove(id); }
        }

        void Fill(Note[] column, IList<Quest> quests, string side, Material mat, HashSet<string> keep)
        {
            for (int k = 0; k < PerColumn; k++)
            {
                var q = k < quests.Count ? quests[k] : null;
                if (q == null) { column[k] = null; continue; }
                keep.Add(q.id);
                var anchor = Board.Anchor($"Note_{side}{k}");
                float tilt = TiltFor(q.id);
                if (!notes.TryGetValue(q.id, out var root))
                {
                    // контейнер: собственный поворот FBX (оси Blender) остаётся внутри нетронутым
                    root = new GameObject("Note_" + q.id).transform;
                    var inst = Object.Instantiate(Resources.Load<GameObject>("Models/UI/paper"), root, false);
                    foreach (var r in inst.GetComponentsInChildren<Renderer>())
                    {
                        r.sharedMaterial = r.name.Contains("Pin") ? plain : mat;
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                    notes[q.id] = root;
                    popUntil[q.id] = Time.time + 0.35f; // новый листок «прихлопывают» к доске
                }
                // та же ориентация и масштаб, что у доски, плюс наклон листка; дальше едет вместе с доской
                root.SetParent(anchor, false);
                root.position = anchor.position;
                root.rotation = Board.Root.rotation * Quaternion.Euler(0, 0, tilt);
                root.localScale = Vector3.one;
                root.localScale = Vector3.one * (Board.Root.lossyScale.x / root.lossyScale.x);
                column[k] = new Note { quest = q, root = root, tilt = tilt };
            }
        }

        /// Каждый кадр: анимация появления новых листков.
        public void Tick()
        {
            foreach (var kv in notes)
            {
                float left = popUntil.TryGetValue(kv.Key, out var u) ? u - Time.time : 0;
                if (left <= -0.1f) continue;
                float s = left > 0 ? 1f + Mathf.Sin(left / 0.35f * Mathf.PI) * 0.18f : 1f;
                var t = kv.Value;
                t.localScale = Vector3.one;
                t.localScale = Vector3.one * (Board.Root.lossyScale.x / t.lossyScale.x * s);
            }
        }

        /// Устойчивый «случайный» наклон по id — листок не дёргается между перерисовками.
        static float TiltFor(string id)
        {
            int h = 17;
            foreach (var c in id) h = h * 31 + c;
            return ((h & 0xffff) / 65535f - 0.5f) * 9f;
        }

        public float PaperWidthWorld => PaperW * Board.Scale;
    }
}
