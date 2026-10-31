# Increase Hard Mod

**English** · [Русский](#-русский)

---

## 🇬🇧 English

A difficulty & gameplay mod for **R.E.P.O.** (BepInEx / HarmonyLib / REPOLib). It ramps the challenge
as a run goes deeper, sprinkles in random per-level "surprise" modifiers, adds custom shop upgrades,
a team ping system, a round-results board and a set of quality-of-life features for lobbies.

- **GUID:** `com.kubai.increasehardmod`
- **Version:** `0.4.0`

### Contents

- [Requirements](#requirements)
- [Features](#features)
- [Default keys](#default-keys)
- [Configuration](#configuration)
- [Building](#building)
- [Notes](#notes)

### Requirements

- [BepInEx 5](https://github.com/BepInEx/BepInEx) (5.4.21)
- [REPOLib](https://thunderstore.io/c/repo/p/Zehs/REPOLib/) — required for the custom shop upgrades.
  A client without REPOLib will not see this mod's custom upgrades in the shop.
- Optional: [REPOConfig](https://thunderstore.io/c/repo/p/REPOModding/REPOConfig/) (needs
  [MenuLib](https://thunderstore.io/c/repo/p/REPOModding/MenuLib/)) for editing settings in-game.
  Both now live under the **REPOModding** author (the old `nickklmao` packages are deprecated).
- Everyone in the lobby should run the mod to avoid desync (several features are host-authoritative).

### Features

#### Difficulty ramp

Difficulty scales with `RunManager.levelsCompleted`, so every client derives the same value from the
game's synced level count (host-authoritative, no extra networking). Configurable start level,
levels-per-step, per-step growth and caps for:

- **Enemy health** — with per-enemy cap overrides (Elsa and Loom are excluded by default).
- **Enemy damage** and **enemy speed**.
- **Enemy respawn time** (faster respawns deeper in the run, with a minimum factor).
- **Quota** growth, capped both by a multiplier and by a fraction of the available loot.

#### Surprise modifiers

Each eligible level can roll one or more random modifiers, broadcast by the host over Photon so the
whole lobby shares the same set. Every modifier can be toggled individually; the chance of a special
level is configurable.

| Group | Modifier | Effect |
|---|---|---|
| Loot | Fragile Loot | Loot breaks and loses value more easily |
| Loot | Heavy Loot | Loot weighs more |
| Loot | Light Loot | Loot weighs less |
| Loot | Protected Loot | Loot takes less damage |
| Loot | Dangerous Loot | Hazardous valuables hurt players more |
| Level | Extra Small / Big Cart | An extra cart spawns |
| Level | Extra Souls | Monsters drop more souls |
| Level | Bigger Soul Drops | Each soul drop is worth more |
| Level | Spread Upgrades | Upgrades are scattered around, usable only back in the truck |
| Level | More Gacha Cubes | More gacha/cube valuables spawn |
| Player | No Map | No map until you find a map upgrade pickup |
| Player | Reduced Player HP | Everyone starts wounded (rolled %) |
| Player | Auto Revive | One automatic revive per player (optionally at the truck, fully healed) |
| Player | Money Loss on Death | Lose a % of collected money when you die |
| Player | Level Timer | Timed level: kills add time, deaths remove it, quota adds it; at 0 the layout rerolls |
| Player | Change Voices | Player voices get swapped/randomized |
| Monsters | Souls Explode Harder | Exploding souls deal more damage |
| Monsters | Monsters Pit-Tanky | Monsters take less death-pit damage |
| Monsters | Light / Heavy Monsters | All monsters weigh less (easy to shove) or more (hard to shove) |

#### Level scaling and economy events

Each of these has its own switch in the `Modules` section and its numbers in the *Level Scaling* and
*Economy* sections. Everything is host-side; clients get the result through the game's normal sync.

| Module | What it does (defaults) |
| --- | --- |
| **Map value** | Valuables gain value with depth: x1.02 per level after level 2, capped at x3. |
| **Enemy count** | More monsters per level after level 8: +3% per level, capped at x2 (the extras come from the hardest tier already unlocked). |
| **Level size** | Bigger maps after level 8: +3% modules and grid per level, capped at x1.5. More modules also means more extraction points through the game's own thresholds. |
| **Extractions** | One extra extraction point every 10 levels, at most 5 per level. |
| **Moon phases** | Off by default. When on, the moon advances every 15 levels instead of every 5. |
| **Economy events** | From level 3 on, a level gets a random event with 50% chance: a multiplier on the value of loot, on the quota and/or on the cash an extraction pays. 20 events from *Gold Rush* (+35% loot value) and *Windfall* (+30% pay) to *Tax Audit* (-25% pay, +10% quota) and *Crushing Quota* (+40%). Bad events get likelier with depth, good ones rarer. The active event is shown under the level info HUD. |

#### Shop upgrades (via REPOLib)

All of these are sold in the normal shop. Prices are approximate (the game scales them with player
count) and, like every number below, are configurable.

**Personal upgrades** - each player buys and levels their own:

| Upgrade | Levels | What it does |
| --- | --- | --- |
| **Slower Stamina Use** | 5 | Sprinting drains 10% less stamina per level. ~4,000 |
| **HP Regeneration** | 3 | Heals 0.2% of max HP per second per level, but only until you are back to 30% of max HP - it is an emergency trickle, not a full heal. ~20,000 |
| **Death Protection** | 1 charge | Survive one lethal hit with 1 HP. The charge is spent when it saves you; re-buy to restore it. ~50,000 |
| **Drone Battery Life** | 20 | Your drones' batteries last 5% longer per level (up to +100%). ~6,000 |
| **Gun Battery Life** (one per gun type: bullet / laser / tranq) | 20 each | That gun's battery lasts 5% longer per level (up to +100%). ~6,000 |
| **Autoscan** | 5 | Automatically discovers valuables around you every second, 5 m further per level (5-25 m). ~8,000 |
| **Map Suite** | 10 | One upgrade, ten map features: L1 alive-player count, L2 dead-player count, L3 active monsters, L4 respawning monsters, L5 quota heading and valuable tracker (found/total, haul), L6 team pings on the map, L7-L8 zoom the map out (zoom keys `-` / `=`), L9 cosmetic-box tracker, L10 monsters as dots. Replaces the vanilla *Map Player Count* upgrade by default. ~15,000 for the first level |
| **Ping Info** | 1 | Your pings show the valuable's value, cosmetic rarity and exact name. ~6,000 |
| **Ping Anywhere** | 1 | Place location pings on any surface, visible through walls (limited live pings per player). ~6,000 |
| **Pin** | 1 | The permanent version of Ping Anywhere: buy once, keep it forever. ~30,000 |

**Truck upgrades** - bought once, they apply to the whole team:

| Upgrade | Levels | What it does |
| --- | --- | --- |
| **Heal Power** | 3 | All healing (truck final heal, health packs, healing drones) is 25% stronger per level. Starts at 10,000 |
| **Charge Efficiency** | 3 | The truck's charging station spends 20% less energy per level when it recharges items. Starts at 10,000. Stacks with the power-crystal economy: fewer crystals burned per recharge |
| **Loot Cash-In** | 1 | Once every quota is closed, leftover valuables are sold automatically for 75% of their value. One-time unlock, 50,000 |

#### Utility drones (shop)

Four extra drones. They are built on the game's own drones, so aiming, the beam, attaching,
the on/off switch, the opening animation and the battery work exactly like the originals. Every
player must run the mod, with the same module toggles.

| Drone | Price (value tier) | What it does |
| --- | --- | --- |
| **Ghost Drone** | 11,000-15,000 | Attach it to a valuable: players walk straight through that valuable and it is left out of extraction counting and suction while the drone is on. Weight, price and durability are unchanged. Anyone still inside it when the drone goes off stays pass-through until they step out. |
| **Silencer Drone** | 7,000-9,000 | Attach it to a valuable, item, cart, player or monster: the noise it makes (collisions, sounds, voice chat/TTS, and the radius monsters investigate it from) drops to 8% by default. It does not make the target deaf. |
| **Link Drone Set (A + B)** | 19,000-24,000 | One purchase gives both halves. Attach A to one object and B to another within 6 m: a physical cable ties them together (the cable is straight, colour-coded; faint amber while waiting, red under strain). Grab one end and the other is slowly reeled in, 0.75 m/s down to 1.25 m. It lets go if over-stretched for too long; switch a half off and on to relink. |
| **Repair Drone** | 30,000-36,000 | Attach it to a damaged valuable to restore its lost value, up to its original value, at up to 500 per second. A full battery repairs 25,000 - more than the 20,000 power crystal it takes to recharge. A valuable that was fully destroyed cannot be repaired. |

#### Extra gear (shop)

Orbs, guns and a grenade, again built on vanilla items (so models, handling and battery UI feel native)
with this mod's own models, animations and sounds.

| Item | Price (value tier) | What it does |
| --- | --- | --- |
| **Defence Orb** | 19,000-24,000 | Protects **loot**. While active, value that valuables would lose from breaking inside its field is paid from its battery instead (about 3,500 at full charge). Slowly self-discharges (default 20% per minute). |
| **Defence Drone** | 11,000-15,000 | Protects a **player**: attach it to a teammate (or yourself) and it absorbs up to 350 damage in total - the battery is the pool, so hits drain it. It also empties on its own in 45 seconds. When the pool runs out mid-hit, the remainder reaches the player. |
| **Protection Orb** | 11,000-15,000 | Players inside the field take 25% less damage, but the orb burns through its charge about three times faster than a normal orb. |
| **Poison Crossbow** | 19,000-24,000 | 7 damage per bolt and a poison that deals 125 damage over 15 seconds. Every bolt refreshes the poison to 15 s, it never stacks. A poisoned monster stays actively hostile and hunts players while the poison lasts. A third fewer shots than a normal pistol. |
| **Decoy Grenade** | 1,000-1,300 | Arm it and throw it: for 5 seconds it lures nearby monsters to itself, then disappears. |
| **Pulse Ice Pistol** | 11,000-15,000 | Freezes a monster for 4.5 seconds without pushing it. Frozen monsters cannot be picked up, take only 60% of firearm damage, and take 35% more from melee weapons. |
| **Sticky Pistol** | 9,000-10,000 | Pulls a monster or object toward you, as long as you could lift it freely with one strength level less than you have. |

Each drone, orb and gun has its own on/off switch in the `Modules` section of the config.

### Team ping system

Press the ping key to mark whatever you're aiming at. Pings use the game's own
`ValuableDiscoverGraphic` highlight (the animated corner-bracket square from valuable discovery),
tinted per ping type, so they match the in-game style, track their target, dim off-screen and fade
out on their own. Pings are broadcast over Photon carrying the pinged object's PhotonView ViewID, so
every client highlights the same object; Location pings anchor to a small proxy at the point.

Gated behind ping upgrades:

- **Ping Info** — enriches ping labels (value, rarity, name).
- **Ping Anywhere** / **Pin** — unlocks Location pings (anywhere you aim, visible through walls),
  capped per player.

A global anti-flood cap limits how many pings can be on screen at once.

#### Level results (MVP / OOPS CHAMP)

When a level ends, a results board shows per-player loot hauled, loot broken, deaths and revives,
plus an **MVP** (best score) and an **OOPS CHAMP** (most value broken). The host records the stats
and broadcasts them, so everyone running the mod sees identical numbers; hauled loot is credited
only on a successful extraction.

#### Lobby & quality-of-life

- **Map Vote** — in the truck lobby players vote on the next map among a few random candidates
  (number keys or on-screen buttons). The leader when the truck departs is used; no votes = vanilla
  random pick.
- **Late Join** — the host keeps the room joinable during a run/shop/truck and re-syncs mod state
  to anyone who connects mid-game.
- **More Players** — raise the room's max player count above the default 6 (host-side).
- **Shop price scaling** — optionally scale shop prices with player count (host-side).
- **Shop duplicates** and **Shopping List** HUD — a cart overview in the shop with per-item
  quantities, summed prices and a running total vs. team currency.
- **Item Unstuck** — relocates valuables embedded in geometry back to a safe spot, and keeps
  valuables invulnerable until first grabbed (host-side anti-softlock).
- **Auto Backup Saves** — backs up the run save before REPO deletes it on a level fail/wipe, and
  can keep the failed save loadable from its last checkpoint.
- **Dead Map Access** — dead/spectating players can open their map tool.
- **Spectator Minimap** — while dead, hold a key to show the game's own map render in the centre
  of the screen.

#### HUD & diagnostics

- **Level Info** panel — logs each level change (run number + name, plus rolled surprises) using
  the game's native TMP font.
- **Server Info** overlay — Photon region, ping, room code and player count (read-only, no sockets).
- **Level timer** on the native HUD for timed levels.
- **Photon HUD** — opt-in diagnostic that probes Photon region name servers (leave off for normal play).
- **Diagnostics** — switches to disable all patches or all runtime objects for troubleshooting.

### Default keys

| Action | Key | Config section |
|---|---|---|
| Place ping | `Mouse2` (middle mouse) | Ping |
| Dismiss / recall results board | `F7` | Round Results |
| Hide / show Level Info | `F8` | Level Info |
| Spectator minimap (hold) | `Tab` | Spectator Minimap |
| Map zoom in / out (Map Suite L7+) | `-` / `=` | Player Upgrades |
| Map vote | number keys | Map Vote |

### Configuration

All settings live in `BepInEx/config/com.kubai.increasehardmod.cfg` and are editable in-game via
REPOConfig. **Every module on/off switch is gathered in the first section, `Modules`** (surprises, upgrades,
each drone and gear item, each difficulty scaling stat, lobby tools, HUDs...), so you don't have to scroll
through tuning values to find them; values from the old sections are carried over the first time. Other sections: General, Enemies, Quota, Surprises, Surprise Modifiers, Modifier Tuning,
Player Upgrades, Truck Upgrades, Ping, Shop, Map Vote, Late Join, Item Unstuck, More Players,
Dead Map Access, Spectator Minimap, Round Results, Auto Backup Saves, and HUD toggles (Level Info,
Server Info, Shopping List, Photon HUD), plus Diagnostics.

### Building

Requires the .NET SDK and a local REPO install. The project references game assemblies from the
Steam install and publicizes `Assembly-CSharp` at build time.

```bash
dotnet build
```

Override the game location if it isn't in the default Steam path:

```bash
dotnet build -p:GameDir="D:\Games\REPO"
```

The build deploys the compiled plugin into your BepInEx plugins folder for the active profile.

### Notes

- `_decompiled/` (game code) is git-ignored and regenerated locally; it is proprietary and not
  distributed.
- Menu-safe by design: Photon/socket probes are gated to `InRoom` so nothing touches networking on
  the main menu.

---

## 🇷🇺 Русский

Мод сложности и геймплея для **R.E.P.O.** (BepInEx / HarmonyLib / REPOLib). Чем глубже забег, тем
сложнее игра; на уровнях случайно выпадают «сюрпризы»-модификаторы; плюс кастомные улучшения в
магазине, командная система пингов, таблица итогов уровня и набор удобств для лобби.

- **GUID:** `com.kubai.increasehardmod`
- **Версия:** `0.4.0`

### Содержание

- [Требования](#требования)
- [Возможности](#возможности)
- [Клавиши по умолчанию](#клавиши-по-умолчанию)
- [Настройка](#настройка)
- [Сборка](#сборка)
- [Примечания](#примечания)

### Требования

- [BepInEx 5](https://github.com/BepInEx/BepInEx) (5.4.21)
- [REPOLib](https://thunderstore.io/c/repo/p/Zehs/REPOLib/) — нужен для кастомных улучшений в
  магазине. Клиент без REPOLib не увидит эти улучшения.
- Мод желательно ставить всем в лобби, чтобы избежать рассинхрона (часть функций работает на стороне хоста).

### Возможности

#### Рост сложности

Сложность зависит от `RunManager.levelsCompleted`, поэтому у всех клиентов значение одинаковое —
берётся из синхронизируемого счётчика уровней игры (решает хост, лишней сети нет). Настраиваются
стартовый уровень, число уровней на шаг, прирост за шаг и потолки для:

- **Здоровья врагов** — с индивидуальными потолками для каждого врага (Elsa и Loom по умолчанию исключены).
- **Урона** и **скорости** врагов.
- **Времени респауна врагов** (чем глубже, тем быстрее, с минимальным множителем).
- **Квоты** — рост ограничен и множителем, и долей доступного лута.

#### Модификаторы-«сюрпризы»

На подходящем уровне может выпасть один или несколько случайных модификаторов; хост рассылает их
через Photon, так что у всего лобби набор одинаковый. Каждый модификатор можно отключить отдельно,
шанс особого уровня настраивается.

| Группа | Модификатор | Эффект |
|---|---|---|
| Лут | Хрупкий лут | Лут легче ломается и теряет стоимость |
| Лут | Тяжёлый лут | Лут весит больше |
| Лут | Лёгкий лут | Лут весит меньше |
| Лут | Защищённый лут | Лут получает меньше урона |
| Лут | Опасный лут | Опасные ценности сильнее ранят игроков |
| Уровень | Доп. малая / большая тележка | Появляется лишняя тележка |
| Уровень | Больше душ | С монстров падает больше душ |
| Уровень | Дорогие души | Каждая душа стоит больше |
| Уровень | Разбросанные улучшения | Улучшения разбросаны по уровню, применяются только в грузовике |
| Уровень | Больше гача-кубов | Появляется больше гача/кубов |
| Игрок | Нет карты | Карты нет, пока не найдёшь улучшение карты |
| Игрок | Сниженное HP | Все начинают раненными (случайный %) |
| Игрок | Авто-воскрешение | Одно авто-воскрешение на игрока (по желанию у грузовика, с полным HP) |
| Игрок | Потеря денег при смерти | При смерти теряется % собранных денег |
| Игрок | Таймер уровня | Уровень на время: убийства добавляют время, смерти отнимают, квота добавляет; на 0 раскладка перегенерируется |
| Игрок | Смена голосов | Голоса игроков меняются местами / случайно |
| Монстры | Души взрываются сильнее | Взрывающиеся души наносят больше урона |
| Монстры | Монстры устойчивы к ямам | Монстры получают меньше урона от смертельных ям |
| Монстры | Лёгкие / тяжёлые монстры | Все монстры весят меньше (легко толкать) или больше (тяжело толкать) |

#### Масштаб уровня и события экономики

У каждого свой переключатель в секции `Modules`, числа лежат в секциях *Level Scaling* и *Economy*.
Всё считает хост, клиенты получают результат обычной синхронизацией игры.

| Модуль | Что делает (по умолчанию) |
| --- | --- |
| **Ценность карты** | Ценности дорожают с глубиной: x1,02 за уровень после 2-го, максимум x3. |
| **Число врагов** | Больше монстров после 8-го уровня: +3% за уровень, максимум x2 (добавка берётся из самого сильного уже открытого тира). |
| **Размер уровня** | Карты крупнее после 8-го уровня: +3% модулей и сетки за уровень, максимум x1,5. Больше модулей — больше точек экстракции по пороговым значениям игры. |
| **Экстракции** | Одна дополнительная точка экстракции раз в 10 уровней, не больше 5 на уровень. |
| **Фазы луны** | По умолчанию выключено. Если включить, луна меняется раз в 15 уровней вместо 5. |
| **События экономики** | Начиная с 3-го уровня уровень с шансом 50% получает случайное событие: множитель стоимости лута, квоты и/или оплаты за экстракцию. 20 событий, от *Gold Rush* (+35% к стоимости лута) и *Windfall* (+30% к оплате) до *Tax Audit* (-25% к оплате, +10% к квоте) и *Crushing Quota* (+40% к квоте). Плохие события с глубиной выпадают чаще, хорошие реже. Активное событие показано под информацией об уровне. |

#### Улучшения в магазине (через REPOLib)

Всё продаётся в обычном магазине. Цены примерные (игра сама масштабирует их по числу игроков) и,
как и все числа ниже, настраиваются.

**Личные улучшения** - каждый игрок покупает и качает свои:

| Улучшение | Уровни | Что делает |
| --- | --- | --- |
| **Slower Stamina Use** | 5 | Бег тратит на 10% меньше выносливости за уровень. ~4 000 |
| **HP Regeneration** | 3 | Лечит 0,2% максимума здоровья в секунду за уровень, но только пока здоровье не поднимется до 30% от максимума - это аварийная струйка, а не полное лечение. ~20 000 |
| **Death Protection** | 1 заряд | Переживаешь один смертельный удар с 1 HP. Заряд тратится, когда он тебя спас; купи снова, чтобы восстановить. ~50 000 |
| **Drone Battery Life** | 20 | Батареи твоих дронов служат на 5% дольше за уровень (до +100%). ~6 000 |
| **Gun Battery Life** (своё на каждый тип оружия: пули / лазер / транквилизатор) | по 20 | Батарея этого оружия служит на 5% дольше за уровень (до +100%). ~6 000 |
| **Autoscan** | 5 | Раз в секунду находит ценности вокруг, на 5 м дальше за уровень (5-25 м). ~8 000 |
| **Map Suite** | 10 | Одно улучшение, десять функций карты: L1 число живых игроков, L2 число мёртвых, L3 активные монстры, L4 возрождающиеся монстры, L5 направление к квоте и трекер ценностей (найдено/всего, добыча), L6 командные пинги на карте, L7-L8 отдаление карты (клавиши `-` / `=`), L9 трекер косметических ящиков, L10 монстры точками. По умолчанию заменяет ванильное *Map Player Count*. ~15 000 за первый уровень |
| **Ping Info** | 1 | Твои пинги показывают стоимость ценности, редкость косметики и точное название. ~6 000 |
| **Ping Anywhere** | 1 | Ставь пинги-метки на любой поверхности, видимые сквозь стены (число живых пингов на игрока ограничено). ~6 000 |
| **Pin** | 1 | Постоянная версия Ping Anywhere: купил один раз - навсегда. ~30 000 |

**Улучшения грузовика** - покупаются один раз и действуют на всю команду:

| Улучшение | Уровни | Что делает |
| --- | --- | --- |
| **Heal Power** | 3 | Всё лечение (финальное лечение грузовика, аптечки, лечащие дроны) сильнее на 25% за уровень. От 10 000 |
| **Charge Efficiency** | 3 | Зарядная станция тратит на 20% меньше энергии за уровень при зарядке предметов. От 10 000. Работает вместе с экономикой кристаллов: на зарядку уходит меньше кристаллов |
| **Loot Cash-In** | 1 | Когда все квоты закрыты, оставшиеся ценности продаются автоматически за 75% стоимости. Разовая разблокировка, 50 000 |

#### Утилитарные дроны (магазин)

Четыре дополнительных дрона. Они сделаны на основе игровых дронов, поэтому наведение, луч,
прикрепление, выключатель, анимация раскрытия и батарея работают так же, как у оригиналов. Мод
должен стоять у всех игроков, и переключатели модулей должны совпадать.

| Дрон | Цена (категория) | Что делает |
| --- | --- | --- |
| **Ghost Drone** | 11 000-15 000 | Прикрепи к ценности: игроки проходят сквозь неё, а при экстракции она не считается и не засасывается, пока дрон включён. Вес, цена и прочность не меняются. Кто остался внутри при выключении дрона, проходит сквозь, пока не выйдет. |
| **Silencer Drone** | 7 000-9 000 | Прикрепи к ценности, предмету, тележке, игроку или монстру: шум от него (столкновения, звуки, голос и TTS, радиус, с которого монстры идут на шум) падает до 8% по умолчанию. Цель не глохнет. |
| **Link Drone Set (A + B)** | 19 000-24 000 | Одна покупка даёт обе половины. Прикрепи A к одному объекту, B к другому в радиусе 6 м: физический трос стягивает их (трос прямой, цветной; бледно-жёлтый в ожидании, красный при натяжении). Если держать один конец, второй медленно подтягивается, 0,75 м/с до 1,25 м. При долгом перенапряжении трос отпускает; выключи и включи половину, чтобы связать снова. |
| **Repair Drone** | 30 000-36 000 | Прикрепи к повреждённой ценности: возвращает потерянную стоимость до оригинальной, до 500 в секунду. Полной батареи хватает на 25 000 - больше, чем стоит кристалл в 20 000 для перезарядки. Полностью разрушенную ценность починить нельзя. |

#### Дополнительное снаряжение (магазин)

Орбы, оружие и граната, тоже на основе ванильных предметов (поэтому модели, управление и индикатор
батареи ощущаются родными), но со своими моделями, анимациями и звуками.

| Предмет | Цена (категория) | Что делает |
| --- | --- | --- |
| **Defence Orb** | 19 000-24 000 | Защищает **лут**. Пока активен, стоимость, которую ценности потеряли бы при поломке внутри поля, оплачивается из его батареи (около 3 500 на полном заряде). Медленно разряжается сам (по умолчанию 20% в минуту). |
| **Defence Drone** | 11 000-15 000 | Защищает **игрока**: прикрепи к товарищу (или себе), он поглощает до 350 урона суммарно - пул это батарея, удары её расходуют. Сам тоже разряжается за 45 секунд. Если пул кончился посреди удара, остаток доходит до игрока. |
| **Protection Orb** | 11 000-15 000 | Игроки в поле получают на 25% меньше урона, но орб тратит заряд примерно втрое быстрее обычного. |
| **Poison Crossbow** | 19 000-24 000 | 7 урона за болт и яд, наносящий 125 урона за 15 секунд. Каждый болт обновляет яд до 15 с, не накапливается. Отравленный монстр остаётся активно враждебным и охотится на игроков, пока яд действует. На треть меньше выстрелов, чем у обычного пистолета. |
| **Decoy Grenade** | 1 000-1 300 | Активируй и брось: 5 секунд приманивает ближайших монстров к себе, затем исчезает. |
| **Pulse Ice Pistol** | 11 000-15 000 | Замораживает монстра на 4,5 секунды, не отталкивая. Замороженного нельзя поднять, он получает 60% урона от огнестрела и на 35% больше от оружия ближнего боя. |
| **Sticky Pistol** | 9 000-10 000 | Притягивает монстра или предмет к тебе, если ты смог бы свободно поднять его с силой на один уровень ниже твоей. |

У каждого дрона, орба и оружия свой переключатель в секции `Modules` конфига.

#### Командные пинги

Нажми клавишу пинга, чтобы отметить то, на что целишься. Пинги используют родную подсветку игры
`ValuableDiscoverGraphic` (анимированная рамка с уголками из обнаружения ценностей), окрашенную
под тип пинга, — выглядят как часть игры, следуют за целью, тускнеют вне экрана и сами исчезают.
Пинги рассылаются через Photon вместе с ViewID PhotonView объекта, поэтому у всех подсвечивается
один и тот же объект; пинг «Точка» крепится к небольшому прокси в этом месте.

Зависят от улучшений:

- **Ping Info** — расширяет подписи пингов (стоимость, редкость, название).
- **Ping Anywhere** / **Pin** — открывают пинги-точки (куда угодно, видны сквозь стены), с лимитом на игрока.

Глобальный анти-флуд ограничивает число пингов на экране одновременно.

#### Итоги уровня (MVP / OOPS CHAMP)

По завершении уровня показывается таблица: у каждого игрока — вынесенный лут, разбитый лут, смерти
и воскрешения, а также **MVP** (лучший результат) и **OOPS CHAMP** (больше всего испорченной
стоимости). Статистику ведёт и рассылает хост, поэтому у всех с модом цифры одинаковые; вынесенный
лут засчитывается только при успешной эвакуации.

#### Лобби и удобства

- **Голосование за карту** — в лобби грузовика игроки выбирают следующую карту из нескольких
  случайных (цифровые клавиши или кнопки на экране). Побеждает лидер на момент отъезда; нет голосов —
  обычный случайный выбор игры.
- **Поздний вход** — хост оставляет комнату открытой во время забега/магазина/грузовика и
  синхронизирует состояние мода для зашедших посреди игры.
- **Больше игроков** — поднимает лимит комнаты выше стандартных 6 (на стороне хоста).
- **Масштаб цен в магазине** — цены могут расти с числом игроков (на стороне хоста).
- **Дубликаты в магазине** и HUD **Shopping List** — обзор корзины в магазине: количество,
  суммарная цена по каждой позиции и итог относительно общих денег команды.
- **Item Unstuck** — возвращает ценности, застрявшие в геометрии, на безопасное место и делает
  их неуязвимыми до первого захвата (защита от софтлока, на стороне хоста).
- **Auto Backup Saves** — бэкап сохранения перед тем, как REPO удалит его при провале/вайпе, с
  возможностью оставить провалившееся сохранение загружаемым с последнего чекпоинта.
- **Карта для мёртвых** — мёртвые/наблюдающие игроки могут открывать карту.
- **Миникарта наблюдателя** — пока ты мёртв, удерживай клавишу, чтобы показать родную карту игры в
  центре экрана.

#### HUD и диагностика

- Панель **Level Info** — журнал смены уровней (номер забега + название, выпавшие сюрпризы) родным шрифтом TMP.
- Оверлей **Server Info** — регион Photon, пинг, код комнаты и число игроков (только чтение, без сокетов).
- **Таймер уровня** в родном HUD для уровней на время.
- **Photon HUD** — опциональная диагностика, пробует серверы регионов Photon (в обычной игре держи выключенным).
- **Diagnostics** — переключатели, отключающие все патчи или все рантайм-объекты для поиска проблем.

### Клавиши по умолчанию

| Действие | Клавиша | Секция конфига |
|---|---|---|
| Поставить пинг | `Mouse2` (средняя кнопка мыши) | Ping |
| Скрыть / вернуть таблицу итогов | `F7` | Round Results |
| Скрыть / показать Level Info | `F8` | Level Info |
| Миникарта наблюдателя (удержание) | `Tab` | Spectator Minimap |
| Зум карты + / − (Map Suite ур. 7+) | `-` / `=` | Player Upgrades |
| Голосование за карту | цифровые клавиши | Map Vote |

### Настройка

Все настройки хранятся в `BepInEx/config/com.kubai.increasehardmod.cfg` и редактируются прямо в
игре через REPOConfig. **Все переключатели модулей собраны в первой секции `Modules`** (сюрпризы, улучшения, каждый дрон и предмет, каждая характеристика роста сложности, лобби-инструменты, HUD...), чтобы не листать значения настройки; значения из прежних секций переносятся при первом запуске. Остальные секции: General, Enemies, Quota, Surprises, Surprise Modifiers, Modifier
Tuning, Player Upgrades, Truck Upgrades, Ping, Shop, Map Vote, Late Join, Item Unstuck, More
Players, Dead Map Access, Spectator Minimap, Round Results, Auto Backup Saves, переключатели HUD
(Level Info, Server Info, Shopping List, Photon HUD) и Diagnostics.

### Сборка

Нужны .NET SDK и локальная установка REPO. Проект ссылается на сборки игры из Steam и делает
`Assembly-CSharp` публичной при сборке.

```bash
dotnet build
```

Если игра лежит не в стандартной папке Steam, укажи путь:

```bash
dotnet build -p:GameDir="D:\Games\REPO"
```

Сборка кладёт готовый плагин в папку plugins активного профиля BepInEx.

### Примечания

- `_decompiled/` (код игры) исключён через git-ignore и создаётся локально; он проприетарный и не распространяется.
- Безопасно для меню: проверки Photon/сокетов выполняются только при `InRoom`, поэтому в главном меню сеть не затрагивается.
