using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LovePandas.View
{
    /// Локация 2.5D: нарисованный фон за персонажем + луч света, пылинки, пятно тени и пост-обработка.
    /// Фон — картинка из Resources/Backgrounds, всегда растянута на весь кадр камеры (режим cover).
    public class Atmosphere : MonoBehaviour
    {
        Camera cam;
        Transform backdrop;
        float backdropAspect;
        Material shaftMat;
        Color shaftColor;

        public static Atmosphere Create(Camera cam, Transform world, string background)
        {
            var a = cam.gameObject.AddComponent<Atmosphere>();
            a.cam = cam;
            a.Build(world, background);
            return a;
        }

        void Build(Transform world, string background)
        {
            // Фон
            var tex = Resources.Load<Texture2D>("Backgrounds/" + background);
            backdropAspect = (float)tex.width / tex.height;
            var bgMat = FX(tex, Color.white, additive: false);
            bgMat.renderQueue = 1000; // рисуется первым, персонаж поверх
            backdrop = Quad("Backdrop", cam.transform, bgMat).transform;
            backdrop.localPosition = new Vector3(0, 0, 40);

            // Луч света сверху-слева на площадку
            shaftColor = new Color(1f, 0.93f, 0.7f, 0.22f);
            shaftMat = FX(ShaftTexture(), shaftColor, additive: true);
            var shaft = Quad("LightShaft", world, shaftMat).transform;
            shaft.localPosition = new Vector3(-0.6f, 3.2f, 1.2f);
            shaft.localRotation = Quaternion.Euler(0, 0, -18);
            shaft.localScale = new Vector3(2.6f, 8f, 1);

            // Мягкая тень под лапами — персонаж «стоит» на нарисованной площадке
            var blob = Quad("BlobShadow", world, FX(RadialTexture(), new Color(0.05f, 0.08f, 0.06f, 0.55f), additive: false)).transform;
            blob.localPosition = new Vector3(0, 0.01f, 0);
            blob.localRotation = Quaternion.Euler(90, 0, 0);
            blob.localScale = new Vector3(1.9f, 1.2f, 1);

            Dust(world);
            PostFX();
        }

        void LateUpdate()
        {
            // Фон на весь кадр при любом соотношении сторон, в том числе когда открыта шторка.
            float d = backdrop.localPosition.z;
            float h = 2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float w = h * cam.aspect;
            if (w / h > backdropAspect) h = w / backdropAspect; else w = h * backdropAspect;
            backdrop.localScale = new Vector3(w, h, 1);

            // Луч чуть «дышит»
            var c = shaftColor;
            c.a *= 0.85f + 0.15f * Mathf.Sin(Time.time * 0.7f);
            shaftMat.SetColor("_BaseColor", c);
        }

        void Dust(Transform world)
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(world, false);
            go.transform.localPosition = new Vector3(0, 2f, 0.5f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.duration = 10;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6, 10);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.7f, 0.9f), new Color(0.6f, 1f, 0.95f, 0.8f));
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;

            var emission = ps.emission;
            emission.rateOverTime = 6;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(5f, 3.5f, 2f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 0.3f;

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.3f), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
            fade.color = g;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = FX(RadialTexture(), Color.white, additive: true);
            ps.Play();
        }

        void PostFX()
        {
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.9f);
            bloom.threshold.Override(0.85f);
            bloom.scatter.Override(0.7f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.5f);
            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(12f);
            color.contrast.Override(8f);

            var volume = new GameObject("PostFX").AddComponent<Volume>();
            volume.transform.SetParent(transform, false);
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        // ---------- Процедурные текстуры и материалы ----------

        static Texture2D radial, shaft;

        static Texture2D RadialTexture()
        {
            if (radial != null) return radial;
            const int n = 64;
            radial = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                float a = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy));
                radial.SetPixel(x, y, new Color(1, 1, 1, a * a));
            }
            radial.Apply();
            return radial;
        }

        static Texture2D ShaftTexture()
        {
            if (shaft != null) return shaft;
            const int w = 32, h = 128;
            shaft = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2 - 1, v = (y + 0.5f) / h;
                float side = Mathf.Pow(Mathf.Clamp01(1 - Mathf.Abs(u)), 1.6f);
                float along = Mathf.SmoothStep(0, 1, v / 0.25f) * Mathf.SmoothStep(0, 1, (1 - v) / 0.35f);
                shaft.SetPixel(x, y, new Color(1, 1, 1, side * along));
            }
            shaft.Apply();
            return shaft;
        }

        static Material FX(Texture tex, Color color, bool additive)
        {
            var m = new Material(Resources.Load<Material>("Materials/FX"));
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            return m;
        }

        static GameObject Quad(string name, Transform parent, Material mat) => Shapes.Quad(name, parent, mat);
    }
}
