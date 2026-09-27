using UnityEngine;

namespace LovePandas.View
{
    /// Сердечки: процедурная текстура (символа ❤ нет в шрифте некоторых телефонов) и всплеск частиц в мире.
    public static class Hearts
    {
        static Texture2D tex;
        static Material mat;

        /// Сердце 128×128 с мягким краем и бликом — для частиц и для бабла в UI.
        public static Texture2D Texture
        {
            get
            {
                if (tex != null) return tex;
                const int n = 128;
                tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var baseCol = new Color(0.96f, 0.3f, 0.42f);
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // классическая неявная кривая сердца: (x²+y²−1)³ − x²y³ ≤ 0
                    float u = (x + 0.5f) / n * 2.6f - 1.3f, v = (y + 0.5f) / n * 2.6f - 1.15f;
                    float a = u * u + v * v - 1f;
                    float f = a * a * a - u * u * v * v * v;
                    float alpha = Mathf.Clamp01(-f * 40f);
                    float shine = Mathf.Clamp01(1f - ((u + 0.45f) * (u + 0.45f) + (v - 0.35f) * (v - 0.35f)) * 9f) * 0.6f;
                    var c = Color.Lerp(baseCol, Color.white, shine);
                    c.a = alpha;
                    tex.SetPixel(x, y, c);
                }
                tex.Apply();
                return tex;
            }
        }

        static Texture2D bubble;

        /// Мыльный пузырь 256×256: прозрачная середина, радужный ободок, блики — для пузырька «входящих».
        public static Texture2D BubbleTexture
        {
            get
            {
                if (bubble != null) return bubble;
                const int n = 256;
                bubble = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2 - 1, v = (y + 0.5f) / n * 2 - 1;
                    float r = Mathf.Sqrt(u * u + v * v);
                    if (r > 1f) { bubble.SetPixel(x, y, Color.clear); continue; }
                    float ang = Mathf.Atan2(v, u);
                    // радужная плёнка сильнее к краю, оттенок бежит по кругу
                    var film = Color.HSVToRGB(Mathf.Repeat(ang / (2 * Mathf.PI) + r * 0.6f, 1f), 0.45f, 1f);
                    float rim = Mathf.Pow(r, 6f);
                    float alpha = 0.08f + rim * 0.75f;
                    var c = Color.Lerp(new Color(0.85f, 0.95f, 1f), film, Mathf.Clamp01(r * 1.2f));
                    // блики: большой сверху-слева и маленький снизу-справа
                    float h1 = Mathf.Clamp01(1f - ((u + 0.42f) * (u + 0.42f) + (v - 0.45f) * (v - 0.45f)) * 30f);
                    float h2 = Mathf.Clamp01(1f - ((u - 0.5f) * (u - 0.5f) + (v + 0.5f) * (v + 0.5f)) * 120f);
                    c = Color.Lerp(c, Color.white, Mathf.Max(h1, h2));
                    alpha = Mathf.Max(alpha, Mathf.Max(h1, h2) * 0.9f);
                    // мягкий край, чтобы не было «лесенки»
                    alpha *= Mathf.Clamp01((1f - r) * 60f);
                    c.a = alpha;
                    bubble.SetPixel(x, y, c);
                }
                bubble.Apply();
                return bubble;
            }
        }

        /// Всплеск сердечек из точки мира: разлетаются вверх и тают.
        public static void Burst(Vector3 position, int count = 10)
        {
            if (mat == null) mat = Atmosphere.FX(Texture, Color.white, additive: false);
            var go = new GameObject("Hearts");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            main.gravityModifier = -0.15f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35;
            shape.radius = 0.1f;
            shape.rotation = new Vector3(-90, 0, 0); // вверх
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.6f), new GradientAlphaKey(0, 1) });
            fade.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.6f, 1, 1.1f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.sortingFudge = -10; // поверх панды
            ps.Play();
        }
    }
}
