using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LovePandas.View
{
    /// Кнопки главного экрана — деревянные медальоны с 3D-иконками (Art/build_ui.py → Models/UI/hud_*):
    /// сундучок — инвентарь, свиток — заказы, домик с сердечком — в гости. Висят перед камерой в углах экрана,
    /// покачиваются, при нажатии пружинят. Нажатия ловят прозрачные кнопки UI поверх (AppUI).
    public class HudButtons : MonoBehaviour
    {
        const float Distance = 2.2f;

        class Item
        {
            public string id;
            public Transform root;           // медальон — неподвижен
            public Transform icon;           // иконка на нём — покачивается
            public Vector3 iconPos;          // поза покоя иконки в координатах root
            public Quaternion iconRot;
            public Vector3 iconScale;
            public Vector2 viewport;   // центр на экране (0..1)
            public float size;         // диаметр медальона — доля ширины экрана
            public float punch;        // время последнего нажатия
            public float phase;
        }

        readonly Dictionary<string, Item> items = new Dictionary<string, Item>();
        Camera cam;
        float shown, shownTarget = 1f;

        public static HudButtons Create(Camera cam)
        {
            var h = new GameObject("Hud").AddComponent<HudButtons>();
            h.transform.SetParent(cam.transform, false);
            h.cam = cam;
            h.Add("inventory", "Models/UI/hud_inventory", new Vector2(0.18f, 0.1f), 0.24f);
            h.Add("orders", "Models/UI/hud_orders", new Vector2(0.82f, 0.1f), 0.24f);
            h.Add("visit", "Models/UI/hud_visit", new Vector2(0.86f, 0.9f), 0.19f);
            return h;
        }

        void Add(string id, string prefab, Vector2 viewport, float size)
        {
            var root = new GameObject("Hud_" + id).transform;
            root.SetParent(transform, false);
            // контейнер: собственный поворот FBX (оси Blender) остаётся внутри
            var inst = Instantiate(Resources.Load<GameObject>(prefab), root, false);
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = Materials.Lit(Color.white);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var icon = inst.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Icon");
            items[id] = new Item
            {
                id = id, root = root, viewport = viewport, size = size, punch = -10, phase = items.Count * 1.7f,
                icon = icon,
                iconPos = icon != null ? root.InverseTransformPoint(icon.position) : Vector3.zero,
                iconRot = icon != null ? Quaternion.Inverse(root.rotation) * icon.rotation : Quaternion.identity,
                iconScale = icon != null ? icon.localScale : Vector3.one,
            };
        }

        /// Показать/спрятать все кнопки (с «попом»).
        public void SetVisible(bool v) => shownTarget = v ? 1f : 0f;

        public void Press(string id)
        {
            if (items.TryGetValue(id, out var it)) it.punch = Time.time;
        }

        /// Центр и радиус медальона в мире — чтобы UI положил поверх прозрачную кнопку.
        public bool TryGet(string id, out Vector3 center, out float radius)
        {
            center = default;
            radius = 0;
            if (!items.TryGetValue(id, out var it) || shown < 0.5f) return false;
            center = it.root.position;
            radius = it.root.lossyScale.x * 0.5f;
            return true;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime, t = Time.time;
            shown = Mathf.MoveTowards(shown, shownTarget, dt * 5f);
            float halfH = Distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfW = halfH * cam.aspect;
            // «поп» с лёгким перелётом при появлении
            float pop = shown <= 0 ? 0 : 1f + Mathf.Sin(shown * Mathf.PI) * 0.15f * (shownTarget > 0 ? 1 : 0);
            foreach (var it in items.Values)
            {
                float diameter = it.size * 2f * halfW;
                // медальон стоит на месте (только «поп» при появлении)
                it.root.localPosition = new Vector3((it.viewport.x - 0.5f) * 2f * halfW, (it.viewport.y - 0.5f) * 2f * halfH, Distance);
                it.root.localRotation = Quaternion.identity;
                it.root.localScale = Vector3.one * diameter * pop * Mathf.SmoothStep(0, 1, shown);
                it.root.gameObject.SetActive(shown > 0.01f);

                // иконка живёт: покачивается, поворачивается, пружинит при нажатии
                if (it.icon == null) continue;
                float since = t - it.punch;
                float punch = since < 0.35f ? 1f - Mathf.Sin(since / 0.35f * Mathf.PI) * 0.2f : 1f;
                float bob = Mathf.Sin(t * 1.3f + it.phase) * 0.035f;
                it.icon.position = it.root.TransformPoint(it.iconPos + Vector3.up * bob);
                it.icon.rotation = it.root.rotation *
                                   Quaternion.Euler(Mathf.Sin(t * 0.9f + it.phase) * 6f, Mathf.Sin(t * 0.7f + it.phase) * 14f,
                                                    Mathf.Sin(t * 1.1f + it.phase) * 3f) * it.iconRot;
                it.icon.localScale = it.iconScale * punch;
            }
        }
    }
}
