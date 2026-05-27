# FatesRoll

Unity 6 dice-driven exploration and combat prototype. Roll 2× d6, spend energy, walk Steve along the NavMesh toward POIs, and fight enemies when you reach them.

**Current version:** `v0.0.070` (see [`VERSION`](VERSION) and Unity **Player Settings → Version**).

| | |
|---|---|
| **Play scene** | `Assets/Scenes/main.unity` |
| **Unity** | 6000.x (binary-serialized scene) |
| **Repo** | https://github.com/czyzem-stack/fatesRoll |
| **Architecture** | [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — diagrams & script index |

---

## Documentation

| Doc | Contents |
|-----|----------|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Full architecture: system overview, combat, POI, dice, AI, UI, git hooks |
| This README | Quick start, layout, versioning, changelog, troubleshooting |

---

## Architecture (summary)

Detailed Mermaid diagrams live in **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)**. Overview:

```mermaid
flowchart LR
    Roll[Dice roll] --> Energy[Energy cost]
    Roll --> Branch{In combat?}
    Branch -->|no| Walk[Hero NavMesh → POI by order]
    Branch -->|yes| FightRoll[Combat damage + anim]
    Walk --> Arrive[Proximity 3m]
    Arrive --> Arrival[Arrival attack]
    Arrival --> Fight[Enemy retaliate]
    FightRoll --> Fight
    Fight --> Die[Enemy dies]
    Die --> Resolve[POI removed + XP]
```

**Diagram index** (all in `docs/ARCHITECTURE.md`):

| # | Diagram |
|---|---------|
| 1 | System overview |
| 2 | Component dependency map |
| 3 | Singletons / scene objects |
| 4 | Core game loop (sequence) |
| 5 | Dice roll pipeline |
| 6 | Movement & POI order routing |
| 7 | Combat (arrival + in-combat + state) |
| 8 | Enemy AI state machine |
| 9 | POI lifecycle |
| 10 | Editor vs runtime POI setup |
| 11 | Stats & damage formulas (tables) |
| 12 | UI & health bars |
| 13 | Version / README git hooks |
| 14 | Script index |

---

## How to play (editor)

1. Open the project in Unity 6.
2. Open **`Assets/Scenes/main.unity`** (build index 0).
3. Press **Play**.
4. Roll dice (input / UI), watch energy, Steve walks based on the roll total toward the active POI.
5. Reach a POI to trigger combat (orc/slime/skeleton types via `POINode`).

---

## Core loop

```
Roll (energy) → dice settle → XP → walk toward POI (by order) → combat at POI → POI resolved → next order
```

| System | Script(s) | Notes |
|--------|-----------|--------|
| Dice | `DiceSpawner`, `DieResult` | Throw anim, settle, read values; combat vs explore branch |
| Movement | `HeroController` | NavMesh path; leftover steps → arrival damage |
| Energy | `EnergyManager` | Depletes on roll; regen timer; floating text |
| POIs | `POINode`, `POIManager` | `order` visit sequence; `POINodeEditor` builds visuals |
| Combat | `HeroController`, `Enemy` | Arrival hit + in-combat rolls; world-space HP bar |
| Stats | `PlayerStats`, `EnemyData` | RPG formulas; SO exists (runtime wiring TBD) |
| XP / level | `LevelManager` | XP from roll total; level-up animation |
| Tuning | `GlobalSettings` | Movement, energy, melee spacing/timing, XP; `combatLogEnabled`, `verboseGameplayLogs`, `showPath` |
| Steve / enemy stats | `PlayerStats`, `Enemy` | HP, damage, crit, dodge (not on GlobalSettings) |
| QA | `QADashboard`, `QAVersionDisplay` | Roll/distance debug; build version + git hash |

---

## Project layout

```
Assets/
  Scenes/main.unity          # Main game scene (use this, not SampleScene)
  Scripts/                   # Gameplay C# (no asmdef)
  Prefabs/UI/                # Health bar prefab (see POINodeEditor)
  Heroes/                    # Steve anims / controllers
  Dice/                      # Dice prefabs
docs/
  ARCHITECTURE.md            # Mermaid diagrams + technical reference
VERSION                      # Release label: v0.0.XXX
scripts/git-commit.ps1       # Commit with hooks (recommended)
scripts/bump-version.ps1     # Bump patch version only
scripts/update-readme.ps1    # Refresh README (run via commit-msg hook)
.githooks/                   # pre-commit: version; commit-msg: README
```

---

## Versioning

Patch versions use **`v0.0.XXX`** in `VERSION` and **`0.0.XXX`** in Unity.

**On each commit:** version and this changelog update automatically when hooks run.

Option A — wrapper (no global git config):

```powershell
.\scripts\git-commit.ps1 -m "Your commit message"
```

