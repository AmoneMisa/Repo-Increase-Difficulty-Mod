# Network regression checks

Run from the repository root with the .NET 10 SDK:

```sh
dotnet run --project tests/NetworkTests/NetworkTests.csproj -c Release
```

Expected result: `NETWORK_AUDIT_PASS: 61`.

The project links the production `ModNetwork.cs` and `NetworkPayload.cs` directly.
`Stubs.cs` supplies controlled Unity, Photon and gameplay APIs; it does not bundle
proprietary game assemblies or start a game session. Tests cover malformed packets,
non-finite values, host authority, ballot identity, repair limits, shield battery
category/coverage, and disconnected callbacks.

Passing these checks does not certify packet timing, host migration, real remote
ownership, multiplayer physics, anti-cheat protection or Proton compatibility.
