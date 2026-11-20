using System;

namespace IncreaseHardMod.FableRealm;

/// <summary>
/// Sizes of handheld-map tiles for Fable Realm rooms. Plain floats, no engine types: tests/FableTests checks them.
///
/// The game's map puts a tile at (marker position x Map.Scale) and gives it the marker's scale. Vanilla rooms use
/// unit markers on a 5 m grid with Map.Scale 0.1, so a unit 1x1 floor tile is 0.5 map units across. A tile is
/// therefore sized in map units: a box W metres wide needs scale W x Map.Scale / tileSize, never W / tileSize
/// (that drew every room ten times its size and buried its pits, stairs and neighbours under one big floor).
/// </summary>
internal static class FableMapMath
{
    /// <summary>Marker scale along one axis for a box <paramref name="metres"/> long.</summary>
    internal static float TileScale(float metres, float mapScale, float tileSize) =>
        tileSize > 1e-4f ? metres * mapScale / tileSize : 1f;

    /// <summary>
    /// How far (in level metres, along one axis) the marker sits before the box's centre: the tile's pivot is
    /// <paramref name="tileCentre"/> map units before its middle, which at the fitted scale is that fraction of the box.
    /// </summary>
    internal static float PivotOffset(float metres, float tileCentre, float tileSize) =>
        tileSize > 1e-4f ? metres * tileCentre / tileSize : 0f;

    /// <summary>Recipe elements drawn as walls: the shell's "wall-" pieces and the older rooms' "front-wall" and the like.</summary>
    internal static bool IsWall(string elementName) =>
        elementName != null && elementName.IndexOf("wall", StringComparison.Ordinal) >= 0 &&
        !elementName.StartsWith("pit-", StringComparison.Ordinal);

    /// <summary>A wall box standing on the room floor; pit linings below the floor and low kerbs are not drawn.</summary>
    internal static bool IsWallBox(float height, float centreY) => height >= 2f && centreY - height * .5f > -.5f;
}