Option B — enable hooks for all commits in this repo:

```powershell
git config core.hooksPath .githooks
```

Or bump manually:

```powershell
.\scripts\bump-version.ps1
```

**Tagged restore points (GitHub):**

| Tag | Notes |
|-----|--------|
| `v0.0.016` | Basic combat + animation fixes |
| `v0.0.014` | Combat prep (DiceRoll anim) |
| `v0.0.013` | Pre-combat checkpoint |
| `v0.0.001` | Initial version tag |

Restore a tag:

```powershell
git fetch --tags
git checkout v0.0.016
```

---

## Git workflow (this project)

- **Commit** when a feature or stable slice works in Play mode.
- **Push** when you want GitHub backup.
- Always **save the scene** in Unity before committing (`main.unity` must be included for level/POI/combat wiring).
- Prefer **one logical change per commit**; version and README changelog update automatically when hooks are on.

**Do not commit** (usually): `Assets/_Recovery/`, `Library/`, `Temp/`, GUI pack reserialize-only prefab noise unless intentional.

---

## Changelog (high level)

Auto-updated on every commit when `.githooks` are enabled. Full history: `git log`.

<!-- CHANGELOG:BEGIN -->
| Version | Summary |
|---------|---------|
| **v0.0.070** | Reduce health bar UI churn, gate spawn logs, and harden combat dice flow |
| **v0.0.069** | Fix visit POIs vs spawns, combat engage, SpawnManager probe, DDOL HUD rebind |
| **v0.0.068** | Add Bootstrap scene flow, restore main gameplay markers, and fix dice/camera follow |
| **v0.0.067** | Defer heavy bootstrap to next frame and harden domain reload resets |
| **v0.0.066** | Polish GameServices hero registration, IsInitialized, and manager docs |
| **v0.0.065** | Add GameServices bootstrap, fix domain reload and chest travel polish |
| **v0.0.064** | Rebuild Steve movement and animation; fix treasure chest travel and path performance |
| **v0.0.063** | Add spawn chests, monster locomotion drivers, and POI spawn pipeline |
| **v0.0.062** | Add equipment loot chests, hero gear visuals, and locomotion animator fixes |
| **v0.0.061** | Add level-up roguelite reward popup with configurable stat offers |
| **v0.0.060** | Add title screen logo sprites under Assets/Sprites |
| **v0.0.059** | Add title loading screen with tap-to-start flow and editor setup |
| **v0.0.058** | Add LootManager to main play scene |
| **v0.0.057** | Add LootManager celebration coin drops and gold pickup |
| **v0.0.056** | Fix melee engage: single distance, Steve initiates, no enemy aggro |
| **v0.0.055** | Fix README sync in pre-commit to stop version-only commits |
| **v0.0.054** | Sync README version and changelog for v0.0.052 |
| **v0.0.053** | Sync README version and changelog for v0.0.052 |
| **v0.0.052** | Sync README version and changelog for v0.0.051 |
| **v0.0.051** | Clean up GlobalSettings and add combat log toggle |
| **v0.0.049** | Sync README version and changelog for v0.0.048 |
| **v0.0.048** | Sync README version and changelog for v0.0.047 |
| **v0.0.047** | Sync README version and changelog for v0.0.046 |
| **v0.0.046** | Sync README version and changelog for v0.0.045 |
| **v0.0.045** | Sync README version and changelog for v0.0.044 |
| **v0.0.044** | Sync README version and changelog for v0.0.043 |
| **v0.0.043** | Tighten melee spacing, stabilize combat movement, and fix skeleton sword attack |
| **v0.0.041** | Sync README version and changelog for v0.0.040 |
| **v0.0.040** | Sync README version and changelog for v0.0.039 |
| **v0.0.039** | Sync README version and changelog for v0.0.038 |
<!-- CHANGELOG:END -->

---

## Troubleshooting

| Issue | Check |
|-------|--------|
| Empty Hierarchy on `main.unity` | Unity 6 binary scene — reopen project, delete `Library/`, reimport; don’t swap scene files across old commits without care |
| Play opens wrong scene | **File → Build Settings** → `main.unity` at index 0 |
| Roll does nothing | Energy ≥ cost (`GlobalSettings.energyDepletionPerRoll`); Steve not already moving |
| No POI / no walk | Scene has `POINode` objects tagged `POI`; check `order` and Console for `HeroController` warnings |
| HP bar jitter / wrong layer | See [UI & health bars](docs/ARCHITECTURE.md#12-ui-and-health-bars) in architecture doc |

---

## License / assets

Third-party assets (Synty, GUI Pro-FantasyRPG, TextMesh Pro, etc.) remain under their respective licenses. Gameplay scripts in `Assets/Scripts/` are project-specific unless noted otherwise.
