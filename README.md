# FatesRoll

Unity 6 dice-driven exploration and combat prototype. Roll 2× d6, spend energy, walk Steve along the NavMesh toward POIs, and fight enemies when you reach them.

**Current version:** `v0.0.017` (see [`VERSION`](VERSION) and Unity **Player Settings → Version**).

| | |
|---|---|
| **Play scene** | `Assets/Scenes/main.unity` |
| **Unity** | 6000.x (binary-serialized scene) |
| **Repo** | https://github.com/czyzem-stack/fatesRoll |

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
Roll (energy cost) → dice settle → XP → walk N meters along path to POI → combat at POI → POI resolved
```

| System | Script(s) | Notes |
|--------|-----------|--------|
| Dice | `DiceSpawner`, `DieResult` | Throw anim, settle, read values |
| Movement | `HeroController` | NavMesh path; distance = `roll × stepsPerDiceValue × metersPerStep` |
| Energy | `EnergyManager` | Depletes on roll; regen timer; floating “+N energy” text |
| POIs | `POINode`, `POIManager` | Tagged `POI`; types drive visuals / enemy |
| Combat | `HeroController`, `EnemyCombatant` | Basic melee flow; HP bars; death anim params |
| XP / level | `LevelManager` | XP from roll total; level-up animation |
| Tuning | `GlobalSettings` | Singleton; movement, energy, combat, XP |
| QA | `QADashboard`, `QAVersionDisplay` | Roll/distance debug; build version + git hash |

---

## Project layout

```
Assets/
  Scenes/main.unity          # Main game scene (use this, not SampleScene)
  Scripts/                   # Gameplay C# (no asmdef)
  Heroes/                    # Steve anims / controllers
  Dice/                      # Dice prefabs
VERSION                      # Release label: v0.0.XXX
scripts/bump-version.ps1     # Bump patch version
scripts/update-readme.ps1    # Refresh README (run via commit-msg hook)
.githooks/                   # pre-commit: version; commit-msg: README
```

---

## Versioning

Patch versions use **`v0.0.XXX`** in `VERSION` and **`0.0.XXX`** in Unity.

**On each commit (recommended):** enable hooks once (bumps `VERSION`, updates this README changelog):

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
| **v0.0.017** | Solid state: scene + `IsDead` on hero/orc animators |
| **v0.0.016** | Basic combat working; hero & orc animation fixes |
| **v0.0.015** | Floating energy text, UI press effect, sticky POI target |
| **v0.0.014** | DiceRoll animation; combat prep |
| **v0.0.013** | Pre-combat checkpoint; version bump script fix |
| **v0.0.011+** | Energy burn on roll, regen, QA version display |
| **v0.0.001** | Project version baseline |
<!-- CHANGELOG:END -->

---

## Troubleshooting

| Issue | Check |
|-------|--------|
| Empty Hierarchy on `main.unity` | Unity 6 binary scene — reopen project, delete `Library/`, reimport; don’t swap scene files across old commits without care |
| Play opens wrong scene | **File → Build Settings** → `main.unity` at index 0 |
| Roll does nothing | Energy ≥ cost (`GlobalSettings.energyDepletionPerRoll`); Steve not already moving |
| No POI / no walk | Scene has `POINode` objects tagged `POI`; see Console for `HeroController` warnings |

---

## License / assets

Third-party assets (Synty, GUI Pro-FantasyRPG, TextMesh Pro, etc.) remain under their respective licenses. Gameplay scripts in `Assets/Scripts/` are project-specific unless noted otherwise.
