using System.Collections.Generic;
using LovePandas.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LovePandas.View
{
    /// Иконки вещей для инвентаря: модель из Resources/Models/Items снимается отдельной камерой
    /// в текстуру с прозрачным фоном. Для некупленных — обесцвеченная копия. Кэшируется на всю сессию.
    public static class IconRenderer
    {
        public const int Layer = 31;
        const int Size = 256;

        static Camera cam;
        static RenderTexture rt;
        static readonly Dictionary<string, (Texture2D color, Texture2D gray)> cache = new Dictionary<string, (Texture2D, Texture2D)>();

        public static (Texture2D color, Texture2D gray) Get(ItemDef item)
        {
            if (cache.TryGetValue(item.id, out var c)) return c;
            var prefab = Resources.Load<GameObject>("Models/Items/" + item.id);
            if (prefab == null) return cache[item.id] = (null, null);

            Setup();
            var holder = new GameObject("IconItem");
            holder.transform.position = cam.transform.position + cam.transform.forward * 5f;
            var inst = Object.Instantiate(prefab, holder.transform, false);
            // вид в три четверти — объём читается лучше, чем строго спереди
            holder.transform.rotation = Quaternion.Euler(-12, 28, 0);
            var renderers = inst.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                r.gameObject.layer = Layer;
                r.sharedMaterial = Materials.Lit(Color.white);
            }

            // центр и размер вещи → кадр ортокамеры
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            inst.transform.position += holder.transform.position - b.center;
            cam.orthographicSize = Mathf.Max(b.extents.x, b.extents.y) * 1.05f;

            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var color = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            color.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            color.Apply();
            RenderTexture.active = prev;
            // сразу, а не в конце кадра: иначе следующая иконка снимется вместе с этой вещью
            Object.DestroyImmediate(holder);

            var gray = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var px = color.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                var p = px[i];
                byte l = (byte)Mathf.Clamp(60 + (p.r * 0.3f + p.g * 0.59f + p.b * 0.11f) * 0.6f, 0, 255);
                px[i] = new Color32(l, l, l, (byte)(p.a * 0.85f));
            }
            gray.SetPixels32(px);
            gray.Apply();
            return cache[item.id] = (color, gray);
        }

        static void Setup()
        {
            if (cam != null) return;
            rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var go = new GameObject("IconStudio");
            go.transform.position = new Vector3(0, -500, 0);
            cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.targetTexture = rt;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 20f;
            cam.enabled = false; // рендерим вручную
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            if (Camera.main != null) Camera.main.cullingMask &= ~(1 << Layer);
        }
    }
}
