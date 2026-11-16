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
    Check(!puzzle.RewardDue(100 + BeanPuzzleRules.LowerSeconds - .1), "No egg while the giant bean is still coming down");
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
    Check(reloaded.Claimed(key) && !again.SettleReward(!reloaded.Claimed(key)) && again.Reward == BeanReward.AlreadyClaimed, "After a reload the bean comes down empty");
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
    Check(guest.Apply(host.Snapshot) && guest.Solved && guest.SolvedAt == 30 && guest.Reward == BeanReward.Pending, "A guest lowers the bean from the host's solve time");
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
    Check(BeanPuzzleRules.Drop(-1) == 0 && BeanPuzzleRules.Drop(0) == 0 && BeanPuzzleRules.Drop(double.NaN) == 0, "The bean hangs still before the puzzle is solved");
    Check(Math.Abs(BeanPuzzleRules.Drop(BeanPuzzleRules.LowerSeconds) - 5.2f) < 1e-4 && Math.Abs(BeanPuzzleRules.Drop(9999) - 5.2f) < 1e-4, "The bean comes down exactly 5.2 m");
    float last = 0; bool rising = true;
    for (double t = 0; t <= BeanPuzzleRules.LowerSeconds; t += .1) { float d = BeanPuzzleRules.Drop(t); rising &= d >= last; last = d; }
    Check(rising, "The bean never moves back up on its way down");
    Check(BeanPuzzleRules.ColourOf(BeanPuzzleRules.BeanKey(Green)) == Green && BeanPuzzleRules.ColourOf("fable_bean_pink") < 0 &&
        BeanPuzzleRules.ColourOf(BeanPuzzleRules.EggKey) < 0 && BeanPuzzleRules.ColourOf(null) < 0, "Only the five bean valuables count as beans");
}

// ---- the shipped room contract
{
    string path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "FableRealm", "fable_rooms.json");
    var file = JsonSerializer.Deserialize<FableRoomFile>(File.ReadAllText(path), new JsonSerializerOptions { IncludeFields = true });
    Check(file?.rooms != null && file.rooms.Length == 30, "The contract lists the 30 rooms");
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
    Check(count == 30 && plan.Start.Count == 3 && plan.Normal[0].Count > 0, "Every room lands in exactly one list");
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

Console.WriteLine("FABLE_TESTS_PASS: " + checks);
