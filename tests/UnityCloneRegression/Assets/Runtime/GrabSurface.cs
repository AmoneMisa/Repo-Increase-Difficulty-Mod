using UnityEngine;

namespace IncreaseHardMod;

/// <summary>
/// REPO's PhysGrabber only starts a grab when its ray hits a collider tagged "Phys Grab Object"; any other
/// collider in front simply blocks the grab. Every collider the mod builds for an item or valuable must be
/// marked with this, or that part of the object cannot be grabbed (the Spritz and Flamethrower bug).
/// </summary>
internal static class GrabSurface
{
    internal const string Tag = "Phys Grab Object";

    internal static GameObject Mark(GameObject obj)
    {
        if (obj == null) return obj;
        // The tag only exists in the game's project; the Unity test fixtures do not define it.
        try { obj.tag = Tag; }
        catch (UnityException) { }
        return obj;
    }
}
