using System.IO;
using UnityEngine;

namespace LovePandas.Core
{
    public interface IGameStore
    {
        SaveData Load();
        void Save(SaveData data);
    }

    /// Сохранение в JSON на устройстве. Временное — до подключения Firebase.
    public class LocalStore : IGameStore
    {
        readonly string path = Path.Combine(Application.persistentDataPath, "save.json");

        public SaveData Load()
        {
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<SaveData>(File.ReadAllText(path)); }
            catch { return null; }
        }

        public void Save(SaveData data) => File.WriteAllText(path, JsonUtility.ToJson(data));
    }
}
