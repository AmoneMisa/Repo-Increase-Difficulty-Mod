using System.Collections.Generic;
using HarmonyLib;
using IncreaseHardMod.Quests;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Event taps that feed the quest system. Mirrors the LevelTimer effect's split: the clock
/// and all authority live in <see cref="QuestManager"/>; these patches are just where the
/// game's kill/pit/death/extraction/damage/level events get forwarded.
///
/// Authority notes (the manager re-checks too):
/// - EnemyHealth.Death only runs on the host, so kill/pit feeds are naturally host-only.
/// - ChangeLevel runs on every client (it early-returns for non-hosts AFTER our prefix), so
///   its prefix is a reliable per-client "level finished" hook for personal quests.
/// - PlayerHealth.Hurt is filtered to the LOCAL player so the personal no-damage quest only
///   reacts to our own character.
/// </summary>
internal static class QuestEffect
{
    // Enemies that touched a death pit recently, so EnemyHealth.Death can tell a pit kill
    // apart from a normal kill. Keyed by the Enemy, valued by the time of contact.
    private static readonly Dictionary<Enemy, float> _recentPitContact = new();
    private const float PitContactWindow = 3f;

    // The most recent weapon that hurt each enemy, so EnemyHealth.Death can attribute the
    // kill to a gun or a melee weapon (for the melee-only / no-gun-kills quests).
    private static readonly Dictionary<Enemy, WeaponSource> _lastWeapon = new();
    // Enemies a gun or melee weapon has hurt: a later shove or throw no longer counts as a bare-handed kill.
    private static readonly HashSet<Enemy> _armedHit = new();

    /// <summary>Credits a hit dealt without a HurtCollider (e.g. the Wizard Staff's lightning) as a gun hit.</summary>
    internal static void NoteGunHit(Enemy enemy)
    {
        if (enemy == null) return;
        _armedHit.Add(enemy);
        _lastWeapon[enemy] = WeaponSource.Gun;
    }

