using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Visuals;

/// <summary>
/// The renderers that make up a player's visible body, and their bounds. A player's visuals hierarchy can
/// hold meshes parked far from the body (hidden or stashed parts); including them stretched the bounds so a
/// net or ice block was built around a point away from the player. Only renderers near the avatar count,
/// held items and mod decorations never do, and a player-sized capsule is the fallback.
/// </summary>
internal static class PlayerBody
{
    private const float MaxDistance = 1.6f, MaxSize = 3f;

    internal static List<Renderer> Renderers(PlayerAvatar avatar, out Bounds bounds)
    {
        var list = new List<Renderer>();
        var anchor = avatar.transform.position + Vector3.up * .8f;
        bounds = new Bounds(anchor, new Vector3(.6f, 1.6f, .6f));
        if (avatar.playerAvatarVisuals == null) return list;
        bool found = false;
        foreach (var r in avatar.playerAvatarVisuals.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || !r.enabled || !r.gameObject.activeInHierarchy ||
                Decoration.Is(r) || r.GetComponentInParent<ItemAttributes>() != null) continue;
            var b = r.bounds;
            if ((b.center - anchor).sqrMagnitude > MaxDistance * MaxDistance || b.size.magnitude > MaxSize) continue;
            list.Add(r);
            if (!found) { bounds = b; found = true; } else bounds.Encapsulate(b);
        }
        return list;
    }
}
