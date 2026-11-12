# Living Portrait revision

Generated a new original canvas using the built-in image generation tool; integrated as `src/Art/ritual_living_portrait.png` (1086 × 1448). Model and frame generated with `tools/build_ritual_rewards.py -- ritual_reward_painting`. Preview is a Blender render of that exported geometry and texture, not an in-game screenshot.

## Generation prompt

Create a finished oil-painting texture for a haunted museum game collectible called Living Portrait. Portrait 3:4 aspect ratio, artwork fills every pixel edge-to-edge. Only the painted canvas, NO frame, NO wall, NO scene mockup, NO typography. Elegant elderly aristocratic woman, waist-up, dark burgundy and black Victorian dress with a small antique gold pendant, restrained silver hair pinned up. Her face has believable adult proportions, long narrow features, slightly weary expression and a subtle unsettling knowing smile. Both eyes clearly visible looking straight at the viewer, natural small dark pupils, NO glowing eyes. Composition face centered at x50%, eyes about y30% from top; hands clasped near bottom. Painterly stylized dark fantasy, a convincing old master oil portrait, broad confident brushwork and rich glazing, subdued olive umber background, warm ivory face, muted burgundy accents, readable silhouette. Age cracks and canvas grain subtle rather than covering features. Strong readable face even viewed at thumbnail size in a dark low-poly horror game. Hand-painted 2D art, NOT a 3D render, NOT a plastic doll, NOT a cartoon round baby head, NOT a photograph. Soft light from upper left, dark atmospheric shadows with visible detail. No frame or borders whatsoever.

## Runtime

One canvas mesh with a dense UV grid around the two eyes. Only a per-spawn mesh copy is animated; UV falloff is zero outside the eyes. Maximum gaze displacement is 0.004 UV at 12.5 updates per second. No animation of rigid body or collider. The other reward models and ritual economy are unchanged. Live-game material, lighting and animation validation is still pending.
