# Increase Hard Mod

**English** · [Русский](#-русский)

---

## 🇬🇧 English

A difficulty & gameplay mod for **R.E.P.O.** (BepInEx / HarmonyLib / REPOLib). It ramps the challenge
as a run goes deeper, sprinkles in random per-level "surprise" modifiers, adds custom shop upgrades,
a team ping system, a round-results board and a set of quality-of-life features for lobbies.

- **GUID:** `com.kubai.increasehardmod`
- **Version:** `0.3.0`

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

#### Custom shop upgrades (via REPOLib)

Per-player:

- **Slower Stamina Use** — sprinting drains less stamina.
- **HP Regeneration** — slowly regenerate health up to a fraction of max HP.
- **Death Protection** — survive one lethal hit at 1 HP; consumed on use, re-buy to restore.
- **Drone Battery Life** and per-gun **Battery Life** — longer batteries (up to +100%).
- **Autoscan** — automatically discovers nearby valuables; higher levels scan farther.
- **Map Suite** — unlocks map features as it levels: live player/monster counts, quota heading and
  valuable tracker, team pings on the map, zoom-out (zoom keys `-` / `=`), trackers. Optionally
  replaces the vanilla *Map Player Count* upgrade.
- **Ping Info**, **Ping Anywhere**, **Pin** — see [Team ping system](#team-ping-system).

Team-wide **Truck upgrades**:

- **Heal Power** — all healing is stronger.
- **Charge Efficiency** — the charging station drains slower.
- **Loot Cash-In** — leftover valuables are auto-cashed once every quota is closed (one-time unlock).

#### Team ping system

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
REPOConfig. Sections: General, Enemies, Quota, Surprises, Surprise Modifiers, Modifier Tuning,
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
- **Версия:** `0.3.0`

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

#### Кастомные улучшения в магазине (через REPOLib)

Личные:

- **Slower Stamina Use** — бег тратит меньше выносливости.
- **HP Regeneration** — медленная регенерация здоровья до доли от максимума.
- **Death Protection** — переживаешь один смертельный удар с 1 HP; расходуется, покупается заново.
- **Drone Battery Life** и **Battery Life** для оружия — батареи служат дольше (до +100%).
- **Autoscan** — автоматически находит ценности рядом; выше уровень — дальше радиус.
- **Map Suite** — открывает функции карты по мере прокачки: живой счётчик игроков/монстров,
  направление к квоте и трекер ценностей, пинги команды на карте, отдаление (зум `-` / `=`),
  трекеры. Может заменять ванильное улучшение *Map Player Count*.
- **Ping Info**, **Ping Anywhere**, **Pin** — см. [Командные пинги](#командные-пинги).

Командные **улучшения грузовика**:

- **Heal Power** — любое лечение сильнее.
- **Charge Efficiency** — зарядная станция тратит энергию медленнее.
- **Loot Cash-In** — оставшиеся ценности автоматически обналичиваются, когда все квоты закрыты (разовая разблокировка).

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
игре через REPOConfig. Секции: General, Enemies, Quota, Surprises, Surprise Modifiers, Modifier
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
