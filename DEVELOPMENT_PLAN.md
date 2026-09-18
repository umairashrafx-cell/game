# ROYAL VAULT — Development Plan

**Engine:** Unity 6000.6.1f1 · **Language:** C# · **Platform:** Android (portrait) · **Genre:** Hybrid-casual puzzle + collection + restoration

---

## 1. Phase 0 — Project Audit (complete)

### 1.1 What was found

The working directory `C:\Users\m_uma\Desktop\gaming app 2` was **completely empty** — 0 files. There is no pre-existing
Unity project, no scenes, no scripts, no packages, and therefore **no existing work to preserve** and no compilation
errors to fix. This is a greenfield build.

### 1.2 Toolchain audit

| Item | Status | Notes |
|---|---|---|
| Unity Editor 6000.6.1f1 | ✅ Installed | `C:\Program Files\Unity\Hub\Editor\6000.6.1f1` |
| Unity Hub | ❌ **Not installed** | Editor is standalone. Module management requires the Hub. |
| Licensing | ✅ Working | Headless `-batchmode` run returned exit code 0. |
| Headless CLI | ✅ Working | **Critical capability** — enables compile + automated test runs without the GUI. |
| Windows Standalone module | ✅ Installed | Usable for fast desktop playtesting. |
| WebGL module | ✅ Installed | Not needed. |
| **Android Build Support** | ❌ **Not installed** | **Blocker for shipping.** No SDK / NDK / OpenJDK. |
| Git | ✅ Installed | Repo not yet initialised. |
| .NET CLI | ❌ Not on PATH | Not required; Unity bundles its own toolchain. |

### 1.3 Risks identified

| # | Risk | Severity | Mitigation |
|---|---|---|---|
| R1 | Android module missing → cannot produce APK/AAB | **High** | Install Unity Hub, then add Android Build Support (+ SDK/NDK/OpenJDK) to 6000.6.1f1. Needed before Phase 10, not before Phase 1. |
| R2 | No GUI feedback loop — I cannot *see* or *feel* the game | **High** | Architecture mitigates this: the puzzle simulation is pure C# and headlessly testable. Game feel still requires human playtesting. |
| R3 | Building scenes/prefabs as hand-written YAML is fragile | **Medium** | **Build the board and UI procedurally from code.** One near-empty bootstrap scene. Also better for a data-driven game. |
| R4 | Shipping an impossible level | **High** | Every level passes a solver in an automated test. A level that cannot be proven solvable fails CI. |
| R5 | Scope — the brief is a full commercial product | **High** | Strict phase gating. No meta systems until the core interaction is proven fun. |
| R6 | Project sits on Desktop, not in version control | Medium | `git init` + `.gitignore` early. |
| R7 | Mobile GC spikes causing frame hitches | Medium | Struct-based moves, no per-frame LINQ/allocation, object pooling for pieces and particles. |

### 1.4 Housekeeping note

Creating the project via CLI initially produced a **stray folder at `C:\Users\m_uma\Desktop\gaming`** (Unity's
`-createProject` received an unquoted path containing spaces). The project was then correctly recreated in place.
**`Desktop\gaming` is a throwaway duplicate and is safe to delete** — it is not referenced by anything.

---

## 2. Core design decision — the signature mechanic

### 2.1 The problem with the obvious implementation

The default way to build this brief is a water-sort clone with jewelry sprites: N containers, top-only access, move a
piece onto an empty container or onto a matching colour. The brief explicitly rejects this, and it is right to — colour
sort has no *decisions*, only bookkeeping. Every piece has exactly one correct destination, so the player is executing
a sort, not solving a puzzle. It also fails the accessibility requirement, because colour is the only signal.

### 2.2 The proposal — **Dual Identity**

Every jewelry piece carries **two** independent attributes:

- **Material** — Gold, Diamond, Ruby, Emerald, Sapphire, Pearl → carries *colour*
- **Form** — Ring, Necklace, Earring, Bracelet, Pendant, Crown → carries *silhouette*

