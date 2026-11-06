using IncreaseHardMod.Hud;

// Engine-free checks of the HUD toolkit's layout, paging and notification queue (src/Hud/HudLayout.cs).
int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
bool Near(float a, float b) => Math.Abs(a - b) < 0.001f;

// ---------------------------------------------------------------- stack layout
var stack = new HudStackLayout(100f, 12f);
Check(Near(stack.OffsetFor("a", 10, 1), 100f), "First block starts at the corner start");
stack.Claim("a", 10, 200f, 1, 700f);
Check(Near(stack.OffsetFor("b", 20, 1), 312f), "Higher order goes below the live block plus the gap");
Check(Near(stack.OffsetFor("a", 10, 1), 100f), "A block never counts itself");
stack.Claim("b", 20, 50f, 1, 600f);
Check(Near(stack.OffsetFor("c", 5, 1), 100f), "Lower order goes first, above existing blocks");
Check(Near(stack.OffsetFor("c", 20, 1), 374f), "Equal order is placed after ids that sort first");
Check(Near(stack.LowestScreenEdge(1), 600f), "Lowest screen edge is the lowest live block");
Check(stack.IsLive("a", 3) && !stack.IsLive("a", 4), "A claim stays live for two frames without renewal");
stack.Claim("b", 20, 50f, 4, 600f);
Check(Near(stack.OffsetFor("b", 20, 4), 100f), "Stack closes up when a block stops renewing its claim");
stack.Release("b");
Check(Near(stack.LowestScreenEdge(4), -1f), "Empty stack reports no edge");
stack.Claim("x", 1, -5f, 5);
Check(Near(stack.OffsetFor("y", 2, 5), 112f), "Negative heights are treated as zero");

// ---------------------------------------------------------------- paging
float Measure(string text) => text.Split('\n').Length - 1; // one unit per line
var entries = new[] { "a\n", "b\n", "c\n", "d\n", "e\n" };
var pages = HudPaging.Paginate(entries, Measure, 3f, "H\n", "");
Check(pages.Count == 3 && pages[0] == "a\nb\n" && pages[2] == "e\n", "Entries fill pages up to the height, header included");
pages = HudPaging.Paginate(new[] { "huge\n\n\n\n\n" }, Measure, 2f);
Check(pages.Count == 1 && pages[0].StartsWith("huge"), "An entry taller than a page still gets its own page");
pages = HudPaging.Paginate(Array.Empty<string>(), Measure, 2f);
Check(pages.Count == 1 && pages[0] == "", "No entries still gives one empty page");
Check(HudPaging.Step(0, 3, -1) == 2 && HudPaging.Step(2, 3, 1) == 0 && HudPaging.Step(5, 1, 1) == 0, "Page steps wrap around");

// ---------------------------------------------------------------- notifications
var queue = new HudMessageQueue<string>();
queue.Show("k", "one", "blue", 3f, 0f);
queue.Show("k", "two", "red", 1f, 1f);
var visible = queue.Visible(1f);
Check(visible.Count == 1 && visible[0].Text == "two" && visible[0].Color == "red", "Same key replaces the text and colour");
Check(Near(visible[0].Until, 3f), "Replacing never shortens a message");
queue.Show(null, "a", "", 5f, 1f);
queue.Show(null, "b", "", 5f, 1f);
queue.Show(null, "c", "", 5f, 1f);
visible = queue.Visible(1f);
Check(visible.Count == 3 && visible[0].Text == "c", "At most three show, newest first");
Check(queue.Visible(3.5f).All(m => m.Text != "two"), "Expired messages are removed");
queue.Clear("k");
queue.ClearAll();
Check(queue.Visible(4f).Count == 0, "ClearAll empties the channel");

queue.Show("s", "status", "", 3f, 10f);
var message = queue.Visible(10f)[0];
Check(Near(HudMessageQueue<string>.Alpha(message, 10f), 0f) && Near(HudMessageQueue<string>.Alpha(message, 10.15f), 1f),
    "Messages fade in over 0.15 s");
queue.Clear("s"); queue.Show("s", "status", "", .5f, 11f);
for (float t = 11.1f; t < 12f; t += .1f) queue.Show("s", "status", "", .5f, t);
message = queue.Visible(12f)[0];
Check(Near(HudMessageQueue<string>.Alpha(message, 12f), 1f), "A status refreshed every frame stays fully visible");
Check(queue.Visible(12.5f).Count == 0, "A status disappears about half a second after the last refresh");
queue.Show("s", "again", "", .5f, 20f);
Check(Near(queue.Visible(20f)[0].Shown, 20f), "An expired key comes back with a fresh fade-in");

Console.WriteLine("HUD_TOOLKIT_PASS: " + checks);
