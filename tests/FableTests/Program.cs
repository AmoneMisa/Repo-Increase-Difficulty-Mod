using System.Text.Json;
using IncreaseHardMod.FableRealm;

// Fable Realm checks: the bean puzzle state machine, its network message and the shipped room contract.
// Run from the repository root:  dotnet run --project tests/FableTests/FableTests.csproj -c Release
int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); checks++; Console.WriteLine("PASS: " + name); }

const int Red = 0, Yellow = 1, Blue = 2, Green = 3, Violet = 4;
int Bit(int colour) => 1 << colour;
int[] Plates(params (int plate, int colours)[] set)
{
    var resting = new int[BeanPuzzleRules.Count];
    foreach (var (plate, colours) in set) resting[plate] |= colours;
    return resting;
}
int[] AllMatched() => Plates((Red, Bit(Red)), (Yellow, Bit(Yellow)), (Blue, Bit(Blue)), (Green, Bit(Green)), (Violet, Bit(Violet)));

// ---- matching
{
    var puzzle = new BeanPuzzleState();
    Check(!puzzle.Observe(Plates(), 1) && puzzle.Matched == 0 && !puzzle.Solved, "Empty plates match nothing");
    Check(puzzle.Observe(Plates((Red, Bit(Red))), 2) && puzzle.Matched == Bit(Red), "Red bean on the red plate lights that plate");
    Check(!puzzle.Observe(Plates((Red, Bit(Red))), 3), "An unchanged scan reports no change");
    Check(puzzle.Observe(Plates((Red, Bit(Red)), (Blue, Bit(Blue))), 4) && puzzle.Matched == (Bit(Red) | Bit(Blue)), "A second matching bean adds its plate");
    Check(puzzle.Observe(Plates((Blue, Bit(Blue))), 5) && puzzle.Matched == Bit(Blue), "Taking a bean away before the end darkens its plate");
    Check(puzzle.Observe(AllMatched(), 6) && puzzle.Solved && puzzle.Matched == BeanPuzzleRules.AllMask && puzzle.SolvedAt == 6, "All five matching beans solve the puzzle");
    Check(!puzzle.Observe(Plates(), 7) && puzzle.Solved && puzzle.Matched == BeanPuzzleRules.AllMask, "A solved puzzle stays solved when the beans are taken away");
}

// ---- wrong colour
{
    var puzzle = new BeanPuzzleState();
    Check(!puzzle.Observe(Plates((Red, Bit(Yellow))), 1) && puzzle.Matched == 0, "A yellow bean on the red plate does not count");
    Check(puzzle.Observe(Plates((Red, Bit(Yellow) | Bit(Red))), 2) && puzzle.Matched == Bit(Red), "A wrong bean lying beside the right one does not spoil the plate");
    var swapped = Plates((Red, Bit(Yellow)), (Yellow, Bit(Red)), (Blue, Bit(Blue)), (Green, Bit(Green)), (Violet, Bit(Violet)));
    Check(puzzle.Observe(swapped, 3) && !puzzle.Solved && puzzle.Matched == (Bit(Blue) | Bit(Green) | Bit(Violet)), "Two swapped beans leave the puzzle unsolved");
    Check(!puzzle.RewardDue(1000) && !puzzle.SettleReward(true) && puzzle.Reward == BeanReward.Pending, "No reward while a bean is on the wrong plate");
    Check(puzzle.Observe(AllMatched(), 4) && puzzle.Solved, "Wrong placements never fail the puzzle: fixing them solves it");
    var allOnOne = new BeanPuzzleState();
    Check(allOnOne.Observe(Plates((Green, BeanPuzzleRules.AllMask)), 1) && allOnOne.Matched == Bit(Green), "Five beans on one plate match only that plate's colour");
    Check(!allOnOne.Solved, "Five beans on one plate do not solve the puzzle");
    Check(!new BeanPuzzleState().Observe(null, 1), "A missing scan is ignored");
    Check(new BeanPuzzleState().Observe(new[] { Bit(Red), Bit(Yellow) }, 1), "A short scan is read without error");
}

