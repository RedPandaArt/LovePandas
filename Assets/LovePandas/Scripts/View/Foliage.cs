using System.Collections.Generic;
using UnityEngine;

namespace LovePandas.View
{
    /// Листья — нарисованные спрайты (Resources/Foliage), качаются на ветру вокруг точки крепления.
    /// Ближний план привязан к углам кадра (всегда обрамляет экран, лишь чуть отстаёт от камеры),
    /// средний план стоит в мире — при сдвиге камеры (инвентарь, дрейф) он даёт параллакс относительно фона.
    public class Foliage : MonoBehaviour
    {
        class Leaf
        {
            public Transform pivot;
            public Quaternion rest;
            public float amp, speed, phase;
            public bool screen;       // ближний план у угла кадра
            public Vector2 viewport;  // угол кадра (0..1)
            public float depth;       // расстояние от камеры
        }

        const float ScreenLag = 0.12f; // ближний план отстаёт от сдвига камеры на эту долю — лёгкий параллакс

        readonly List<Leaf> leaves = new List<Leaf>();
        Camera cam;
        Vector3 camRest;

        // Ближний план: спрайт, угол кадра (viewport), глубина от камеры, поворот, высота, точка крепления (UV), зеркально.
        // Монстера на картинке обрезана сверху и справа — обрез уходит за угол кадра, качается она вокруг него.
        static readonly (string tex, Vector2 viewport, float depth, float rotZ, float size, Vector2 anchor, bool flip)[] Near =
        {
            ("monstera", new Vector2(1.04f, 1.03f), 3.4f, 0f, 0.62f, new Vector2(1f, 1f), false),
            ("monstera", new Vector2(-0.04f, 1.02f), 3.5f, 0f, 0.55f, new Vector2(1f, 1f), true),
            ("fern", new Vector2(-0.02f, -0.03f), 3.2f, 10f, 0.55f, new Vector2(0.5f, 0.05f), false),
            ("fern", new Vector2(1.02f, -0.03f), 3.2f, -10f, 0.5f, new Vector2(0.5f, 0.05f), true),
        };

        // Средний план: спрайт, точка в мире, поворот, высота, точка крепления (UV), зеркально.
        static readonly (string tex, Vector3 pos, float rotZ, float size, Vector2 anchor, bool flip)[] World =
        {
            ("vine", new Vector3(-1.75f, 5.4f, 1.5f), 0f, 3.0f, new Vector2(0.6f, 1f), false),
            ("vine", new Vector3(1.95f, 5.6f, 2.2f), 0f, 3.4f, new Vector2(0.6f, 1f), true),
            ("vine", new Vector3(1.1f, 5.8f, 4f), 0f, 2.4f, new Vector2(0.6f, 1f), false),
            ("fern", new Vector3(-1.6f, -0.05f, 0.9f), 4f, 1.2f, new Vector2(0.5f, 0.05f), false),
            ("fern", new Vector3(1.7f, -0.05f, 1.1f), -4f, 1.1f, new Vector2(0.5f, 0.05f), true),
        };

        readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        public static Foliage Create(Transform world)
        {
            var go = new GameObject("Foliage");
            go.transform.SetParent(world, false);
            var f = go.AddComponent<Foliage>();
            f.cam = Camera.main;
            f.camRest = f.cam.transform.localPosition;
            f.Build();
            return f;
        }

        void Build()
        {
            foreach (var (tex, pos, rotZ, size, anchor, flip) in World)
                Add(tex, transform, pos, rotZ, size, anchor, flip);
            foreach (var (tex, viewport, depth, rotZ, size, anchor, flip) in Near)
            {
                var l = Add(tex, cam.transform, Vector3.zero, rotZ, size, anchor, flip);
                if (l == null) continue;
                l.screen = true;
                l.viewport = viewport;
                l.depth = depth;
            }
        }

        Leaf Add(string texName, Transform parent, Vector3 pos, float rotZ, float size, Vector2 anchor, bool flip)
        {
            if (!mats.TryGetValue(texName, out var mat))
            {
                var tex = Resources.Load<Texture2D>("Foliage/" + texName);
                if (tex == null) return null;
                mat = Atmosphere.FX(tex, Color.white, additive: false);
                mat.renderQueue = 3000 + 10; // поверх пылинок и луча
                mats[texName] = mat;
            }
            var t = (Texture2D)mat.GetTexture("_BaseMap");
            float aspect = (float)t.width / t.height;

            var pivot = new GameObject("Leaf_" + texName).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = pos;
            pivot.localRotation = Quaternion.Euler(0, 0, rotZ);

            // спрайт смещён так, чтобы точка крепления (anchor в UV) совпала с pivot; ширина со знаком — зеркало
            var quad = Shapes.Quad("Sprite", pivot, mat).transform;
            float w = size * aspect * (flip ? -1 : 1);
            quad.localScale = new Vector3(w, size, 1);
            quad.localPosition = new Vector3((0.5f - anchor.x) * w, (0.5f - anchor.y) * size, 0);

            var leaf = new Leaf
            {
                pivot = pivot, rest = pivot.localRotation,
                amp = anchor.y > 0.9f ? 2.2f : 3.5f, speed = Random.Range(0.7f, 1.1f), phase = Random.Range(0f, 6f),
            };
            leaves.Add(leaf);
            return leaf;
        }

        void LateUpdate()
        {
            float t = Time.time;
            float halfTan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var lag = -(cam.transform.localPosition - camRest) * ScreenLag;
            foreach (var l in leaves)
            {
                float a = Mathf.Sin(t * l.speed + l.phase) * l.amp + Mathf.Sin(t * l.speed * 2.3f + l.phase) * l.amp * 0.35f;
                l.pivot.localRotation = l.rest * Quaternion.Euler(0, 0, a);
                if (!l.screen) continue;
                float halfH = l.depth * halfTan, halfW = halfH * cam.aspect;
                l.pivot.localPosition = new Vector3((l.viewport.x - 0.5f) * 2 * halfW, (l.viewport.y - 0.5f) * 2 * halfH, l.depth) + lag;
            }
        }
    }
}
