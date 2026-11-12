using HarmonyLib;
using IncreaseHardMod.Modifiers;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// ChangeVoices modifier — every player's proximity-voice is pitch-shifted for the
/// special level, so the squad sounds chipmunky/booming.
///
/// The game already has the exact hook we need: PlayerVoiceChat.OverridePitch sets a
/// target pitch multiplier that OverridePitchLogic lerps audioSource.pitch toward.
/// The catch is that it's TEMPORARY — OverridePitchTick counts overridePitchTimer
/// down (in FixedUpdate) and clears it, so a single call decays back to normal. To
/// hold a pitch for the whole level we simply re-arm it every FixedUpdate (this
/// postfix runs right after the game's OverridePitchTick), refreshing the timer so it
/// never expires.
///
/// Each PlayerVoiceChat instance exists on every client (one per player) and
/// audioSource.pitch is applied locally on the LISTENER side, so to make a player
/// sound the same to everyone we just need every client to pick the SAME pitch for
/// that player. We derive the pitch deterministically from the player's steamID via a
/// stable FNV-1a hash (NOT string.GetHashCode, which can vary), so no extra network
/// sync is needed — every client computes an identical value.
///
/// We yield to the game's own transient pitch effects (walkie-talkie distortion,
/// item gags, etc.): if some other override is currently active with a different
/// target we leave it alone, and re-take control only once it has decayed.
/// </summary>
[HarmonyPatch(typeof(PlayerVoiceChat), nameof(PlayerVoiceChat.FixedUpdate))]
internal static class ChangeVoicesEffect
{
    // How quickly audioSource.pitch eases toward our target, and how long each
    // re-arm keeps the override alive. The timer only has to outlast one FixedUpdate;
    // we keep a small margin so playback stays perfectly steady between re-arms.
    private const float LerpTime = 0.15f;
    private const float HoldTimer = 0.3f;
    private const float Epsilon = 0.001f;

    [HarmonyPostfix]
    private static void Postfix(PlayerVoiceChat __instance)
    {
        if (!ModifierManager.Current.Has(ModifierType.ChangeVoices))
            return;

        if (__instance == null || __instance.playerAvatar == null)
            return;

        string steamId = SemiFunc.PlayerGetSteamID(__instance.playerAvatar);
        if (string.IsNullOrEmpty(steamId))
            return;

        float target = PitchForPlayer(steamId);

        // If a different override is currently playing (e.g. walkie distortion), let
        // it finish rather than fighting it; we'll reclaim the voice once it decays.
        if (__instance.overridePitchIsActive &&
            Mathf.Abs(__instance.overridePitchMultiplierTarget - target) > Epsilon)
            return;

        __instance.OverridePitch(target, LerpTime, LerpTime, HoldTimer);
    }

    /// <summary>
    /// Map a steamID to a stable pitch in [0.6, 1.5], biased away from ~1.0 so the
    /// change is always audible. Uses FNV-1a so all clients agree regardless of
    /// runtime hash randomization.
    /// </summary>
    private static float PitchForPlayer(string steamId)
    {
        uint hash = 2166136261u;
        foreach (char c in steamId)
        {
            hash ^= c;
            hash *= 16777619u;
        }

        float t = (hash % 1000u) / 1000f; // 0..1, deterministic
        // Two bands (low/high) so we never land near the unchanged 1.0.
        return (t < 0.5f)
            ? 0.6f + t * 0.6f          // 0.60 .. 0.90
            : 1.15f + (t - 0.5f) * 0.7f; // 1.15 .. 1.50
    }
}
