using IncreaseHardMod.Modifiers;
using IncreaseHardMod.Networking;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Core of the LevelTimer modifier — a countdown that, when it hits zero, restarts
/// the current level and rerolls it to a fresh layout (progress is NOT lost; the
/// level is regenerated, exactly like loading a new one). Kills add time, player
/// deaths remove time, and completing an extraction quota adds time.
///
/// Authority model: the HOST owns the clock. It ticks it down every frame, applies
/// the kill/death/quota deltas (those events only fire host-side), and periodically
/// broadcasts the authoritative remaining time so clients' HUDs stay in sync. Clients
/// smooth the number locally between broadcasts for a non-janky display, but never
/// make decisions from it. When the host's clock reaches zero it calls
/// RunManager.ChangeLevel(_completedLevel:false, _levelFailed:false, RunLevel) — the
/// RunLevel path runs SetRunLevel(), which picks a new random run level WITHOUT
/// incrementing levelsCompleted, i.e. a clean restart+reroll rather than a fail.
///
/// The state is plain static data; ModNetwork.Update drives Tick() (ModNetwork is a
/// persistent DontDestroyOnLoad object, so the timer keeps ticking across the frame
/// after a level change until ResetForNewLevel clears it).
/// </summary>
internal static class LevelTimerController
{
    private static float _remaining;
    private static bool _running;
    private static float _broadcastCooldown;

    /// <summary>Remaining seconds, for the HUD.</summary>
    public static float Remaining => _remaining;

    /// <summary>True while a LevelTimer countdown is active (HUD visibility).</summary>
    public static bool Running => _running;

    /// <summary>Host-only: begin the countdown for a freshly generated LevelTimer level.</summary>
    public static void StartForLevel()
    {
        _remaining = Mathf.Max(1f, ModConfig.LevelTimerSeconds.Value);
        _running = true;
        _broadcastCooldown = 0f;
        Broadcast();
    }

    /// <summary>Called from ModifierManager on every level change to clear stale state.</summary>
    public static void ResetForNewLevel()
    {
        _running = false;
        _remaining = 0f;
    }

    public static void AddTime(float seconds)
    {
        if (!_running || !SemiFunc.IsMasterClientOrSingleplayer())
            return;
        _remaining += seconds;
        Broadcast();
    }

    public static void RemoveTime(float seconds)
    {
        if (!_running || !SemiFunc.IsMasterClientOrSingleplayer())
            return;
        _remaining = Mathf.Max(0f, _remaining - seconds);
        Broadcast();
    }

    /// <summary>Clients apply the host's authoritative snapshot.</summary>
    public static void ApplyNetworkState(float remaining, bool running)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer())
            return; // host is authoritative; ignore echoes
        _remaining = remaining;
        _running = running;
    }

    public static void Tick(float dt)
    {
        if (!_running)
            return;

        // Only meaningful while a LevelTimer level is actually being played.
        if (!ModifierManager.Current.Has(ModifierType.LevelTimer) || !SemiFunc.RunIsLevel())
        {
            _running = false;
            return;
        }

        if (SemiFunc.IsMasterClientOrSingleplayer())
        {
            _remaining -= dt;

            _broadcastCooldown -= dt;
            if (_broadcastCooldown <= 0f)
                Broadcast();

            if (_remaining <= 0f)
            {
                _remaining = 0f;
                _running = false;
                Broadcast();
                RestartLevel();
            }
        }
        else
        {
            // Client-side smoothing between broadcasts (display only).
            _remaining = Mathf.Max(0f, _remaining - dt);
        }
    }

    private static void RestartLevel()
    {
        var run = RunManager.instance;
        if (run == null)
            return;

        // RunLevel => SetRunLevel(): reroll to a new random run level, no progress
        // increment, not a fail. Regenerates the map like a fresh load.
        run.ChangeLevel(false, false, RunManager.ChangeLevelType.RunLevel);
        Plugin.Log.LogInfo("[LevelTimer] Time expired — restarting and rerolling the level.");
    }

    private static void Broadcast()
    {
        _broadcastCooldown = 0.5f;
        if (SemiFunc.IsMultiplayer())
            ModNetwork.Instance?.BroadcastLevelTimer(_remaining, _running);
    }
}
