using System.Collections.Generic;
using UnityEngine;

namespace LovePandas.View
{
    /// Подвесная 3D-доска перед камерой (модели из Art/build_ui.py): спускается сверху на пружине,
    /// покачивается, вписывается в заданный прямоугольник экрана. Пустышки модели доступны по имени —
    /// к ним UI привязывает надписи и кнопки. На ней стоят доска заказов и дощечка «Новый заказ».
    public class HangingBoard : MonoBehaviour
    {
        Camera cam;
        Transform hanger, model;
        readonly Dictionary<string, Transform> anchors = new Dictionary<string, Transform>();
        float distance, visualW, visualH;
        Rect viewport;       // куда вписать (доли экрана: x, y — центр, width/height — максимум)
        bool cover;          // закрыть весь экран (больше рамки), а не вписаться
        float open, openVel, target, swing;

        public float Scale { get; private set; } = 1f;
        public bool Visible => hanger.gameObject.activeSelf;
        public bool Settled => target > 0 && Mathf.Abs(open - 1) < 0.03f;
        public Transform Root => hanger;
        public Camera Camera => cam;

        public static HangingBoard Create(Camera cam, string prefab, float distance, float visualW, float visualH,
                                          Rect viewport, bool cover, Material material)
        {
            var go = new GameObject("Hanging_" + prefab.Replace('/', '_'));
            go.transform.SetParent(cam.transform, false);
            var hb = go.AddComponent<HangingBoard>();
            hb.cam = cam;
            hb.hanger = go.transform;
            hb.distance = distance;
            hb.visualW = visualW;
            hb.visualH = visualH;
            hb.viewport = viewport;
            hb.cover = cover;
            hb.model = Instantiate(Resources.Load<GameObject>(prefab), go.transform, false).transform;
            foreach (var r in hb.model.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach (var t in hb.model.GetComponentsInChildren<Transform>()) hb.anchors[t.name] = t;
            go.SetActive(false);
            return hb;
        }

        public Transform Anchor(string name) => anchors.TryGetValue(name, out var t) ? t : null;

        public void Show()
        {
            if (!hanger.gameObject.activeSelf) { open = 0; openVel = 0; }
            hanger.gameObject.SetActive(true);
            target = 1;
            swing = 6f;
        }

        public void Hide() => target = 0;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            // пружина: быстро спускается, чуть проскакивает и оседает; наверх уходит резче
            float k = target > 0 ? 90f : 140f, damp = target > 0 ? 11f : 20f;
            openVel += ((target - open) * k - openVel * damp) * dt;
            open += openVel * dt;
            if (target == 0 && open < 0.02f) { open = 0; openVel = 0; hanger.gameObject.SetActive(false); return; }

            float halfH = distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfW = halfH * cam.aspect;
            float sw = viewport.width * 2f * halfW / visualW, sh = viewport.height * 2f * halfH / visualH;
            Scale = cover ? Mathf.Max(sw, sh) : Mathf.Min(sw, sh);
            float x = (viewport.x - 0.5f) * 2f * halfW;
            float y = (viewport.y - 0.5f) * 2f * halfH + (1f - open) * 2.3f * halfH;
            hanger.localPosition = new Vector3(x, y, distance);
            hanger.localScale = Vector3.one * Scale;

            swing = Mathf.Lerp(swing, 0, 1 - Mathf.Exp(-dt * 1.5f));
            float t = Time.time;
            hanger.localRotation = Quaternion.Euler(0, Mathf.Sin(t * 0.5f) * (cover ? 0.6f : 1.5f),
                                                    Mathf.Sin(t * 3.2f) * swing + Mathf.Sin(t * 0.7f) * (cover ? 0.25f : 0.5f));
        }
    }
}
