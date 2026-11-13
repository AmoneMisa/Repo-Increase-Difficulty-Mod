# Project rules

## HUD text style — required

Apply this rule to all new or changed player-facing mod text, including HUD labels,
voting, shopping lists, Black Market, results, modifier descriptions and notifications.

- Use the supplied [in-game text reference](art/references/hud-text-style.png) as the
  visual standard. Match the font, weight, apparent on-screen size and soft glow/blur
  together; using the same font family alone does not satisfy this rule.
- Reuse the game's native TextMeshPro font assets and appropriate material presets.
  Teko is the intended font family; prefer the actual game asset over a downloaded
  substitute. Match the reference's heavier headings and lighter body/value text.
  Do not substitute Unity GUI, Arial or another system font in player-facing UI.
- Match native text size and spacing at the same screen resolution and UI scale.
  Keep headings, values and supporting text proportionate. Check readability and
  placement at different aspect ratios without stretching glyphs or overlapping HUD.
- Preserve the reference's restrained soft glow, edge softness and native chromatic
  appearance. Do not disable glow globally or use a sharp, heavy outline as a substitute.
  A separate overlay canvas does not automatically inherit native HUD post-processing;
  explicitly account for that when choosing the rendering path and material.
- Keep text interiors crisp enough to read. Match softness visually rather than
  applying a uniform blur to every label. Use the native colour hierarchy and preserve
  meaningful warning/status colours and localized text.
- Keep HUD labels, lists and notifications free of text backplates unless the user
  explicitly requests one or the element is part of an established full-screen menu.
- Validate changes with an in-game comparison against the reference at a comparable
  resolution. Engine fixtures can check layout and settings, but do not certify the
  final glow/blur appearance. Report any remaining mismatch you know of.

This is the required target for future work, not a claim that all existing UI already
matches it. Do not distribute extracted proprietary game font assets in the mod package.

## HUD toolkit

Build new HUD on the shared toolkit in [src/Hud](src/Hud/README.md): vitals rows, corner stacks, list panels,
paging and notifications. Extend the toolkit rather than adding another one-off panel, so HUD elements
stack instead of overlapping and keep the native text style.

## Reports

- Do not say that changes are untested or were not checked in game ("не протестировано",
  "в игре не проверялось" and the like). The user tests in game themselves.
- Report what was actually done and run: the build result, which tests passed, what changed.
  Concrete things worth looking at in game may be listed as such, without a "not tested" disclaimer.
