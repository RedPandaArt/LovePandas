using System.Collections.Generic;
using LovePandas.Core;
using UnityEngine;

namespace LovePandas.View
{
    /// Панда на сцене: модель из Resources/Models (Art/build_panda.py), одежда на сокетах скелета
    /// и «оживление» кодом — дыхание, голова, уши, хвост, лапки, прыжок радости.
    public class CharacterView : MonoBehaviour
    {
        const float Scale = 2.2f;   // модель ≈ 1 м без шляпы, камера рассчитана на рост ≈ 2.2
        const float Yaw = 0f;       // после запекания осей модель уже смотрит на камеру (−Z)

        // Куда цепляется вещь — Models/Items/sockets.json (пишет Art/build_panda.py). Слот — запасной вариант.
        static readonly Dictionary<Slot, string> SlotSockets = new Dictionary<Slot, string>
        {
            { Slot.Head, "Socket_Head" }, { Slot.Face, "Socket_Face" }, { Slot.Neck, "Socket_Neck" },
            { Slot.Body, "Socket_Neck" }, { Slot.Legs, "Hips" }, { Slot.Hands, "Socket_Hand_R" },
            { Slot.Tail, "Socket_Tail" }, { Slot.Back, "Socket_Back" }, { Slot.Feet, "Socket_Feet" },
        };
        [System.Serializable] class SocketEntry { public string item; public string socket; }
        [System.Serializable] class SocketList { public List<SocketEntry> entries; }
        static Dictionary<string, string> itemSockets;

        static string SocketFor(ItemDef item)
        {
            if (itemSockets == null)
            {
                itemSockets = new Dictionary<string, string>();
                var json = Resources.Load<TextAsset>("Models/Items/sockets");
                if (json != null)
                    foreach (var e in JsonUtility.FromJson<SocketList>(json.text).entries) itemSockets[e.item] = e.socket;
            }
            return itemSockets.TryGetValue(item.id, out var s) ? s : SlotSockets[item.Slot];
        }

        /// Примерка: слот → вещь (null — снять). Перекрывает надетое с сервера, пока открыт инвентарь.
        public readonly Dictionary<Slot, string> Preview = new Dictionary<Slot, string>();

        /// Поворот, которым игрок крутит панду пальцем в инвентаре.
        public float UserYaw;
        float yaw;

        Player lastPlayer;
        System.Func<string, ItemDef> lastLookup;
        string characterId;
        Transform model;
        readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        readonly Dictionary<Slot, GameObject> worn = new Dictionary<Slot, GameObject>();
        readonly List<Joint> joints = new List<Joint>();
        Joint spine, head, earL, earR, armL, armR;
        readonly Joint[] tail = new Joint[4];
        Transform hips;
        float joyUntil, nextTwitch, twitchUntil;
        int twitchSide;

        /// Кость с запомненной позой покоя и осями персонажа в её локальных координатах.
        class Joint
        {
            public Transform t;
            public Quaternion rest;
            public Vector3 right, up, forward;
            public void Pose(float pitch, float yaw, float roll) =>
                t.localRotation = rest * Quaternion.AngleAxis(pitch, right) * Quaternion.AngleAxis(yaw, up) * Quaternion.AngleAxis(roll, forward);
        }

        public static CharacterView Create(Transform parent)
        {
            var go = new GameObject("Character");
            go.transform.SetParent(parent, false);
            return go.AddComponent<CharacterView>();
        }

        /// Перерисовать с последними данными (после изменения примерки).
        public void Reapply()
        {
            if (lastPlayer != null) Apply(lastPlayer, lastLookup);
        }

        public void Apply(Player player, System.Func<string, ItemDef> lookup)
        {
            lastPlayer = player;
            lastLookup = lookup;
            SetCharacter(string.IsNullOrEmpty(player.characterId) ? "red_panda_m" : player.characterId);
            foreach (Slot slot in System.Enum.GetValues(typeof(Slot)))
            {
                var itemId = Preview.TryGetValue(slot, out var tryOn) ? tryOn : player.GetEquipped(slot);
                if (worn.TryGetValue(slot, out var current))
                {
                    if (current != null && current.name == itemId) continue;
                    if (current != null) Destroy(current);
                    worn.Remove(slot);
                }
                var def = lookup(itemId);
                if (def != null) worn[slot] = MakeItem(def);
            }
        }

        public void PlayJoy() => joyUntil = Time.time + 1.4f;

        // ---------- Модель ----------

        void SetCharacter(string id)
        {
            if (id == characterId && model != null) return;
            characterId = id;
            if (model != null) Destroy(model.gameObject);
            worn.Clear();
            bones.Clear();
            joints.Clear();

            var prefab = Resources.Load<GameObject>(id == "red_panda_f" ? "Models/panda_f" : "Models/panda_m");
            model = Instantiate(prefab, transform).transform;
            model.localScale = Vector3.one * Scale;
            model.localRotation = Quaternion.Euler(0, Yaw, 0);
            foreach (var r in model.GetComponentsInChildren<Renderer>()) Toon(r);
            foreach (var t in model.GetComponentsInChildren<Transform>()) bones[t.name] = t;

            hips = Bone("Hips");
            spine = MakeJoint("Spine"); head = MakeJoint("Head");
            earL = MakeJoint("Ear_L"); earR = MakeJoint("Ear_R");
            armL = MakeJoint("Arm_L"); armR = MakeJoint("Arm_R");
            for (int i = 0; i < 4; i++) tail[i] = MakeJoint("Tail_" + (i + 1));
        }

