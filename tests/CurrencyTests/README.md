# Currency regression checks

Run `dotnet run --project tests/CurrencyTests -c Release` with .NET 10.
Compiles the production QuestMoney and CurrencyPayout source against a controlled native-wallet adapter.
Covers dollar/K conversion, rounding, host authority, extraction-only economic multipliers,
reward scope restoration after exceptions, HUD refresh and integer overflow.
Native engine/Harmony execution and complete quest progression still require in-game verification.
