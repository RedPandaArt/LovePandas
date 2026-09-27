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
            bgMat.SetFloat("_Wind", 0.0035f);   // листва на фоне чуть колышется
            bgMat.SetFloat("_GroundLine", 0.36f); // площадка внизу картинки неподвижна
            backdrop = Quad("Backdrop", cam.transform, bgMat).transform;
            backdrop.localPosition = new Vector3(0, 0, 40);

            // Луч света сверху-слева на площадку
            shaftColor = new Color(1f, 0.93f, 0.7f, 0.22f);
            shaftMat = FX(ShaftTexture(), shaftColor, additive: true);
            var shaft = Quad("LightShaft", world, shaftMat).transform;
            shaft.localPosition = new Vector3(-0.6f, 3.2f, 1.2f);
            shaft.localRotation = Quaternion.Euler(0, 0, -18);
            shaft.localScale = new Vector3(2.6f, 8f, 1);

            // Пол — нарисованная площадка на фоне: отдельной платформы нет, иначе видно «два пола».
            // Тень под лапами вешает на себя панда (ContactShadow), чтобы тень шла вместе с ней.
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

        /// Тень под лапами на нарисованном полу — «сажает» панду на пол. Вешается на панду и едет вместе с ней.
        /// Плоская, лежит на полу; задняя половина честно прячется за телом, видна передняя — перед лапами.
        /// Камера смотрит почти горизонтально, поэтому тень крупная по глубине и с плотной серединой
        /// (у мягкой радиальной текстуры видимые края почти прозрачны — тени «нет»).
        /// Не рисовать её в очереди &lt; 2500: в URP это непрозрачный проход, порядок с фоном там не гарантирован.
        public static Transform ContactShadow(Transform parent)
        {
            var root = new GameObject("ContactShadow").transform;
            root.SetParent(parent, false);
            // Альфа-смешивание тут вело себя непредсказуемо (0.5–0.8 почти не видно, 1.0 — чёрная дыра),
            // поэтому тень умножает пол на тёмный оттенок: цвет прямо задаёт, насколько темнее станет пол.
            // диск с прозрачностью в вершинах (Shapes.Disc), радиус 1 → масштаб = полуоси овала.
            // Повёрнут к камере с наклоном 60° (как лежащий на полу, но не сплющенный в нитку) и стоит на уровне лап,
            // чуть перед ними — поэтому тело его не закрывает.
            var soft = Shapes.Disc("Soft", root, ShadowMat(new Color(0.5f, 0.48f, 0.43f))).transform;
            soft.localPosition = new Vector3(0, 0.02f, -0.15f);
            soft.localRotation = Quaternion.Euler(60, 0, 0);
            soft.localScale = new Vector3(1.35f, 0.62f, 1);
            var core = Shapes.Disc("Core", root, ShadowMat(new Color(0.36f, 0.34f, 0.3f))).transform;
            core.localPosition = new Vector3(0, 0.03f, -0.12f);
            core.localRotation = Quaternion.Euler(60, 0, 0);
            core.localScale = new Vector3(0.8f, 0.3f, 1);
            return root;
        }

        /// Материал тени-умножения: пол × tint по маске пятна (tint 1 — не темнее, 0.5 — вдвое темнее).
        static Material ShadowMat(Color tint)
        {
            var m = FX(Texture2D.whiteTexture, tint, additive: false);
            m.SetFloat("_Multiply", 1f);
            m.SetFloat("_SrcBlend", (float)BlendMode.DstColor);
            m.SetFloat("_DstBlend", (float)BlendMode.Zero);
            return m;
        }

        static Texture2D core;

        /// Пятно с плотной серединой и коротким мягким краем — для контактной тени прямо под лапами.
        static Texture2D CoreTexture()
        {
            if (core != null) return core;
            const int n = 64;
            core = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                float a = Mathf.SmoothStep(0, 1, Mathf.Clamp01((1 - Mathf.Sqrt(dx * dx + dy * dy)) / 0.45f));
                core.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            core.Apply();
            return core;
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

        public static Material FX(Texture tex, Color color, bool additive)
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
