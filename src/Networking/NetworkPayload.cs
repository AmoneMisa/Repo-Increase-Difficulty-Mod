using System;

namespace IncreaseHardMod.Networking;

/// <summary>Validate message shapes before Photon callbacks read or mutate gameplay state.</summary>
internal static class NetworkPayload
{
    internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    internal static bool Host(int sender, int host, bool inRoom) => inRoom && host > 0 && sender == host;
    private static bool Envelope(object raw, int min, int max, out object[] data)
    {
        data = raw as object[];
        return data != null && data.Length >= min && data.Length <= max;
    }
    private static bool Text(object value, int limit) => value is string text && text.Length <= limit;

    internal static bool Timer(object raw, out object[] data) => Envelope(raw, 2, 2, out data) &&
        data[0] is float seconds && Finite(seconds) && seconds >= 0f && data[1] is bool;

    internal static bool Ping(object raw, out object[] data) => Envelope(raw, 6, 7, out data) &&
        data[0] is byte kind && kind <= 8 && data[1] is float x && Finite(x) &&
        data[2] is float y && Finite(y) && data[3] is float z && Finite(z) &&
        Text(data[4], 512) && Text(data[5], 128) && (data.Length == 6 || data[6] is int id && id >= 0);

    internal static bool Candidates(object raw, out object[] data)
    {
        if (!Envelope(raw, 5, 5, out data) || data[0] is not int || data[1] is not string[] names ||
            names.Length == 0 || names.Length > 6 || data[2] is not float seconds || !Finite(seconds) || seconds < 0f ||
            data[3] is not bool || data[4] is not int winner || winner < -1 || winner >= names.Length) return false;
        foreach (var name in names) if (string.IsNullOrEmpty(name) || name.Length > 256) return false;
        return true;
    }

    internal static bool Ballot(object raw, int sender, out object[] data) => Envelope(raw, 4, 4, out data) &&
        data[0] is int && data[1] is int actor && actor == sender && sender > 0 &&
        data[2] is int choice && choice >= 0 && choice < 6 && Text(data[3], 128);

    internal static bool BatteryDrain(object raw, out object[] data) => Envelope(raw, 2, 2, out data) &&
        data[0] is int id && id > 0 && data[1] is float percent && Finite(percent) && percent > 0f && percent <= 100f;

    internal static bool Repair(object raw, out object[] data) => Envelope(raw, 2, 2, out data) &&
        data[0] is int id && id > 0 && data[1] is float value && Finite(value) && value >= 0f;

    internal static bool QuestBuff(object raw, out object[] data) => Envelope(raw, 2, 2, out data) &&
        data[0] is byte stat && Enum.IsDefined(typeof(Quests.StatKind), stat) && data[1] is int amount && amount > 0;

    // Skill tree: [op (0 buy, 1 respec, 2 personal-quest points), node id, level count] from a client; [op, result, node id] back from the host.
    internal static bool SkillRequest(object raw, out object[] data) => Envelope(raw, 3, 3, out data) &&
        data[0] is byte op && op <= 2 && Text(data[1], 64) && data[2] is int count && count >= 1 && count <= 10;

    internal static bool SkillResult(object raw, out object[] data) => Envelope(raw, 3, 3, out data) &&
        data[0] is byte op && op <= 2 && data[1] is byte result && result <= 20 && Text(data[2], 64);

    internal static bool QuestState(object raw, out object[] data)
    {
        // 15 values per quest: ..., title, flags (1 done, 2 failed, 4 contract), skill points, contract perk (255 none).
        const int fields = 15;
        if (!Envelope(raw, 1, 1 + 5 * fields, out data) || data[0] is not int count || count < 0 || count > 5 ||
            data.Length != 1 + count * fields) return false;
        for (int i = 1; i < data.Length; i += fields)
            if (data[i] is not int || data[i + 1] is not byte || data[i + 2] is not byte ||
                data[i + 3] is not byte || data[i + 4] is not int || data[i + 5] is not int ||
                !Text(data[i + 6], 256) || data[i + 7] is not byte || data[i + 8] is not int ||
                data[i + 9] is not byte || data[i + 10] is not byte || !Text(data[i + 11], 512) ||
                data[i + 12] is not byte flags || flags > 7 || data[i + 13] is not byte points || points > 10 ||
                data[i + 14] is not byte) return false;
        return true;
    }
}
