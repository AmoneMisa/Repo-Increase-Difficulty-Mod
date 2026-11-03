# Unity clone and wheeled-prop regression fixture

Run `tools/test-valuable-clones.ps1` with a licensed local Unity 6 editor.
The runner copies the current production donor-removal, binding-resolver, catalog
and rolling-physics sources into this isolated project before compilation.

Checks exercise real Unity `Object.Instantiate`, renderer flag reset, forced
renderer re-enabling, spawn/reclone reference recovery and template destruction.
Physics checks simulate 55 kg, 15 kg and 5 kg bodies on a floor with one holder's
rolling assistance, checking upright placement, movement, preserved mass, no lift,
and no assistance while airborne or ungrabbed.

The metadata loader/logger are fixture stubs. Tests do not load REPO, certify its
networking or reproduce every native collider/room. Engine output is recorded in
`clone-regression-result.txt`; game visual comparison and host/client testing remain
separate checks. No proprietary game assets are used.
