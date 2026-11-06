using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using RepoUpgrades = REPOLib.Modules.Upgrades;

namespace IncreaseHardMod.Upgrades;

/// <summary>Multiplies the complete native sprint cost, including sprint-speed upgrades.
/// Carries fractional drain so float rounding cannot make sprinting free.</summary>
internal static class StaminaUpgrade
{
    public const string Id = "IHM_Stamina";
    private sealed class State { internal double Pending; internal float LastResult; internal bool HasLast; }
    private static readonly ConditionalWeakTable<PlayerController, State> States = new();

    public static void Register()
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value) return;
        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = Id,
            DisplayName = "Slower Stamina Use",
            MaxLevel = ModConfig.StaminaMaxLevel.Value,
        });
    }

    internal static float DrainMultiplier(float percent, float level)
    {
        float keep = 1f - Mathf.Clamp(percent, 0f, .5f);
        float cappedLevel = Mathf.Clamp(level, 0f, Mathf.Clamp(ModConfig.StaminaMaxLevel.Value, 1, 20));
        return Mathf.Pow(keep, cappedLevel);
    }

    internal static float SpendSprintEnergy(float current, float rawDrain, PlayerController controller)
    {
        var avatar = controller != null ? controller.playerAvatarScript : null;
        // Skill tree: Stamina Efficiency, on the old ladder of StaminaMaxLevel levels.
        float level = avatar != null && avatar.isLocal
            ? Skills.SkillEffects.Equivalent(avatar, Skills.SkillCatalog.StaminaEfficiency, ModConfig.StaminaMaxLevel.Value) : 0f;
        if (controller == null || level <= 0 || rawDrain <= 0f || float.IsNaN(rawDrain) || float.IsInfinity(rawDrain))
        {
            if (controller != null) States.Remove(controller);
            return current - rawDrain;
        }
        var state = States.GetValue(controller, _ => new State());
        // Recharge, jump, slide or an external stamina edit starts a new accumulation interval.
        if (!state.HasLast || current != state.LastResult) state.Pending = 0d;
        double drain = state.Pending + (double)rawDrain * DrainMultiplier(ModConfig.StaminaPercentPerLevel.Value, level);
        float result = (float)Math.Max(0d, (double)current - drain);
        result = Mathf.Min(current, result);
        state.Pending = drain - ((double)current - result);
        state.LastResult = result;
        state.HasLast = true;
        return result;
    }

    [HarmonyPatch(typeof(PlayerController), "FixedUpdate")]
    private static class SprintCost
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var drain = AccessTools.Field(typeof(PlayerController), "EnergySprintDrain");
            var energy = AccessTools.Field(typeof(PlayerController), "EnergyCurrent");
            var matches = new List<int>();
            for (int start = 0; start < code.Count; start++)
            {
                if (code[start].opcode != OpCodes.Ldfld || !Equals(code[start].operand, drain)) continue;
                // Stay inside this expression; never replace a later jump/slide deduction.
                for (int i = start + 1; i + 1 < code.Count && i < start + 48; i++)
                {
                    if (code[i].opcode != OpCodes.Stfld || !Equals(code[i].operand, energy)) continue;
                    if (code[i - 1].opcode == OpCodes.Sub) matches.Add(i - 1);
                    break;
                }
            }
            if (matches.Count != 1)
            {
                Plugin.Log.LogWarning("[Stamina] Native sprint expression changed; sprint reduction patch was not applied.");
                return code;
            }
            int index = matches[0];
            var load = new CodeInstruction(OpCodes.Ldarg_0);
            load.labels.AddRange(code[index].labels);
            load.blocks.AddRange(code[index].blocks);
            code[index] = load;
            code.Insert(index + 1, new CodeInstruction(OpCodes.Call,
                typeof(StaminaUpgrade).GetMethod(nameof(SpendSprintEnergy), BindingFlags.Static | BindingFlags.NonPublic)));
            return code;
        }
    }
}