// ---- single reward
{
    var ledger = new BeanRewardLedger();
    string key = BeanRewardLedger.Key("REPO_SAVE_2026_11_16", 4);
    var puzzle = new BeanPuzzleState();
    puzzle.Observe(AllMatched(), 100);
    Check(!puzzle.RewardDue(100 + BeanPuzzleRules.LowerSeconds - .1), "No egg while the beanstalk is still growing");
    Check(puzzle.RewardDue(100 + BeanPuzzleRules.LowerSeconds), "The egg is due once the bean has landed");
    Check(puzzle.SettleReward(!ledger.Claimed(key)) && ledger.Claim(key) && puzzle.Reward == BeanReward.Given, "The first landing gives the egg");
    Check(!puzzle.RewardDue(1000) && !puzzle.SettleReward(true) && puzzle.Reward == BeanReward.Given, "The same puzzle never gives a second egg");
    Check(!puzzle.Observe(AllMatched(), 200) && !puzzle.RewardDue(1000), "Repeated scans after the reward change nothing");
    Check(!ledger.Claim(key), "The ledger refuses a second claim for the same level");

    // Scene change or reloaded save: a fresh puzzle on the same level of the same save.
    var reloaded = new BeanRewardLedger();
    reloaded.Load(ledger.Lines());
    var again = new BeanPuzzleState();
    again.Observe(AllMatched(), 50);
    Check(reloaded.Claimed(key) && !again.SettleReward(!reloaded.Claimed(key)) && again.Reward == BeanReward.AlreadyClaimed, "After a reload the beanstalk grows with an empty crown");
    Check(!again.SettleReward(true) && again.Reward == BeanReward.AlreadyClaimed, "An empty landing is final too");
    Check(!reloaded.Claimed(BeanRewardLedger.Key("REPO_SAVE_2026_11_16", 5)) && !reloaded.Claimed(BeanRewardLedger.Key("OTHER_SAVE", 4)), "Another level or another save still has its egg");
    Check(BeanRewardLedger.Key("a|b\nc", 2) == "a_b c|2" && BeanRewardLedger.Key(null, 0) == "unsaved|0", "Ledger keys stay on one line");
    var unsolved = new BeanPuzzleState();
    Check(!unsolved.SettleReward(true) && unsolved.Reward == BeanReward.Pending, "An unsolved puzzle cannot be paid");
}

// ---- clients and late joiners
{
    var host = new BeanPuzzleState();
    var guest = new BeanPuzzleState();
    host.Observe(Plates((Red, Bit(Red)), (Violet, Bit(Violet))), 1);
    Check(guest.Apply(host.Snapshot) && guest.Matched == host.Matched, "A guest shows the host's plates");
    Check(!guest.Apply(host.Snapshot), "A repeated report changes nothing");
    host.Observe(Plates((Violet, Bit(Violet))), 2);
    Check(guest.Apply(host.Snapshot) && guest.Matched == Bit(Violet), "A guest follows a bean being taken away");
    host.Observe(AllMatched(), 30);
    Check(guest.Apply(host.Snapshot) && guest.Solved && guest.SolvedAt == 30 && guest.Reward == BeanReward.Pending, "A guest grows the beanstalk from the host's solve time");
    var stale = new BeanSnapshot(Bit(Red), false, BeanReward.Pending, 0);
    Check(!guest.Apply(stale) && guest.Solved && guest.Matched == BeanPuzzleRules.AllMask, "A late unsolved report cannot undo a solved puzzle");
    host.SettleReward(true);
    Check(guest.Apply(host.Snapshot) && guest.Reward == BeanReward.Given, "A guest learns the egg was given");
    Check(!guest.Apply(host.Snapshot) && !guest.Apply(new BeanSnapshot(BeanPuzzleRules.AllMask, true, BeanReward.AlreadyClaimed, 30)) && guest.Reward == BeanReward.Given, "A decided reward is never changed by later reports");
    var late = new BeanPuzzleState();
    Check(late.Apply(host.Snapshot) && late.Solved && late.Reward == BeanReward.Given && late.SolvedAt == 30, "A late joiner gets plates, landing and egg in one report");
    Check(!late.RewardDue(1000) && !late.SettleReward(true), "A late joiner promoted to host does not spawn another egg");
    var promoted = new BeanPuzzleState();
    promoted.Apply(new BeanSnapshot(BeanPuzzleRules.AllMask, true, BeanReward.Pending, 30));
    Check(promoted.RewardDue(30 + BeanPuzzleRules.LowerSeconds) && promoted.SettleReward(true) && !promoted.SettleReward(true), "A guest promoted mid-landing gives the egg exactly once");
}