    // Start the quest set once a real run level finishes generating.
    [HarmonyPatch(typeof(LevelGenerator), "GenerateDone")]
    private static class OnGenerated
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (!SemiFunc.RunIsLevel())
                return;
            QuestManager.OnLevelGenerated();
        }
    }

    // Finalize survival/personal quests as the level flips. _completedLevel is the game's
    // own "this level was actually completed" flag.
    [HarmonyPatch(typeof(RunManager), "ChangeLevel")]
    private static class OnLevelChange
    {
        [HarmonyPrefix]
        private static void Prefix(bool _completedLevel)
        {
            QuestManager.OnLevelCompleted(_completedLevel);
        }
    }

    // Every enemy death: count the kill (and a pit kill if it followed a pit shove).
    [HarmonyPatch(typeof(EnemyHealth), "Death")]
    private static class OnKill
    {
        [HarmonyPostfix]
        private static void Postfix(EnemyHealth __instance)
        {
            var parent = __instance != null ? __instance.GetComponentInParent<EnemyParent>() : null;
            var enemy = __instance != null ? __instance.enemy : null;

            WeaponSource weapon = WeaponSource.Other;
            if (enemy != null && _lastWeapon.TryGetValue(enemy, out var w))
            {
                _lastWeapon.Remove(enemy);
                weapon = w;
            }
            bool armed = enemy != null && _armedHit.Remove(enemy);
            if (armed && weapon == WeaponSource.Hands)
                weapon = WeaponSource.Other;
            // An enemy that died while a player had hold of it (or just let go) was slammed or thrown by hand.
            if (!armed && weapon == WeaponSource.Other && enemy != null && HeldByPlayer(enemy))
                weapon = WeaponSource.Hands;

            QuestManager.OnEnemyKilled(parent, weapon);

            if (enemy != null && _recentPitContact.TryGetValue(enemy, out float t))
            {
                _recentPitContact.Remove(enemy);
                if (Time.time - t <= PitContactWindow)
                    QuestManager.OnPitKill();
            }
        }
    }

    // Track what last hurt each enemy: a death pit (for pit kills) and/or a weapon (for
    // melee-only / no-gun quests). A gun's / melee's HurtCollider sits under its item, and a
    // bullet's under the bullet, so a parent lookup identifies the weapon type.
    [HarmonyPatch(typeof(HurtCollider), "EnemyHurt")]
    private static class OnEnemyHurt
    {
        [HarmonyPrefix]
        private static void Prefix(HurtCollider __instance, Enemy _enemy)
        {
            if (__instance == null || _enemy == null)
                return;

            if (__instance.deathPit)
                _recentPitContact[_enemy] = Time.time;

            var weapon = Classify(__instance);
            if (weapon == WeaponSource.Gun || weapon == WeaponSource.Melee)
                _armedHit.Add(_enemy);
            if (weapon != WeaponSource.Other)
                _lastWeapon[_enemy] = weapon;
        }

        // A grabbed or just-released object (a thrown valuable, crate or the like) is a hand attack.
        private static bool HandHeld(HurtCollider collider)
        {
            var body = collider.GetComponentInParent<PhysGrabObject>();
            return body != null && (body.grabbed || body.lastPlayerGrabbing != null);
        }

        private static WeaponSource Classify(HurtCollider collider)
        {
            if (collider.GetComponentInParent<ItemMelee>() != null)
                return WeaponSource.Melee;
            if (collider.GetComponentInParent<ItemGun>() != null ||
                collider.GetComponentInParent<ItemGunBullet>() != null)
                return WeaponSource.Gun;
            return HandHeld(collider) ? WeaponSource.Hands : WeaponSource.Other;
        }
    }

    private static bool HeldByPlayer(Enemy enemy)
    {
        if (!enemy.HasRigidbody || enemy.Rigidbody == null) return false;
        var body = enemy.Rigidbody.physGrabObject;
        return body != null && (body.grabbed || body.lastPlayerGrabbing != null);
    }

    // Team death count for the no-deaths survival quest.
    [HarmonyPatch(typeof(PlayerAvatar), "PlayerDeathRPC")]
    private static class OnPlayerDeath
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            QuestManager.OnPlayerDeath();
        }
    }

    // Extraction quota closed: re-evaluate haul-value quests.
    [HarmonyPatch(typeof(ExtractionPoint), "StateSet")]
    private static class OnExtraction
    {
        [HarmonyPostfix]
        private static void Postfix(ExtractionPoint.State newState)
        {
            if (newState == ExtractionPoint.State.Success)
                QuestManager.OnExtractionSuccess();
        }
    }

    // Intact-extraction quest. Valuables are sucked into the tube one at a time via
    // DestroyTheFirstPhysObjectsInHaulList (paced) and any stragglers via
    // DestroyAllPhysObjectsInHaulList (final sweep). We read each valuable's value in a PREFIX,
    // before the object is destroyed; QuestManager de-dupes across the two hooks by instance ID.
    [HarmonyPatch(typeof(ExtractionPoint), "DestroyTheFirstPhysObjectsInHaulList")]
    private static class OnExtractFirst
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            var rd = RoundDirector.instance;
            if (rd == null || rd.dollarHaulList == null || rd.dollarHaulList.Count == 0)
                return;
            var go = rd.dollarHaulList[0];
            if (go != null)
                QuestManager.OnValuableExtracted(go.GetComponent<ValuableObject>());
        }
    }

    [HarmonyPatch(typeof(ExtractionPoint), "DestroyAllPhysObjectsInHaulList")]
    private static class OnExtractAll
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            var rd = RoundDirector.instance;
            if (rd == null || rd.dollarHaulList == null)
                return;
            foreach (var go in rd.dollarHaulList)
                if (go != null)
                    QuestManager.OnValuableExtracted(go.GetComponent<ValuableObject>());
        }
    }

    // Local player took real damage: fail our personal no-damage quest.
    [HarmonyPatch(typeof(PlayerHealth), "Hurt")]
    private static class OnLocalDamage
    {
        [HarmonyPrefix]
        private static void Prefix(PlayerHealth __instance, int damage, bool hurtByHeal)
        {
            if (hurtByHeal || damage <= 0 || __instance == null)
                return;
            if (__instance.playerAvatar != PlayerAvatar.instance)
                return;
            QuestManager.OnLocalPlayerDamaged();
        }
    }

    // An enemy newly stunned (not one already stunned being re-stunned).
    [HarmonyPatch(typeof(EnemyStateStunned), "Set")]
    private static class OnStun
    {
        [HarmonyPrefix]
        private static void Prefix(EnemyStateStunned __instance, out bool __state) => __state = __instance.stunTimer <= 0f;

        [HarmonyPostfix]
        private static void Postfix(EnemyStateStunned __instance, bool __state)
        {
            if (__state && __instance.stunTimer > 0f)
                QuestManager.OnEnemyStunned();
        }
    }

    // A teammate brought back (the truck's revive and the mod's free auto-revive do not count).
    [HarmonyPatch(typeof(PlayerAvatar), "ReviveRPC")]
    private static class OnRevive
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerAvatar __instance, bool _revivedByTruck) =>
            QuestManager.OnPlayerRevived(__instance, _revivedByTruck);
    }

    // Halloween: the moon accepted an offering at an extraction point ("A moon is judging").
    [HarmonyPatch(typeof(ExtractionHalloweenTower), "HalloweenRewardCommitRPC")]
    private static class OnMoonApproval
    {
        [HarmonyPostfix]
        private static void Postfix(ExtractionHalloweenTower __instance)
        {
            if (__instance.recoveredBonus > 0 && __instance.IsApproved())
                QuestManager.OnMoonApproved();
        }
    }
}
