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
                    if (save != null && save.version <= SaveData.CurrentVersion)
                    {
                        if (save.version < SaveData.CurrentVersion) KeepBackup(save.version);
                        Campaign.Migrate(save, content);
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

        // Drops crew the content no longer has. Crew now join through the story, so none are added here.
        static void Reconcile(SaveData save, GameContent content)
        {
            save.crew.RemoveAll(c => Array.Find(content.crew, m => m.id == c.id) == null);

            // A saved party can be short on purpose, with the rest of the crew holding the helm.
            save.EnsureValidParty(save.party.Count == 0);
        }

        // A format change keeps the old file, so a bad migration can never cost a player their progress.
        static void KeepBackup(int version)
        {
            try
            {
                File.Copy(SavePath, SavePath + ".v" + version + ".backup", true);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Save] Could not back up the old save: {exception.Message}");
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