So a piece is a *Ruby Ring*, an *Emerald Necklace*, a *Pearl Crown*. Trays are vertical velvet jewelry trays with a
fixed number of slots; **only the topmost piece can be lifted.**

The rule that makes this a game:

> **An empty tray has no identity. The first piece placed leaves it undecided. The second piece *locks* the tray to
> whichever attribute the two pieces share — its Material or its Form. From then on the tray only accepts pieces
> matching that locked attribute.**

Worked example:

```
Empty tray ──place Ruby Ring──▶ identity undecided {Ruby, Ring}
   ├── place Ruby Necklace ──▶ shares Ruby ──▶ tray LOCKS to RUBY   (now accepts any ruby piece)
   └── place Gold Ring     ──▶ shares Ring ──▶ tray LOCKS to RING   (now accepts rings of any material)
```

### 2.3 Why this is the right call

- **The player chooses what each tray becomes.** That is a real, reversible, consequential decision — exactly the
  "interesting decisions" the brief asks for instead of artificial move limits.
- **Every piece is dual-purpose**, so there is genuine planning tension: committing a tray to Ruby may strand your rings.
- **Accessibility is satisfied by the design itself**, not bolted on. Material *and* Form are both load-bearing, so
  shape and colour each carry meaning and colour-blind players lose nothing.
- **It is still understandable in 30 seconds:** *"Match the gem, or match the shape. Fill a tray to seal it."*
- **It produces the difficulty curve the brief wants, for free** (see below).

### 2.4 How it ramps across worlds

The difficulty lever is **how often each attribute value occurs**, and it was found by measurement, not intuition.

> A tray is completed by `TrayCapacity` pieces sharing one attribute. So **an attribute value occurring fewer than
> `TrayCapacity` times can never complete a tray.** Keep every Form below capacity and material becomes the only way
> to finish — the game is a gentle "match the gem" sort. Raise one Form to exactly capacity and the genuine
> Material-or-Form choice switches on.

| World | Lever | Emergent complexity |
|---|---|---|
| 1 — Golden Workshop | Every Form occurs **< capacity** | Only gem-sets can be completed. Effectively a colour sort. Effortless. |
| 1, final level | One Form raised to **= capacity** | The first shape-set becomes possible. The real mechanic reveals itself, with no tutorial text. |
| 2 — Diamond Palace | Several Forms at capacity | Material-or-Form is a live decision every move. Locks introduced. |
| 3 — Ruby Chamber | + frozen / hidden pieces | Genuine planning. |
| 4+ | Full matrix | Combined mechanics, Royal Challenges. |

> **Correction.** An earlier draft of this plan proposed making World 1 "all Rings, materials vary" to degenerate into
> a colour sort. That is wrong, and the test suite proved it: if *every* piece shares a Form, then every tray accepts
> every piece and any four pieces seal as a valid "set of rings". The result is not an easy puzzle — it is no puzzle
> at all. The occurrence-count lever above is the correct mechanism.

The mechanic itself teaches the player, so early levels need no tutorial text — they simply cannot go wrong.

### 2.5 The consequence that reshaped level design

A tray's identity is the intersection of its pieces' attributes, so a **partially-emptied mixed tray shares nothing
and can therefore accept nothing.** Starting trays are *write-only* until fully drained.

This is the single biggest difference from water sort, where a partly-poured tube can always receive its own colour
again. It makes buffer space far scarcer than the equivalent water-sort layout, and it is why several
carefully hand-authored levels turned out to be provably impossible.

Measured across 192 solver runs (all definitive, no timeouts):

| Spare trays | Result |
|---|---|
| 1 | 0/8 to 5/8 solvable — a coin flip. **Never ship.** |
| 2 | 7/8 to 8/8 solvable across every configuration tested |
| 3+ | 8/8, with more forgiving play |

**Two spare trays is the floor for a fair level.** Difficulty is therefore tuned through *layout* and attribute
counts, never by starving the player of working space — which is exactly the "interesting decisions, not artificial
scarcity" the brief asks for.

