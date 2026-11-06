using UnityEngine;

namespace IncreaseHardMod.Visuals;

/// <summary>Builds the Ice Pistol's ice textures and loads its shader in small steps from game start (see IceBlock.Prewarm).</summary>
internal sealed class IcePrewarm : MonoBehaviour
{
    private void Start() => StartCoroutine(IceBlock.Prewarm());
}
