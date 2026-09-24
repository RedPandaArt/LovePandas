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