Because hand-authoring against this rule is unreliable, levels are authored *against the solver*: a tool searches
seeded layouts, discards the impossible ones, and emits the survivors as source code.

### 2.5 How the rest of the brief maps on

**Royal Match** fires when a tray fills with a coherent locked set: pieces align, gold light sweeps the rim, gems
flash, the tray seals. **Royal Chain** counts consecutive seals without Undo. Because the Dual Identity rule makes
seals *earned decisions* rather than inevitabilities, chaining actually means something.

---

## 3. Architecture

### 3.1 The load-bearing decision: simulation / presentation split

```
┌─────────────────────────────────────────┐
│  RoyalVault.Core   (pure C#, no engine) │   ← headlessly testable, deterministic
│  BoardState · Tray · Move · RuleEngine  │      zero allocation on the hot path
│  WinDetector · LevelDefinition · Solver │
└────────────────────┬────────────────────┘
                     │ events / state reads
┌────────────────────▼────────────────────┐
│  RoyalVault.Game   (MonoBehaviours)     │   ← animation, juice, audio, haptics
│  BoardView · TrayView · PieceView       │
└─────────────────────────────────────────┘
```

The simulation core has **no reference to UnityEngine**. This is the most important choice in the project because it:

1. Makes the puzzle rules **unit-testable in the CLI** — which matters enormously here, since I cannot see the screen.
2. Lets the **solvability validator brute-force levels** at thousands of boards per second.
3. Keeps the rules in one place, so presentation bugs can never become rule bugs.
4. Eliminates GC churn during play.

### 3.2 Module map

Split across three assembly definitions so build times stay low and layering is *enforced by the compiler*:

- `RoyalVault.Core` — board, rules, moves, undo, win detection, level data, solver/validator
- `RoyalVault.Game` — views, managers (Board, Level, Booster, Economy, Collection, Vault, Progression, Daily, Ad, IAP, Save, Audio, Haptic, Analytics, UI), all engine-facing
- `RoyalVault.Tests.EditMode` — tests against Core

No god classes; managers communicate through a lightweight event bus rather than direct references.

### 3.3 Data-driven content

ScriptableObjects for jewelry, worlds, economy, boosters, collections, vault rooms and ad frequency; JSON for level
layouts so levels can be authored, diffed and validated in bulk without opening the editor. **No economy or tuning
number is ever written inline in a script.**

### 3.4 Save system

Versioned, flat, `JsonUtility`-serialisable structs (deliberately avoiding a Newtonsoft dependency). Atomic write via
temp-file-then-replace. Corrupt save → fall back to last good, never hard-crash. Migration hook from day one.

---

## 4. Milestones

| Phase | Deliverable | Gate to pass |
|---|---|---|
| **0** | Audit + this plan | ✅ **Done** |
| **1** | Core prototype: rules, undo, win detection, 5 hand-made levels, procedural board scene | ⏳ **Built and verified — awaiting the one gate a machine cannot check.** 32 automated tests green; all 5 levels proven solvable. **Still needs a human to confirm it is fun.** |
| **2** | Game feel: movement arcs, selection lift, Royal Match, Royal Chain, particles, audio + haptic hooks | The core interaction feels excellent before any meta system exists |
| **3** | LevelData pipeline, loader, difficulty config, validator tooling, first 20 intentional levels | Every shipped level passes the solver |
| **4** | First-session experience: boot → menu → tutorial → win → first restoration → first collection unlock | The first 10 minutes tested end to end |
| **5** | Meta game: Vault, Collections, Restoration, worlds, daily rewards | Progression always shows exactly one clear next objective |
| **6** | Content to 100 levels, special mechanics, Royal Challenges, Treasure levels | Controlled difficulty curve, no filler levels |
| **7** | Monetization: rewarded ads, frequency-capped interstitials, IAP, Remove Ads | Test ad IDs only; reward strictly on confirmed completion |
| **8** | Analytics wired to real gameplay | Drop-off and difficulty measurable |
| **9** | Polish: performance, transitions, audio, accessibility, responsiveness | Smooth on low-end Android |
| **10** | Release prep: AAB, signing, icons, store checklist | Nothing published |

