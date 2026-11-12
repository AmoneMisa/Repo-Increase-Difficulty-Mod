using HarmonyLib;
using IncreaseHardMod.Modifiers;
using UnityEngine;
using C = IncreaseHardMod.Skills.SkillCatalog;

namespace IncreaseHardMod.Skills;

/// <summary>
/// The quest-only Contracts perks, and the surprise levels that share their hooks. All of these are decided by the
/// host (it runs monster AI, cart physics and loot damage), reading each player's synced perk levels.
///  - Cart Handling / Greased Wheels: the C.A.R.T. answers its pusher harder, so full carts climb and turn easier.
///  - Gentle Hands: loot you are carrying (or just dropped) loses less value when it breaks.
///  - Calm Presence / Short Memory: a monster chasing you gives up sooner once it can't see you. This reaches the
///    monsters that use the game's shared chase behaviour; monsters with their own hunting logic keep theirs.
///  - Soft Steps / Silent Night / Loud Facility: footsteps, landings, your voice and loot you handle alert monsters
///    from a shorter (or longer) distance.
/// </summary>
internal static class ContractPerks
{
    internal const float CartPerLevel = .2f, LootPerLevel = .05f, CalmPerLevel = .6f, NoisePerLevel = .15f;
    // A surprise level counts as this many perk levels for everyone.
    private const int ModifierLevels = 2;

    private static bool Has(ModifierType modifier) => ModifierManager.Current.Has(modifier);

    // ------------------------------------------------------------------ cart

    private static Vector3 _cartBefore;

    [HarmonyPatch(typeof(PhysGrabCart), "CartSteer")]
    private static class CartPush
    {
        [HarmonyPrefix]
        private static void Prefix(PhysGrabCart __instance)
        {
            var rb = __instance.rb;
            _cartBefore = rb != null ? rb.linearVelocity : Vector3.zero;
        }

