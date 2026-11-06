# HUD toolkit

Shared building blocks for the mod's player-facing HUD. Every piece uses the game's own TextMeshPro font and
material (through `Patches/Effects/NativeHudPanel.cs`) and draws no backplates. That covers most of the
[HUD text style rules](../../AGENTS.md), but new HUD must still be checked in-game against the reference image.

| Piece | File | Use it for |
|---|---|---|
| Vitals rows | `HudVitals.cs` | An icon + value + `/max` row under health and energy, cloned from the native energy row |
| Corner stack | `HudStack.cs` | Panels that stack in a corner instead of overlapping |
| List panel | `HudListPanel.cs` | A titled list (name + value rows, footer, paging), like the Shopping List |
| Paging | `HudLayout.cs` (`HudPaging`) | Splitting long text into pages without cutting an entry |
| Notifications | `HudNotify.cs` | Short toasts above the inventory bar and status lines in the upper centre |
| Style | `HudStyle.cs` | Text sizes, margins and the shared status colours |
| Icons | `HudIcons.cs` | White silhouette icons drawn in code (tinted where used) |

`HudRuntime` (added in `Plugin.cs`) drives the vitals rows and notifications every frame. The engine-free
parts (`HudLayout.cs`) are checked by `tests/HudToolkitTests`.

## Add a vitals row

Register once at startup (see `Patches/Effects/ArmorHud.cs`). `Read` runs every frame, so keep it cheap and
reuse strings when the numbers have not changed.

```csharp
HudVitals.Register(new VitalsRowSpec
{
    Id = "armor", Order = 10,               // lower order = closer to the energy row
    Icon = () => HudIcons.Shield,
    ValueColor = HudStyle.Armor, MaxColor = HudStyle.ArmorMax,
    Read = () => level > 0 ? VitalsReading.Of("10", "20%") : VitalsReading.Hidden,
});
```

Visible rows close up in order, so a hidden row leaves no gap.

## Add a list panel

Own it from a MonoBehaviour (see `Patches/Effects/ShoppingListHud.cs`):

```csharp
_list ??= new HudListPanel("shopping", HudCorner.TopRight, order: 100) { Title = "SHOPPING LIST" };
_list.SetRows(rows);                      // HudListRow(left, right, colour)
_list.Footer = "TOTAL  $12K / $40K";
_list.Show();                             // every frame it is visible; _list.Hide() otherwise
// OnDestroy: _list.Destroy();
```

Set `PageKeys = true` to let PgUp / PgDn turn pages once a list has more than `RowsPerPage` rows.

## Stack your own panel

For a custom `NativeHudPanel`, set its size, then call `HudStack.Place(surface, corner, id, order)` every frame
it shows and `HudStack.Release(id)` when it hides. Orders in use at the top right: 100 Shopping List. The
native-canvas modifier list (Level Info) starts below whatever the top-right stack holds.

## Notifications

```csharp
HudNotify.Toast("Relocated stuck loot: Cannon", HudStyle.Info, seconds: 3f, key: "unstuck");
HudNotify.Status("ihm.frozen", $"FROZEN  {remaining:0.0}s", HudStyle.Ice);  // call every frame it applies
HudNotify.Clear("ihm.frozen");                                              // or let it fade out
```

A message with the key of a visible one replaces it instead of adding a line. Each channel shows at most three.

## Add an icon

```csharp
internal static Sprite Bolt => HudIcons.Silhouette("bolt", (u, v) => /* inside test: u -1..1, v 0..1 top-down */);
```
