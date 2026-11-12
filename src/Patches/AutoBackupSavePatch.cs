using System;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Patches;

/// <summary>
/// Preserve the run checkpoint when REPO would automatically delete it.
///
/// When you fail/wipe a level, <c>DataDirector.SaveDeleteCheck</c> permanently deletes
/// the run's save folder (<c>persistentDataPath/saves/&lt;name&gt;</c>) via
/// <c>StatsManager.SaveFileDelete</c>. This prefix runs first and, using the SAME
/// decision the game uses, decides whether a delete is about to happen. If so it copies
/// the save folder to <c>persistentDataPath/saves_IncreaseHardMod_backups/&lt;name&gt;_&lt;timestamp&gt;</c>
/// when backups are enabled. Deletion is always skipped, even if backups are disabled
/// or copying fails, so the checkpoint can be loaded from the menu without restarting.
/// Backup snapshots are retained indefinitely.
///
/// We patch <c>SaveDeleteCheck</c> (the automatic fail/wipe path) rather than the raw
/// <c>StatsManager.SaveFileDelete</c> so the manual "delete save" menu button keeps
/// working normally.
/// </summary>
[HarmonyPatch(typeof(DataDirector), nameof(DataDirector.SaveDeleteCheck))]
internal static class AutoBackupSavePatch
{
    [HarmonyPrefix]
    private static bool Prefix(bool _leaveGame)
    {
        // Only the host/singleplayer owns the save, mirroring the game's own guard.
        if (!SemiFunc.IsMasterClientOrSingleplayer())
            return true;

        if (!WouldDelete(_leaveGame))
            return true; // no deletion imminent: nothing to back up, run original.

        if (ModConfig.AutoBackupEnabled.Value)
        {
            try
            {
                BackupCurrentSave();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[AutoBackup] Backup failed; the original save is still protected: {e.Message}");
            }
        }

        Plugin.Log.LogInfo("[AutoBackup] Preserved the run checkpoint. Load it from the save menu to continue without restarting.");
        return false; // Automatic deletion must never depend on backup settings or success.
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

        // Never overwrite an earlier snapshot, including repeated checks in the same second.
        string stamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss_fffffff") + "_" + Guid.NewGuid().ToString("N");
        string dest = Path.Combine(backupsRoot, $"{name}_{stamp}");
        CopyDirectory(source, dest);
        Plugin.Log.LogInfo($"[AutoBackup] Backed up save '{name}' -> {dest}");
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (string file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: false);
        foreach (string dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }
}
