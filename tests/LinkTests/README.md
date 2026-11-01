# Link Drone set regression checks

Run `dotnet run --project tests/LinkTests -c Release` with .NET 10.
Compiles production LinkSets against controlled owned-item, run-stat and purchase adapters.
Covers two delivered items per purchase, fixed partner identities, save reloads, legacy migration,
missing halves, reused native instance suffixes, cached prompt labels and host authority.
The native shop adapter deliberately defers owned records until truck ItemAdd; saved
reservations are tested with reversed spawn order and multiple pending kits. Expected: 21 checks.
Physical joints and projected HUD geometry are checked separately in a local Unity fixture.
The stubbed native purchase adapter does not certify real multiplayer truck delivery.