// ---- network message
{
    var ids = new[] { 11, 12, 13, 14, 15 };
    var floors = new float[15];
    var solved = new BeanSnapshot(BeanPuzzleRules.AllMask, true, BeanReward.Given, 42.5);
    object raw = BeanPuzzleWire.Write("3:Level - Fable Realm", solved, ids, floors);
    Check(BeanPuzzleWire.TryRead(raw, "3:Level - Fable Realm", out var read, out var readIds, out var readFloors) &&
        read.Matched == solved.Matched && read.Solved && read.Reward == BeanReward.Given && read.SolvedAt == 42.5 && readIds == ids && readFloors == floors, "A host report survives the round trip");
    Check(!BeanPuzzleWire.TryRead(raw, "4:Level - Fable Realm", out _, out _, out _), "A report for another level index is refused");
    Check(!BeanPuzzleWire.TryRead(raw, "3:Level - Manor", out _, out _, out _), "A report for another level is refused");
    Check(!BeanPuzzleWire.TryRead(raw, "", out _, out _, out _), "No report is accepted without a current level");
    foreach (object bad in new object[] { null, 7, "text", Array.Empty<object>(), new object[] { "3:Level - Fable Realm" }, new object[7] })
        Check(!BeanPuzzleWire.TryRead(bad, "3:Level - Fable Realm", out _, out _, out _), "Malformed report refused: " + (bad?.GetType().Name ?? "null"));
    object[] Forged(Action<object[]> change) { var d = BeanPuzzleWire.Write("k", new BeanSnapshot(Bit(Red), false, BeanReward.Pending, 0), ids, floors); change(d); return d; }
    Check(BeanPuzzleWire.TryRead(Forged(_ => { }), "k", out _, out _, out _), "An unsolved report is accepted");
    Check(!BeanPuzzleWire.TryRead(Forged(d => d[1] = (byte)64), "k", out _, out _, out _), "Unknown plate bits are refused");
    Check(!BeanPuzzleWire.TryRead(Forged(d => d[2] = true), "k", out _, out _, out _), "Solved without all five plates is refused");
    Check(!BeanPuzzleWire.TryRead(Forged(d => d[1] = (byte)BeanPuzzleRules.AllMask), "k", out _, out _, out _), "All five plates without solved is refused");
    Check(!BeanPuzzleWire.TryRead(Forged(d => d[3] = (byte)BeanReward.Given), "k", out _, out _, out _), "A reward on an unsolved puzzle is refused");
    Check(!BeanPuzzleWire.TryRead(Forged(d => d[3] = (byte)9), "k", out _, out _, out _), "An unknown reward value is refused");
    Check(!BeanPuzzleWire.TryRead(Forged(d => d[4] = double.NaN), "k", out _, out _, out _), "A non-finite solve time is refused");
    Check(!BeanPuzzleWire.TryRead(Forged(d => d[5] = new[] { 1, 2, 3 }), "k", out _, out _, out _), "A wrong number of beans is refused");
    Check(!BeanPuzzleWire.TryRead(Forged(d => d[5] = new[] { 1, 2, -3, 4, 5 }), "k", out _, out _, out _), "A negative view id is refused");
    Check(!BeanPuzzleWire.TryRead(Forged(d => d[6] = new float[] { float.PositiveInfinity, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }), "k", out _, out _, out _), "A non-finite bean position is refused");
}

// ---- lowering
{
    Check(BeanPuzzleRules.Drop(-1) == 0 && BeanPuzzleRules.Drop(0) == 0 && BeanPuzzleRules.Drop(double.NaN) == 0, "The beanstalk stays in the ground before the puzzle is solved");
    Check(Math.Abs(BeanPuzzleRules.Drop(BeanPuzzleRules.LowerSeconds) - BeanPuzzleRules.LowerDistance) < 1e-4 && Math.Abs(BeanPuzzleRules.Drop(9999) - BeanPuzzleRules.LowerDistance) < 1e-4,
        "The beanstalk rises exactly its sink depth");
    float last = 0; bool rising = true;
    for (double t = 0; t <= BeanPuzzleRules.LowerSeconds; t += .1) { float d = BeanPuzzleRules.Drop(t); rising &= d >= last; last = d; }
    Check(rising, "The beanstalk never sinks back while it grows");
    Check(BeanPuzzleRules.ColourOf(BeanPuzzleRules.BeanKey(Green)) == Green && BeanPuzzleRules.ColourOf("fable_bean_pink") < 0 &&
        BeanPuzzleRules.ColourOf(BeanPuzzleRules.EggKey) < 0 && BeanPuzzleRules.ColourOf(null) < 0, "Only the five bean valuables count as beans");
}

