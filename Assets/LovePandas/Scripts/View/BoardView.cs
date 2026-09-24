using System.Collections.Generic;
using LovePandas.Core;
using UnityEngine;

namespace LovePandas.View
{
    /// 3D-доска инвентаря (Art/build_ui.py → Models/UI/board): висит перед камерой на левой половине кадра,
    /// спускается сверху на верёвках с покачиванием. В гнёздах — настоящие модели вещей (некупленные серые),
    /// в шапке — медальоны категорий с мини-моделями. Надписи и нажатия — в UI (InventoryPanel).
    public class BoardView : MonoBehaviour
    {
        public const int Slots = 6;
        const float Distance = 3f;          // от камеры
        const float VisualWidth = 1.3f;     // доска с брёвнами, в единицах модели
        const float VisualHeight = 2.62f;
        const float ItemRadius = 0.16f, TabRadius = 0.088f;

        // вещь-символ для каждой вкладки
        static readonly string[] TabIcons = { "sword_wood", "hat_wanderer", "cape_red", "boots_brown" };

        Camera cam;
        Transform hanger, board, closeAnchor;
        readonly Transform[] slots = new Transform[Slots];
        readonly Transform[] tabs = new Transform[4];
        readonly GameObject[] slotItems = new GameObject[Slots];
        readonly GameObject[] slotRings = new GameObject[Slots];
        readonly GameObject[] tabItems = new GameObject[4];
        GameObject tabRing;
        int selectedTab = -1;
        float open, openVel, target;       // 0 — спрятана наверху, 1 — на месте (пружина)
        float swing;
        float scale = 1f;

        public bool Visible => hanger != null && hanger.gameObject.activeSelf;
        public float Scale => scale;

        public static BoardView Create(Camera cam)
        {
            var bv = cam.gameObject.AddComponent<BoardView>();
            bv.cam = cam;
            bv.Build();
            return bv;
        }

        void Build()
        {
            hanger = new GameObject("BoardHanger").transform;
            hanger.SetParent(cam.transform, false);
            board = Instantiate(Resources.Load<GameObject>("Models/UI/board"), hanger, false).transform;
            foreach (var r in board.GetComponentsInChildren<Renderer>()) r.sharedMaterial = Materials.Lit(Color.white);
            foreach (var t in board.GetComponentsInChildren<Transform>())
            {
                if (t.name.StartsWith("Slot_")) slots[int.Parse(t.name.Substring(5))] = t;
                else if (t.name.StartsWith("Tab_")) tabs[int.Parse(t.name.Substring(4))] = t;
                else if (t.name == "Close") closeAnchor = t;
            }
            for (int i = 0; i < 4; i++) tabItems[i] = Place(TabIcons[i], tabs[i], TabRadius, Materials.Lit(Color.white));
            tabRing = Ring(tabs[0], TabRadius / 0.2f * 1.25f, new Color(1f, 0.55f, 0.25f));
            hanger.gameObject.SetActive(false);
        }

        // ---------- Открытие ----------

        public void Show()
        {
            hanger.gameObject.SetActive(true);
            target = 1;
            swing = 7f; // качнётся после спуска
        }

        public void Hide() => target = 0;

        void LateUpdate()
        {
            if (!hanger.gameObject.activeSelf) return;
            float dt = Time.deltaTime;

            // пружина: быстро спускается, чуть проскакивает и оседает
            float k = target > 0 ? 90f : 140f, damp = target > 0 ? 11f : 20f;
            openVel += ((target - open) * k - openVel * damp) * dt;
            open += openVel * dt;
            if (target == 0 && open < 0.02f) { open = 0; openVel = 0; hanger.gameObject.SetActive(false); return; }

            // место на экране: левые ~55% ширины, по высоте — сколько влезет
            float halfH = Distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfW = halfH * cam.aspect;
            scale = Mathf.Min(0.54f * 2f * halfW / VisualWidth, 0.76f * 2f * halfH / VisualHeight);
            float x = (0.295f - 0.5f) * 2f * halfW;
            float y = (0.54f - 0.5f) * 2f * halfH + (1f - open) * 2.2f * halfH;
            hanger.localPosition = new Vector3(x, y, Distance);
            hanger.localScale = Vector3.one * scale;

            swing = Mathf.Lerp(swing, 0, 1 - Mathf.Exp(-dt * 1.5f));
            float t = Time.time;
            hanger.localRotation = Quaternion.Euler(0, Mathf.Sin(t * 0.5f) * 2f, Mathf.Sin(t * 3.2f) * swing + Mathf.Sin(t * 0.7f) * 0.6f);

            // вещи в гнёздах медленно вращаются, выбранная вкладка «выдвинута» и покачивается
            for (int i = 0; i < Slots; i++)
                if (slotItems[i] != null) slotItems[i].transform.localRotation = Quaternion.Euler(-10, t * 35f + i * 40f, 0);
            for (int i = 0; i < 4; i++)
            {
                bool sel = i == selectedTab;
                var tr = tabItems[i].transform;
                tr.localPosition = new Vector3(0, 0, sel ? -0.06f : 0);
                tr.localRotation = Quaternion.Euler(-10, sel ? Mathf.Sin(t * 2f) * 25f : 20f, 0);
            }
        }