        Transform Bone(string name) => bones.TryGetValue(name, out var t) ? t : null;

        Joint MakeJoint(string name)
        {
            var t = Bone(name);
            if (t == null) return null;
            var inv = Quaternion.Inverse(t.rotation);
            var j = new Joint
            {
                t = t, rest = t.localRotation,
                right = inv * model.right, up = inv * model.up, forward = inv * model.forward,
            };
            joints.Add(j);
            return j;
        }

        static void Toon(Renderer r)
        {
            var mats = new Material[r.sharedMaterials.Length == 0 ? 1 : r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = Materials.Lit(Color.white);
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
        }

        // ---------- Одежда ----------

        GameObject MakeItem(ItemDef item)
        {
            var socket = Bone(SocketFor(item)) ?? model;
            // Контейнер: собственный поворот и масштаб FBX (конвертация осей Blender) остаются внутри нетронутыми.
            var go = new GameObject(item.id);
            var prefab = Resources.Load<GameObject>("Models/Items/" + item.id);
            if (prefab != null)
            {
                var inner = Instantiate(prefab, go.transform, false);
                foreach (var r in inner.GetComponentsInChildren<Renderer>()) Toon(r);
            }
            else
            {
                Placeholder(item).transform.SetParent(go.transform, false);
            }

            // Предмет смоделирован в координатах персонажа с origin в точке сокета:
            // та же ориентация и масштаб, что у модели, позиция — в сокете. Дальше едет вместе с костью.
            var t = go.transform;
            t.SetParent(socket, false);
            t.position = socket.position;
            t.rotation = model.rotation;
            t.localScale = Vector3.one;
            t.localScale = Vector3.one * (model.lossyScale.x / t.lossyScale.x);
            return go;
        }

        /// Предмет, у которого ещё нет модели: цветной кружок нужной редкости.
        static GameObject Placeholder(ItemDef item)
        {
            var go = Shapes.Sphere("Placeholder", null, Materials.Lit(RarityColor(item.Rarity)));
            go.transform.localScale = Vector3.one * 0.08f;
            return go;
        }

        public static Color RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Uncommon: return new Color(0.35f, 0.72f, 0.45f);
                case Rarity.Rare: return new Color(0.35f, 0.55f, 0.95f);
                case Rarity.Legendary: return new Color(1f, 0.78f, 0.2f);
                case Rarity.BossExclusive: return new Color(0.7f, 0.45f, 0.9f);
                default: return new Color(0.95f, 0.55f, 0.65f);
            }
        }

        // ---------- Оживление ----------

        void Update()
        {
            if (model == null) return;
            float t = Time.time;
            float joy = joyUntil > t ? Mathf.Clamp01((joyUntil - t) / 1.4f) : 0f;
            float joyWave = joy > 0 ? Mathf.Sin((1.4f - (joyUntil - t)) * 9f) : 0f;

            // дыхание и прыжок радости
            if (hips != null) hips.localScale = new Vector3(1, 1 + Mathf.Sin(t * 2.2f) * 0.018f, 1);
            float hop = joy > 0 ? Mathf.Abs(joyWave) * 0.35f * joy : 0f;
            model.localPosition = new Vector3(0, hop, 0);
            yaw = Mathf.LerpAngle(yaw, UserYaw, 1 - Mathf.Exp(-Time.deltaTime * 12f));
            model.localRotation = Quaternion.Euler(0, Yaw + yaw, 0);

            spine?.Pose(Mathf.Sin(t * 2.2f) * 1.5f, Mathf.Sin(t * 0.6f) * 3f, 0);
            head?.Pose(Mathf.Sin(t * 1.1f) * 2.5f, Mathf.Sin(t * 0.45f) * 6f, Mathf.Sin(t * 0.8f) * 4f + joy * 8f);

            // уши иногда подёргиваются
            if (t > nextTwitch) { twitchSide = Random.value > 0.5f ? 1 : -1; twitchUntil = t + 0.25f; nextTwitch = t + Random.Range(2.5f, 6f); }
            float twitch = twitchUntil > t ? Mathf.Sin((twitchUntil - t) / 0.25f * Mathf.PI) * 18f : 0f;
            earL?.Pose(0, 0, Mathf.Sin(t * 1.3f) * 2f + (twitchSide > 0 ? twitch : 0));
            earR?.Pose(0, 0, -Mathf.Sin(t * 1.3f) * 2f - (twitchSide < 0 ? twitch : 0));

            // лапки: лёгкое покачивание, в радости — вверх
            armL?.Pose(Mathf.Sin(t * 1.6f) * 4f, 0, -joy * 70f);
            armR?.Pose(-Mathf.Sin(t * 1.6f) * 4f, 0, joy * 70f);

            // хвост волной по цепочке
            for (int i = 0; i < tail.Length; i++)
                tail[i]?.Pose(Mathf.Sin(t * 1.4f - i * 0.6f) * 4f, Mathf.Sin(t * 1.1f - i * 0.7f) * (6f + i * 2f) * (1 + joy * 2f), 0);
        }
    }
}