// ---- the shipped room contract
{
    string path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "FableRealm", "fable_rooms.json");
    var file = JsonSerializer.Deserialize<FableRoomFile>(File.ReadAllText(path), new JsonSerializerOptions { IncludeFields = true });
    Check(file?.rooms != null && file.rooms.Length == 54, "The contract lists the 32 rooms and 22 second layouts");
    var plan = FableLevelPlan.From(file.rooms, out string error);
    Check(plan != null && error == null, "The contract makes a complete level: " + error);
    Check(plan.Special.Count == 1 && plan.Special[0].id == "BeanstalkGrove" && plan.Special[0].special == BeanPuzzleRules.Special, "The bean puzzle room is the one special room");
    Check(plan.SpecialAmount == 1, "The generator is asked for one special tile");
    bool listedByRole = plan.Start.Contains(plan.Special[0]);
    foreach (var tiers in new[] { plan.Normal, plan.Passage, plan.DeadEnd, plan.Extraction })
        foreach (var tier in tiers) listedByRole |= tier.Contains(plan.Special[0]);
    Check(!listedByRole, "The special room is in no ordinary module list, so it cannot be drawn a second time");
    Check(FableLevelPlan.PuzzleAnchorsComplete(plan.Special[0], out string missing), "The grove has its five sockets, landing and egg markers: " + missing);
    int count = plan.Start.Count + plan.Special.Count;
    foreach (var tiers in new[] { plan.Normal, plan.Passage, plan.DeadEnd, plan.Extraction }) foreach (var tier in tiers) count += tier.Count;
    Check(count == 54 && plan.Start.Count == 3 && plan.Normal[0].Count > 0, "Every room lands in exactly one list");
    // Second layouts: one for every normal, passage and dead-end room except the special one, in the same role and tier.
    int layouts = 0;
    foreach (var room in file.rooms)
    {
        string baseId = FableLayouts.BaseId(room.id);
        if (baseId == room.id) continue;
        layouts++;
        var original = Array.Find(file.rooms, r => r.id == baseId);
        Check(original != null && original.role == room.role && original.difficulty == room.difficulty && string.IsNullOrEmpty(room.special),
            room.id + " is a second layout of " + baseId + " in the same role and tier");
    }
    Check(layouts == 22 && !Array.Exists(file.rooms, r => r.id == "BeanstalkGrove" + FableLayouts.Suffix), "22 second layouts, none of the special room");
    Check(FableLayouts.BaseId("RootHall-B") == "RootHall" && FableLayouts.BaseId("RootHall") == "RootHall" && FableLayouts.BaseId("-B") == "-B" &&
        FableLayouts.BaseId(null) == null, "A second layout is looked up under its base room");
    // Pits: every pit has a death zone, wholly below the room floor, so the rim and bridges are safe.
    foreach (var room in file.rooms)
    {
        int pits = 0, zones = 0; bool below = true;
        foreach (var anchor in room.anchors)
        {
            if (anchor.kind == "pit") pits++;
            if (anchor.kind != "kill") continue;
            zones++;
            below &= anchor.s != null && anchor.s.Length == 3 && anchor.s[1] >= 1.5f && anchor.p[1] + anchor.s[1] / 2 <= -1.2f && anchor.s[0] >= 1.5f && anchor.s[2] >= 1.5f;
        }
        if (pits > 0) Check(zones == pits && below, "Room " + room.id + " has a death zone under the floor for each pit");
    }
    // Beanstalk: the egg appears on the crown, above the last leaf and below the height the stalk rises by.
    var crown = FableLevelPlan.Anchor(plan.Special[0], BeanPuzzleRules.EggSpawn);
    // Height: as tall as Swiftbroom Academy's Great Tree (about 30 m to its crown). The egg waits up there, in the castle
    // gate on the cloud bank, which does not move; every beanstalk piece rises out of the floor and is hidden beneath it.
    Check(crown != null && crown.p[1] >= 25f && crown.p[1] <= 32f, "The Golden Egg appears at the top, as high as the Swiftbroom tree");
    float highest = float.MinValue;
    foreach (var anchor in plan.Special[0].anchors) if (anchor.kind == "volume" && anchor.id == "room") highest = anchor.p[1] + anchor.s[1] / 2;
    Check(highest > crown.p[1] + 3f, "The room volume reaches above the castle, so the egg is still in the room");
    Check(BeanPuzzleRules.LowerDistance > crown.p[1] && BeanPuzzleRules.LowerSeconds >= 8f, "The whole beanstalk sinks out of sight and grows visibly");
    // The two open rooms carry the large valuables.
    foreach (string id in new[] { "GiantPantry", "TrophyHall" })
    {
        var hall = Array.Find(file.rooms, r => r.id == id);
        int Count(string type) { int n = 0; if (hall != null) foreach (var a in hall.anchors) if (a.kind == "loot" && a.tag == type) n++; return n; }
        Check(hall != null && hall.role == "Normal" && Count("Big") >= 4 && Count("Wide") >= 2 && Count("VeryTall") >= 2 && Count("Tall") >= 2,
            id + " has room for big, wide, tall and very tall loot");
    }
    foreach (var room in plan.Start)
        Check(FableLevelPlan.Anchor(room, "truck") != null && FableLevelPlan.Anchor(room, "extraction") != null, "Start room " + room.id + " has truck and extraction anchors");
    foreach (var tier in plan.Extraction) foreach (var room in tier)
        Check(FableLevelPlan.Anchor(room, "extraction") != null, "Extraction room " + room.id + " has an extraction anchor");

    FableRoom Room(string id, string role, string special = "") => new() { id = id, role = role, special = special, difficulty = 1, anchors = Array.Empty<FableAnchor>() };
    var two = FableLevelPlan.From(new[] { Room("s", "Start"), Room("n", "Normal"), Room("p", "Passage"), Room("d", "DeadEnd"), Room("e", "Extraction"),
        Room("g1", "DeadEnd", BeanPuzzleRules.Special), Room("g2", "DeadEnd", BeanPuzzleRules.Special) }, out _);
    Check(two != null && two.Special.Count == 2 && two.SpecialAmount == 1 && two.DeadEnd[0].Count == 1, "Even with two special rooms the level asks for one special tile");
    Check(FableLevelPlan.From(new[] { Room("s", "Start"), Room("n", "Normal"), Room("p", "Passage"), Room("d", "DeadEnd"), Room("e", "Extraction") }, out _).SpecialAmount == 0, "A level without a special room asks for none");
    Check(FableLevelPlan.From(new[] { Room("s", "Start"), Room("s", "Normal") }, out error) == null && error != null, "Duplicate room ids are rejected");
    Check(FableLevelPlan.From(new[] { Room("n", "Normal"), Room("p", "Passage"), Room("d", "DeadEnd"), Room("e", "Extraction") }, out error) == null, "A level without a start room is rejected");
    Check(FableLevelPlan.From(new[] { Room("s", "Start"), Room("x", "Cellar") }, out error) == null, "An unknown role is rejected");
    Check(!FableLevelPlan.PuzzleAnchorsComplete(Room("g", "DeadEnd", BeanPuzzleRules.Special), out missing) && missing == "bean-socket-red", "A grove without markers is reported");
}