        // ---------- Содержимое ----------

        public void SetTab(int index)
        {
            selectedTab = index;
            tabRing.transform.SetParent(tabs[index], false);
        }

        public enum SlotState { Owned, Locked }

        /// items[i] — вещь в i-м гнезде (null — пусто); ring — подсветка гнезда: null, оранжевый (надето), бирюзовый (примерка).
        public void SetSlot(int i, ItemDef item, SlotState state, Color? ring)
        {
            if (slotItems[i] != null && (item == null || slotItems[i].name != item.id + state)) { Destroy(slotItems[i]); slotItems[i] = null; }
            if (item != null && slotItems[i] == null)
            {
                slotItems[i] = Place(item.id, slots[i], ItemRadius, state == SlotState.Locked ? Materials.Desaturated() : Materials.Lit(Color.white));
                if (slotItems[i] != null) slotItems[i].name = item.id + state;
            }

            if (ring == null) { if (slotRings[i] != null) slotRings[i].SetActive(false); }
            else
            {
                if (slotRings[i] == null) slotRings[i] = Ring(slots[i], 1.02f, ring.Value);
                slotRings[i].SetActive(true);
                foreach (var r in slotRings[i].GetComponentsInChildren<Renderer>()) r.sharedMaterial = Materials.Lit(ring.Value);
            }
        }

        // ---------- Для UI: где что на экране ----------

        public Vector3 SlotWorld(int i) => slots[i].position;
        public Vector3 SlotLabelWorld(int i) => slots[i].position - hanger.up * (0.23f * scale);
        public Vector3 TabWorld(int i) => tabs[i].position;
        public Vector3 CloseWorld => closeAnchor.position;
        public float SlotRadiusWorld => 0.2f * scale;
        public float TabRadiusWorld => 0.1f * scale;
        public Vector3 CameraRight => cam.transform.right;
        public Camera Camera => cam;

        // ---------- Внутреннее ----------

        /// Вещь-модель в гнезде: по центру точки, вписана в радиус, лицом к камере.
        GameObject Place(string itemId, Transform anchor, float radius, Material mat)
        {
            var prefab = Resources.Load<GameObject>("Models/Items/" + itemId);
            if (prefab == null) return null;
            var holder = new GameObject(itemId);
            holder.transform.SetParent(anchor, false);
            var inst = Instantiate(prefab, holder.transform, false);
            var rs = inst.GetComponentsInChildren<Renderer>();
            foreach (var r in rs)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // вписать по габаритам (в координатах holder, до масштабирования)
            var b = new Bounds(holder.transform.InverseTransformPoint(rs[0].bounds.center), Vector3.zero);
            foreach (var r in rs)
            {
                var wb = r.bounds;
                b.Encapsulate(holder.transform.InverseTransformPoint(wb.min));
                b.Encapsulate(holder.transform.InverseTransformPoint(wb.max));
            }
            // длинные вещи (посох, меч) кладём по диагонали — так они занимают всё гнездо, а не крохотную середину
            bool tall = b.extents.y > 1.8f * Mathf.Max(b.extents.x, b.extents.z);
            float fit = tall ? b.extents.y * 0.72f : Mathf.Max(b.extents.x, b.extents.y, b.extents.z);
            float s = radius / Mathf.Max(0.0001f, fit);
            var tilt = new GameObject("Tilt").transform;
            tilt.SetParent(holder.transform, false);
            tilt.localRotation = Quaternion.Euler(0, 0, tall ? -40f : 0f);
            inst.transform.SetParent(tilt, false);
            inst.transform.localPosition = -b.center;
            holder.transform.localScale = Vector3.one * s;
            return holder;
        }

        GameObject Ring(Transform anchor, float scaleMul, Color color)
        {
            var go = Instantiate(Resources.Load<GameObject>("Models/UI/ring"), anchor, false);
            go.transform.localPosition = new Vector3(0, 0, 0.07f);
            go.transform.localScale = Vector3.one * scaleMul;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = Materials.Lit(color);
            return go;
        }
    }
}
