using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Patches;

/// <summary>
/// Auto-backup the run save before REPO wipes it.
///
/// When you fail/wipe a level, <c>DataDirector.SaveDeleteCheck</c> permanently deletes
/// the run's save folder (<c>persistentDataPath/saves/&lt;name&gt;</c>) via
/// <c>StatsManager.SaveFileDelete</c>. This prefix runs first and, using the SAME
/// decision the game uses, decides whether a delete is about to happen. If so it copies
/// the save folder to <c>persistentDataPath/saves_IncreaseHardMod_backups/&lt;name&gt;_&lt;timestamp&gt;</c>
/// (keeping only the newest N per save), and — if configured — SKIPS the deletion
/// entirely so the save stays in the normal save list and can be reloaded.
///
/// We patch <c>SaveDeleteCheck</c> (the automatic fail/wipe path) rather than the raw
/// <c>StatsManager.SaveFileDelete</c> so the manual "delete save" menu button keeps
/// working normally.
/// </summary>
[HarmonyPatch(typeof(DataDirector), "SaveDeleteCheck")]
internal static class AutoBackupSavePatch
{
    [HarmonyPrefix]
    private static bool Prefix(bool _leaveGame)
    {
        if (!ModConfig.AutoBackupEnabled.Value)
            return true; // feature off: let the game do its thing.

        // Only the host/singleplayer owns the save, mirroring the game's own guard.
        if (!SemiFunc.IsMasterClientOrSingleplayer())
            return true;

        if (!WouldDelete(_leaveGame))
            return true; // no deletion imminent: nothing to back up, run original.

        try
        {
            BackupCurrentSave();
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[AutoBackup] Backup failed harmlessly: {e.Message}");
        }

        if (ModConfig.AutoBackupKeepSave.Value)
        {
            Plugin.Log.LogInfo("[AutoBackup] Kept the failed save loadable (skipped REPO's delete).");
            return false; // skip SaveDeleteCheck entirely -> save is NOT deleted.
        }

        return true; // backed up, but let REPO delete the original as usual.
    }

    /// <summary>
    /// Re-implements DataDirector.SaveDeleteCheck's own condition so we only act when
    /// the game is actually about to delete the save.
    /// </summary>
    private static bool WouldDelete(bool leaveGame)
    {
        var rm = RunManager.instance;
        if (rm == null)
            return false;
        if (rm.levelPrevious == rm.levelTutorial ||
            rm.levelPrevious == rm.levelMainMenu ||
            rm.levelPrevious == rm.levelRecording)
            return false;

        if (SemiFunc.RunIsArena())
            return true;
        if (rm.allPlayersDead &&
            rm.levelPrevious != rm.levelMainMenu &&
            rm.levelPrevious != rm.levelLobbyMenu &&
            rm.levelPrevious != rm.levelTutorial &&
            rm.levelPrevious != rm.levelLobby &&
            !SemiFunc.IsLevelShop(rm.levelPrevious) &&
            rm.levelPrevious != rm.levelRecording)
            return true;
        if (leaveGame && rm.levelsCompleted == 0)
            return true;

        return false;
    }

    private static void BackupCurrentSave()
    {
        string name = StatsManager.instance?.saveFileCurrent;
        if (string.IsNullOrEmpty(name))
            return;

        string source = Path.Combine(Application.persistentDataPath, "saves", name);
        if (!Directory.Exists(source))
        {
            Plugin.Log.LogWarning($"[AutoBackup] Save folder to back up not found: {source}");
            return;
        }

        string backupsRoot = Path.Combine(Application.persistentDataPath, "saves_IncreaseHardMod_backups");
        Directory.CreateDirectory(backupsRoot);

        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string dest = Path.Combine(backupsRoot, $"{name}_{stamp}");
        CopyDirectory(source, dest);
        Plugin.Log.LogInfo($"[AutoBackup] Backed up save '{name}' -> {dest}");

        Prune(backupsRoot, name);
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (string file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
        foreach (string dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }

    /// <summary>Keep only the newest MaxBackupsPerSave backups for this save name.</summary>
    private static void Prune(string backupsRoot, string name)
    {
        int max = ModConfig.AutoBackupMaxPerSave.Value;
        if (max <= 0)
            return;

        var backups = Directory.GetDirectories(backupsRoot, $"{name}_*")
            .OrderByDescending(d => Directory.GetCreationTimeUtc(d))
            .ToList();

        for (int i = max; i < backups.Count; i++)
        {
            try
            {
                Directory.Delete(backups[i], recursive: true);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[AutoBackup] Failed to prune old backup '{backups[i]}': {e.Message}");
            }
        }
    }
}
