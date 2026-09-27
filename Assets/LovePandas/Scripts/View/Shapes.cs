using UnityEngine;

namespace LovePandas.View
{
    /// Простые объекты без GameObject.CreatePrimitive: тот добавляет коллайдер, а физика вырезана
    /// из сборки (Strip Engine Code) — на телефоне это ошибки «MeshCollider doesn't exist».
    public static class Shapes
    {
        static Mesh quad;

        public static GameObject Quad(string name, Transform parent, Material mat) =>
            Make(name, parent, quad != null ? quad : quad = BuildQuad(), mat);

        public static GameObject Sphere(string name, Transform parent, Material mat) =>
            Make(name, parent, Resources.GetBuiltinResource<Mesh>("Sphere.fbx"), mat);

        static GameObject Make(string name, Transform parent, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        static Mesh disc;

        /// Мягкое пятно-диск радиуса 1 в плоскости XY: плотная середина (до 0.5), к краю прозрачность падает до 0.
        /// Прозрачность — в цветах вершин, без текстуры (для теней).
        public static GameObject Disc(string name, Transform parent, Material mat) =>
            Make(name, parent, disc != null ? disc : disc = BuildDisc(), mat);

        static Mesh BuildDisc()
        {
            const int seg = 32;
            var v = new Vector3[1 + seg * 2];
            var c = new Color[v.Length];
            var uv = new Vector2[v.Length];
            v[0] = Vector3.zero; c[0] = Color.white; uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2 / seg;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                v[1 + i] = dir * 0.5f; c[1 + i] = Color.white;
                v[1 + seg + i] = dir; c[1 + seg + i] = new Color(1, 1, 1, 0);
                uv[1 + i] = uv[1 + seg + i] = new Vector2(0.5f, 0.5f);
            }
            var tris = new System.Collections.Generic.List<int>();
            for (int i = 0; i < seg; i++)
            {
                int n = (i + 1) % seg;
                tris.AddRange(new[] { 0, 1 + n, 1 + i });                           // середина
                tris.AddRange(new[] { 1 + i, 1 + n, 1 + seg + n, 1 + i, 1 + seg + n, 1 + seg + i }); // мягкий край
            }
            var m = new Mesh { name = "LP_Disc", vertices = v, colors = c, uv = uv, triangles = tris.ToArray() };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Mesh BuildQuad()
        {
            var m = new Mesh { name = "LP_Quad" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