**Phase gating is strict.** Phase 2 does not begin until Phase 1 is fun. A beautiful meta-game wrapped around a boring
core is the most expensive mistake this project could make.

---

## 5. Testing strategy

Tests exist where they protect real value, not for coverage theatre.

**Solver choice — and why it changed.** The validator first used breadth-first search, to get the shortest solution
for free as a par count. On 16-piece boards the BFS frontier exploded, and because the sweep treated "budget
exhausted" as "unsolvable", **adding a spare tray appeared to make levels harder** — a physically impossible result
that exposed the flaw. A validator that reports "impossible" when it means "I gave up" is worse than none, because it
silently deletes good levels. Solvability is now proven depth-first (memory scales with depth, not frontier) and
returns an explicit *budget exhausted* state that is never conflated with *unsolvable*. BFS is retained only as
`SolveShortest` for par on small boards.

**Priority (Phase 1):**
- Move legality — every branch of the Dual Identity rule, including the lock-on-second-piece edge case
- Undo — exact state restoration, including identity unlock and chain reset
- Win detection and dead-end (no legal move) detection
- **Level solvability** — a breadth-first solver proves every shipped level is completable; an unsolvable level fails the build

**Later:** save/load round-trip + version migration, economy arithmetic, daily-reward rollover across timezones.

**Explicitly not tested:** thin Unity wrappers, animation timings, anything better judged by eye.

Run headlessly via:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.6.1f1\Editor\Unity.exe" -runTests -batchmode -projectPath "C:\Users\m_uma\Desktop\gaming app 2" -testPlatform EditMode -logFile -
```

---

## 6. Current state

**Done and verified (32 automated tests, all green):**

- `RoyalVault.Core` — pieces, trays, Dual Identity rule engine, moves, undo, Royal Chain, win and dead-end detection.
  Compiler-enforced to contain no engine types (`noEngineReferences`).
- Depth-first solvability prover + breadth-first par solver, with an explicit *budget exhausted* state.
- `LevelValidator` — structural checks plus a solvability proof. **All 5 levels pass.**
- `LevelAuthoringTool` — searches seeded layouts and emits verified levels as source.
- 5 hand-designed levels with measured, rising search resistance: **11 → 20 → 39 → 944 → 1325**.
- `RoyalVault.Game` — procedural UI, generated sprites with a distinct silhouette per form, tap-to-lift input,
  arc move animation, Royal Match seal sequence, rejection shake, Android haptics, safe-area handling, undo/restart.
- `Game.unity`, generated by an editor script, registered as the startup scene.

**How to verify any of this without opening the editor:**

```bash
"C:\Program Files\Unity\Hub\Editor\6000.6.1f1\Editor\Unity.exe" -runTests -batchmode -nographics -projectPath "C:\Users\m_uma\Desktop\gaming app 2" -testPlatform EditMode -logFile -
```

### Next steps

1. **Play it.** Phase 1's real gate is whether the core interaction is enjoyable, and that needs human hands.
2. Feed the verdict back into the mechanic before any meta system is built.
3. Then Phase 2 (game feel) — not before.

### Open items for the user

| # | Item | Why it matters |
|---|---|---|
| R1 | Install Unity Hub, then **Android Build Support + SDK/NDK/OpenJDK** for 6000.6.1f1 | Required to produce an APK/AAB. Large download — start it early. Not blocking Phases 1-9. |
| — | Delete the stray folder `C:\Users\m_uma\Desktop\gaming` | A throwaway duplicate from the first CLI attempt. Nothing references it. |
| R6 | `git init` in the project root | `.gitignore` is already written. Not initialised, since committing was not requested. |
| — | Par-move star thresholds | The depth-first solver returns the *first* solution, not the shortest (level 4 solves in 887 moves), so it must not be used for star targets. Needs the breadth-first solver or a dedicated optimiser. |
