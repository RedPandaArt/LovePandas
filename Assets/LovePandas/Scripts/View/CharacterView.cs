using System.Collections.Generic;
using LovePandas.Core;
using UnityEngine;

namespace LovePandas.View
{
    /// Временная панда из примитивов с сокетами под слоты одежды (GDD 8.2).
    /// Когда придёт модель от художника, сокеты станут костями рига, а предметы — префабами.
    public class CharacterView : MonoBehaviour
    {
        readonly Dictionary<Slot, Transform> sockets = new Dictionary<Slot, Transform>();
        readonly Dictionary<Slot, GameObject> worn = new Dictionary<Slot, GameObject>();
        Transform body;
        float joyUntil;

        static readonly Color Fur = new Color(0.80f, 0.36f, 0.16f);
        static readonly Color Cream = new Color(0.98f, 0.92f, 0.84f);
        static readonly Color Dark = new Color(0.22f, 0.13f, 0.10f);

        public static CharacterView Create(Transform parent)
        {
            var go = new GameObject("Character");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<CharacterView>();
            view.Build();
            return view;
        }

        void Build()
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);

            var torso = Part(PrimitiveType.Capsule, body, new Vector3(0, 0.75f, 0), new Vector3(0.9f, 0.75f, 0.8f), Fur);
            Part(PrimitiveType.Sphere, body, new Vector3(0, 0.7f, 0.3f), new Vector3(0.6f, 0.7f, 0.3f), Dark); // живот
            var head = Part(PrimitiveType.Sphere, body, new Vector3(0, 1.75f, 0), new Vector3(0.95f, 0.85f, 0.85f), Fur);
            Part(PrimitiveType.Sphere, head.transform, new Vector3(0, -0.15f, 0.4f), new Vector3(0.55f, 0.4f, 0.3f), Cream);
            Part(PrimitiveType.Sphere, head.transform, new Vector3(0, -0.08f, 0.55f), Vector3.one * 0.12f, Dark); // нос
            Part(PrimitiveType.Sphere, head.transform, new Vector3(-0.2f, 0.12f, 0.45f), Vector3.one * 0.12f, Dark);
            Part(PrimitiveType.Sphere, head.transform, new Vector3(0.2f, 0.12f, 0.45f), Vector3.one * 0.12f, Dark);
            Part(PrimitiveType.Sphere, head.transform, new Vector3(-0.38f, 0.45f, 0), new Vector3(0.28f, 0.3f, 0.15f), Cream);
            Part(PrimitiveType.Sphere, head.transform, new Vector3(0.38f, 0.45f, 0), new Vector3(0.28f, 0.3f, 0.15f), Cream);
            Part(PrimitiveType.Sphere, body, new Vector3(-0.5f, 0.8f, 0.15f), Vector3.one * 0.3f, Dark);
            var handR = Part(PrimitiveType.Sphere, body, new Vector3(0.5f, 0.8f, 0.15f), Vector3.one * 0.3f, Dark);
            var tail = Part(PrimitiveType.Capsule, body, new Vector3(0, 0.5f, -0.6f), new Vector3(0.3f, 0.45f, 0.3f), Fur);
            tail.transform.localRotation = Quaternion.Euler(-60, 0, 0);

            sockets[Slot.Head] = Socket("Head", head.transform, new Vector3(0, 0.5f, 0));
            sockets[Slot.Face] = Socket("Face", head.transform, new Vector3(0, 0.12f, 0.52f));
            sockets[Slot.Neck] = Socket("Neck", body, new Vector3(0, 1.3f, 0));
            sockets[Slot.Body] = Socket("Body", torso.transform, Vector3.zero);
            sockets[Slot.Legs] = Socket("Legs", body, new Vector3(0, 0.25f, 0));
            sockets[Slot.Hands] = Socket("Hands", handR.transform, Vector3.zero);
            sockets[Slot.Tail] = Socket("Tail", tail.transform, new Vector3(0, 0.5f, 0));
            sockets[Slot.Back] = Socket("Back", body, new Vector3(0, 0.95f, -0.45f));
        }

        /// Надеть всё по данным игрока; лишнее снять.
        public void Apply(Player player, System.Func<string, ItemDef> lookup)
        {
            foreach (Slot slot in System.Enum.GetValues(typeof(Slot)))
            {
                var itemId = player.GetEquipped(slot);
                if (worn.TryGetValue(slot, out var current))
                {
                    if (current != null && current.name == itemId) continue;
                    if (current != null) Destroy(current);
                    worn.Remove(slot);
                }
                if (itemId != null) worn[slot] = MakeItem(lookup(itemId));
            }
        }

        public void PlayJoy() => joyUntil = Time.time + 1.2f;

        void Update()
        {
            // Дыхание и прыжок радости — заменятся Animator-ом с настоящими анимациями.
            float t = Time.time;
            float breathe = 1f + Mathf.Sin(t * 2f) * 0.02f;
            float hop = joyUntil > t ? Mathf.Abs(Mathf.Sin((joyUntil - t) * 8f)) * 0.35f : 0f;
            body.localScale = new Vector3(1f, breathe, 1f);
            body.localPosition = new Vector3(0, hop, 0);
        }

        GameObject MakeItem(ItemDef item)
        {
            if (item == null) return null;
            var color = RarityColor(item.Rarity);
            var socket = sockets[item.Slot];
            GameObject go;
            switch (item.Slot)
            {
                case Slot.Head:  go = Part(PrimitiveType.Cylinder, socket, Vector3.zero, new Vector3(0.6f, 0.12f, 0.6f), color); break;
                case Slot.Face:  go = Part(PrimitiveType.Cube, socket, Vector3.zero, new Vector3(0.7f, 0.12f, 0.05f), color); break;
                case Slot.Neck:  go = Part(PrimitiveType.Cylinder, socket, Vector3.zero, new Vector3(0.75f, 0.06f, 0.7f), color); break;
                case Slot.Body:  go = Part(PrimitiveType.Capsule, socket, Vector3.zero, new Vector3(1.08f, 0.85f, 1.08f), color); break;
                case Slot.Legs:  go = Part(PrimitiveType.Cylinder, socket, Vector3.zero, new Vector3(0.85f, 0.18f, 0.75f), color); break;
                case Slot.Hands: go = Part(PrimitiveType.Sphere, socket, Vector3.zero, Vector3.one * 1.25f, color); break;
                case Slot.Tail:  go = Part(PrimitiveType.Sphere, socket, Vector3.zero, new Vector3(1.3f, 0.5f, 1.3f), color); break;
                default:         go = Part(PrimitiveType.Cube, socket, Vector3.zero, new Vector3(0.55f, 0.6f, 0.25f), color); break;
            }
            go.name = item.id;
            return go;
        }

        public static Color RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Uncommon: return new Color(0.35f, 0.72f, 0.45f);
                case Rarity.Rare: return new Color(0.35f, 0.55f, 0.95f);
                case Rarity.Legendary: return new Color(1f, 0.78f, 0.2f);
                case Rarity.BossExclusive: return new Color(0.7f, 0.45f, 0.9f);
                default: return new Color(0.95f, 0.55f, 0.65f);
            }
        }

        static Transform Socket(string name, Transform parent, Vector3 pos)
        {
            var s = new GameObject("Socket_" + name).transform;
            s.SetParent(parent, false);
            s.localPosition = pos;
            return s;
        }

        static GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Materials.Lit(color);
            return go;
        }
    }
}