        [HarmonyPostfix]
        private static void Postfix(PhysGrabCart __instance)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer() || !__instance.cartBeingPulled || __instance.rb == null) return;
            var grabbers = __instance.physGrabObject != null ? __instance.physGrabObject.playerGrabbing : null;
            var pusher = grabbers != null && grabbers.Count > 0 && grabbers[0] != null ? grabbers[0].playerAvatar : null;
            int levels = SkillEffects.Level(pusher, C.CartHandling) + (Has(ModifierType.GreasedWheels) ? ModifierLevels : 0);
            if (levels <= 0) return;
            // The game steps the cart's velocity toward where the pusher wants it; take a bigger step and allow a bit
            // more speed. Vertical speed is the game's (gravity, ramps).
            var rb = __instance.rb;
            var now = rb.linearVelocity;
            var step = new Vector3(now.x - _cartBefore.x, 0f, now.z - _cartBefore.z);
            var boosted = now + step * (CartPerLevel * levels);
            var flat = Vector3.ClampMagnitude(new Vector3(boosted.x, 0f, boosted.z), 5f * (1f + .1f * levels));
            rb.linearVelocity = new Vector3(flat.x, now.y, flat.z);
        }
    }

    // ------------------------------------------------------------------ loot damage

    [HarmonyPatch(typeof(PhysGrabObjectImpactDetector), nameof(PhysGrabObjectImpactDetector.Break))]
    private static class GentleLoot
    {
        [HarmonyPrefix]
        private static void Prefix(PhysGrabObjectImpactDetector __instance, ref float valueLost)
        {
            if (valueLost <= 0f || !__instance.isValuable || !SemiFunc.IsMasterClientOrSingleplayer()) return;
            var grab = __instance.physGrabObject;
            var handler = grab == null ? null
                : grab.playerGrabbing.Count > 0 && grab.playerGrabbing[0] != null ? grab.playerGrabbing[0].playerAvatar : grab.lastPlayerGrabbing;
            int level = SkillEffects.Level(handler, C.GentleHands);
            if (level > 0) valueLost *= 1f - LootPerLevel * level; // 15% less at the third level
            valueLost *= TradeOffs.Factor(handler, Fx.LootBreakLoss); // Salvager
        }
    }

    // ------------------------------------------------------------------ calm monsters

    [HarmonyPatch(typeof(EnemyStateChase), "Update")]
    private static class CalmChase
    {
        [HarmonyPostfix]
        private static void Postfix(EnemyStateChase __instance)
        {
            var enemy = __instance.Enemy;
            if (enemy == null || !enemy.MasterClient || enemy.CurrentState != EnemyState.Chase) return;
            var target = enemy.TargetPlayerAvatar;
            if (target == null || target.photonView == null || enemy.Vision == null) return;
            int levels = SkillEffects.Level(target, C.CalmPresence) + (Has(ModifierType.ShortMemory) ? ModifierLevels : 0);
            if (levels <= 0) return;
            if (enemy.Vision.VisionTriggered.TryGetValue(target.photonView.ViewID, out bool sees) && sees) return;
            // Out of sight: the chase's own "keep looking" timers run down faster.
            float extra = Time.deltaTime * CalmPerLevel * levels;
            __instance.VisionTimer = Mathf.Max(0f, __instance.VisionTimer - extra);
            __instance.StateTimer = Mathf.Max(0f, __instance.StateTimer - extra);
        }
    }

    // ------------------------------------------------------------------ noise

    // Who is making the noise right now, set around the game's noise-making methods.
    private static PlayerAvatar _noisy;

    private static float NoiseScale(PlayerAvatar player)
    {
        float scale = 1f;
        if (Has(ModifierType.SilentNight)) scale *= .6f;
        if (Has(ModifierType.LoudFacility)) scale *= 1.5f;
        int level = SkillEffects.Level(player, C.SoftSteps);
        if (level > 0) scale *= 1f - NoisePerLevel * level;
        scale *= TradeOffs.Factor(player, Fx.Noise); // Ghost Runner quieter, Scout louder
        return scale;
    }

    [HarmonyPatch(typeof(EnemyDirector), nameof(EnemyDirector.SetInvestigate))]
    private static class QuieterNoise
    {
        [HarmonyPrefix]
        private static void Prefix(ref float radius)
        {
            if (_noisy == null || radius <= 0f) return;
            radius *= NoiseScale(_noisy);
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.Footstep))]
    private static class FootstepNoise
    {
        [HarmonyPrefix] private static void Prefix(PlayerAvatar __instance) => _noisy = __instance;
        [HarmonyPostfix] private static void Postfix() => _noisy = null;
    }

    [HarmonyPatch(typeof(PlayerAvatar), "LandRPC")]
    private static class LandingNoise
    {
        [HarmonyPrefix] private static void Prefix(PlayerAvatar __instance) => _noisy = __instance;
        [HarmonyPostfix] private static void Postfix() => _noisy = null;
    }

    [HarmonyPatch(typeof(PlayerVoiceChat), "Update")]
    private static class VoiceNoise
    {
        [HarmonyPrefix] private static void Prefix(PlayerVoiceChat __instance) => _noisy = __instance.playerAvatar;
        [HarmonyPostfix] private static void Postfix() => _noisy = null;
    }

    [HarmonyPatch(typeof(PhysGrabObjectImpactDetector), nameof(PhysGrabObjectImpactDetector.EnemyInvestigate), typeof(float), typeof(Vector3))]
    private static class LootNoise
    {
        [HarmonyPrefix]
        private static void Prefix(PhysGrabObjectImpactDetector __instance)
        {
            var grab = __instance.physGrabObject;
            _noisy = grab == null ? null
                : grab.playerGrabbing.Count > 0 && grab.playerGrabbing[0] != null ? grab.playerGrabbing[0].playerAvatar : grab.lastPlayerGrabbing;
        }
        [HarmonyPostfix] private static void Postfix() => _noisy = null;
    }
}
