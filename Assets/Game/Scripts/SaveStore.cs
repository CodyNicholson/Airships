using System;
using System.IO;
using Airships.Core;
using UnityEngine;

namespace Airships.Game
{
    // Local save file. Writes go to a temporary file first, so a crash can't corrupt the save.
    public static class SaveStore
    {
        static string fileName = "save.json";

        static string SavePath => Path.Combine(Application.persistentDataPath, fileName);

        // Automated playthroughs start clean and never touch the real save. Set on every boot because
        // this project's Play mode keeps static values between runs.
        public static void SelectFile(bool automated)
        {
            fileName = automated ? "save_autoplay.json" : "save.json";
            if (automated && File.Exists(SavePath)) File.Delete(SavePath);
        }

        public static SaveData Load(GameContent content)
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    var save = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                    if (save != null && save.version == SaveData.CurrentVersion)
                    {
                        Reconcile(save, content);
                        return save;
                    }
                    Debug.LogWarning($"[Save] Ignoring save file with version {save?.version}");
                }
            }
            catch (Exception exception) when (exception is IOException || exception is ArgumentException || exception is UnauthorizedAccessException)
            {
                // A damaged save is kept on disk under a new name instead of being overwritten.
                Debug.LogWarning($"[Save] Could not read the save file: {exception.Message}");
                TryKeepDamagedCopy();
            }
            return SaveData.NewGame(content);
        }

        public static void Save(SaveData save)
        {
            try
            {
                var temporary = SavePath + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(save));
                if (File.Exists(SavePath)) File.Replace(temporary, SavePath, null);
                else File.Move(temporary, SavePath);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogError($"[Save] Could not write the save file: {exception.Message}");
            }
        }

        // Adds crew added to the content since this save was written.
        static void Reconcile(SaveData save, GameContent content)
        {
            foreach (var member in content.crew)
            {
                if (save.crew.Find(c => c.id == member.id) == null) save.crew.Add(new CrewProgress { id = member.id });
            }
        }

        static void TryKeepDamagedCopy()
        {
            try
            {
                File.Move(SavePath, SavePath + ".damaged-" + DateTime.UtcNow.Ticks);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Save] Could not keep a copy of the damaged save: {exception.Message}");
            }
        }
    }
}