// ---- exits: grid sides, plugs and which room hangs the door
{
    Check(FableExitRules.ExtraLoot(40) == 20 && FableExitRules.ExtraLoot(7) == 4 && FableExitRules.ExtraLoot(0) == 0,
          "Fable Realm adds half the game's valuable count again");
    Check(!FableExitRules.SpawnsLoot("Extraction") && FableExitRules.SpawnsLoot("Normal") && FableExitRules.SpawnsLoot("DeadEnd") &&
          FableExitRules.SpawnsLoot("Passage"), "No loot spawns in extraction rooms");
    Check(FableExitRules.BothWays(0f, 100f) == (-100f, 100f) && FableExitRules.BothWays(-90f, 0f) == (-90f, 90f) &&
          FableExitRules.BothWays(-30f, 110f) == (-110f, 110f), "Gates swing both ways as far as the donor door opens");
    Check(FableExitRules.LocalSide(0f, 7.5f) == FableExitRules.Top && FableExitRules.LocalSide(7.5f, 0f) == FableExitRules.Right &&
          FableExitRules.LocalSide(0f, -7.5f) == FableExitRules.Bottom && FableExitRules.LocalSide(-7.5f, -0f) == FableExitRules.Left, "Exits face the grid side they stand on");
    Check(FableExitRules.WorldSide(FableExitRules.Top, 90f) == FableExitRules.Right && FableExitRules.WorldSide(FableExitRules.Top, 180f) == FableExitRules.Bottom &&
          FableExitRules.WorldSide(FableExitRules.Top, -90f) == FableExitRules.Left, "A turned room turns its exits with it");
    Check(FableExitRules.WorldSide(FableExitRules.Left, 270.00002f) == FableExitRules.Bottom && FableExitRules.WorldSide(FableExitRules.Right, 89.99998f) == FableExitRules.Bottom &&
          FableExitRules.WorldSide(FableExitRules.Top, 359.99997f) == FableExitRules.Top, "A yaw read back a hair off a quarter turn still finds the right side");
    Check(FableExitRules.Connected(true, FableExitRules.Right, false, true, false, false) && !FableExitRules.Connected(true, FableExitRules.Left, true, true, true, false),
        "A normal room opens exactly the sides the generator joined");
    Check(FableExitRules.Connected(false, FableExitRules.Left, false, false, false, false), "Passages, dead ends and start rooms keep their exits open");
    int doors = 0;
    for (int side = 0; side < 4; side++) if (FableExitRules.HangsDoor(side)) doors++;
    Check(doors == 2 && FableExitRules.HangsDoor(FableExitRules.Top) != FableExitRules.HangsDoor(FableExitRules.Bottom) &&
          FableExitRules.HangsDoor(FableExitRules.Right) != FableExitRules.HangsDoor(FableExitRules.Left), "Each shared edge gets one door: from the room below or to the left");
}

// ---- handheld map: tiles in map units
{
    bool Near(float a, float b) => Math.Abs(a - b) < 1e-4f;
    // Measured in game: the 1x1 floor tile is 0.5 x 0.5 map units with its pivot on a corner; Map.Scale is 0.1.
    Check(Near(FableMapMath.TileScale(5f, .1f, .5f), 1f), "A 5 m floor gets the vanilla unit marker");
    Check(Near(FableMapMath.TileScale(15f, .1f, .5f), 3f) && Near(FableMapMath.TileScale(15f, .1f, .5f) * .5f, 15f * .1f), "A 15 m room is drawn 1.5 map units wide, as its position spacing");
    Check(Near(FableMapMath.PivotOffset(15f, .25f, .5f), 7.5f) && Near(FableMapMath.PivotOffset(4f, 0f, .4f), 0f), "A corner-pivot tile is placed half the box before its centre");
    Check(FableMapMath.IsWall("wall-north-1") && FableMapMath.IsWall("front-wall-1") && !FableMapMath.IsWall("pit-0-wall-x0") && !FableMapMath.IsWall("lintel-north"),
        "Shell walls are drawn, pit linings and lintels are not");
    Check(FableMapMath.IsWallBox(8f, 4f) && !FableMapMath.IsWallBox(2.6f, -1.7f) && !FableMapMath.IsWallBox(.4f, .2f), "Only full-height walls on the floor become map walls");
}

// ---- monster paths: stray unreachable level points are removed, a wholesale failure is not
{
    Check(FableMonsterPathRules.ShouldPrune(300, 3) && FableMonsterPathRules.ShouldPrune(300, 75), "A few unreachable level points are removed");
    Check(!FableMonsterPathRules.ShouldPrune(300, 76) && !FableMonsterPathRules.ShouldPrune(300, 300), "A level where most points fail keeps them: the check itself is wrong then");
    Check(!FableMonsterPathRules.ShouldPrune(300, 0) && !FableMonsterPathRules.ShouldPrune(0, 0), "Nothing to remove when every point is fine");
}

Console.WriteLine("FABLE_TESTS_PASS: " + checks);
