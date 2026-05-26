# FatesRoll — Architecture

Technical reference for the **main** play scene (`Assets/Scenes/main.unity`). Build flow: `title.unity` (loading / tap to start) → `main.unity`.

---

## Table of contents

1. [System overview](#1-system-overview)
2. [Component map](#2-component-map)
3. [Singletons and scene objects](#3-singletons-and-scene-objects)
4. [Core game loop](#4-core-game-loop)
5. [Dice roll pipeline](#5-dice-roll-pipeline)
6. [Movement and POI routing](#6-movement-and-poi-routing)
7. [Combat flows](#7-combat-flows)
8. [Enemy AI](#8-enemy-ai)
9. [POI lifecycle](#9-poi-lifecycle)
10. [Editor vs runtime (POI setup)](#10-editor-vs-runtime-poi-setup)
11. [Stats and damage formulas](#11-stats-and-damage-formulas)
12. [UI and health bars](#12-ui-and-health-bars)
13. [Title scene and level-up rewards](#13-title-scene-and-level-up-rewards)
14. [Repo, version, and README automation](#14-repo-version-and-readme-automation)
15. [Script index](#15-script-index)

---

## 1. System overview

High-level view: input drives dice; dice drives movement or combat; POIs host enemies; managers coordinate targets and cleanup.

```mermaid
flowchart TB
    subgraph Input
        KB[Keyboard / Input System]
        UI[HUD Buttons / Hold Proxy]
    end

    subgraph Core["Gameplay core"]
        DS[DiceSpawner]
        HC[HeroController]
        EM[EnergyManager]
        LM[LevelManager]
        RL[RogueLiteManager]
        LM2[LootManager]
    end

    subgraph POI["Points of interest"]
        PM[POIManager]
        PN[POINode]
        EN[Enemy]
    end

    subgraph Data["Tuning & stats"]
        GS[GlobalSettings]
        PS[PlayerStats]
        ED[EnemyData SO]
    end

    subgraph World["Scene / world"]
        NM[NavMesh]
        SC[main.unity]
    end

    KB --> DS
    UI --> DS
    DS --> EM
    DS --> GS
    DS --> HC
    DS --> LM
    LM --> RL
    LM --> HC
    RL --> PS
    RL --> EM
    RL --> LM2
    DS --> EN

    HC --> PM
    HC --> PS
    HC --> GS
    HC --> NM

    PM --> PN
    ED -->|optional enemyData| PN
    PN --> EN

    EN --> HC
    EN --> PM
```

---

## 2. Component map

Who talks to whom (runtime dependencies).

```mermaid
graph LR
    DiceSpawner --> EnergyManager
    DiceSpawner --> GlobalSettings
    DiceSpawner --> HeroController
    DiceSpawner --> PlayerStats
    DiceSpawner --> Enemy
    DiceSpawner --> LevelManager
    DiceSpawner --> DieResult

    HeroController --> POIManager
    HeroController --> GlobalSettings
    HeroController --> PlayerStats
    HeroController --> Enemy

    POINode --> POIManager
    POINode --> Enemy

    Enemy --> HeroController
    Enemy --> POIManager
    Enemy --> FloatingText

    POIManager --> POINode

    LevelManager --> HeroController
    LevelManager --> GlobalSettings
    LevelManager --> RogueLiteManager

    RogueLiteManager --> PlayerStats
    RogueLiteManager --> EnergyManager
    RogueLiteManager --> LootManager

    EnergyManager --> GlobalSettings
    EnergyManager --> HeroController
    EnergyManager --> FloatingText

    QADashboard --> HeroController
    QADashboard --> DiceSpawner
    QADashboard --> POIManager
    QADashboard --> GlobalSettings
```

---

## 3. Singletons and scene objects

Gameplay services use **`GameServices`** (`DefaultExecutionOrder -10000`) plus **`GameServiceBehaviour<T>`** — no `FindAnyObjectByType` in `Instance` getters.

```mermaid
flowchart TD
    subgraph Bootstrap
        GSvc[GameServices Awake]
        GSvc --> Reg[Register managers from Inspector / children]
        Reg --> Dict[Type registry]
    end

    subgraph Access pattern
        A[Caller] --> B[GameServiceBehaviour T.Instance]
        B --> C{static _instance?}
        C -->|yes| D[Return _instance]
        C -->|no| E[GameServices.Get T]
    end

    subgraph Managers
        GS[GlobalSettings<br/>DontDestroyOnLoad]
        EM[EnergyManager]
        PM[POIManager]
        LM[LevelManager]
        RL[RogueLiteManager]
        LM2[LootManager]
    end

    Dict --> E
```

**Scene setup:** **FatesRoll → Setup → Add Game Services Bootstrap** groups managers under a `GameServices` object (optional **Persist Across Scenes**). Steve calls `GameServices.RegisterHero(this)` in `HeroController.Awake` (and again in `Start` if bootstrap order was late). Access Steve via `GameServices.Hero` or `GameServices.HeroController`. Check bootstrap with `GameServices.IsInitialized`. Strict lookup: `GameServices.Get<T>()` throws if missing; `TryGet<T>`, `HasInstance`, and `Foo.Instance` stay null-safe.

**Domain reload:** `GameServices` clears `Current` on `SubsystemRegistration` and `BeforeSceneLoad` (no stale GC handles after script recompile).

**Startup cost:** `GameServices` Awake only sets `Current` and DDOL; child discovery runs next frame (`deferHeavyBootstrap`). `POIManager` / `SpawnManager` scene scans (`FindObjectsByType`) also run after one frame; spawn init waits for POI init. Optional `targetFrameRateOnStart` on bootstrap for profiling.

| Component | Pattern | Notes |
|-----------|---------|--------|
| `GameServices` | Bootstrap registry | Inspector refs + optional `GetComponentInChildren` on bootstrap root only |
| `GlobalSettings` | `GameServiceBehaviour` + `DontDestroyOnLoad` | Movement, energy, combat delays, XP curve |
| `EnergyManager` | `GameServiceBehaviour` | Current energy, regen timer, HUD text |
| `POIManager` | `GameServiceBehaviour` | Registry of active `POINode`s |
| `LevelManager` | `GameServiceBehaviour` | XP bar, level-up celebration trigger |
| `RogueLiteManager` | `GameServiceBehaviour` | Post–level-up reward popup (A/B stat picks) |
| `LootManager` | `GameServiceBehaviour` | Coin drops; applies RogueLite coin bonus |
| `HeroController` | Scene component | One hero (Steve); `GameServices.Hero` |
| `DiceSpawner` | `GameServiceBehaviour` | Roll orchestration |

---

## 4. Core game loop

One turn from the player’s perspective.

```mermaid
sequenceDiagram
    participant P as Player
    participant DS as DiceSpawner
    participant EM as EnergyManager
    participant HC as HeroController
    participant PM as POIManager
    participant EN as Enemy
    participant LM as LevelManager

    P->>DS: Roll (input / auto-roll)
    DS->>DS: CanRoll? (not rolling, not moving, has energy)
    DS->>EM: Deplete(energyDepletionPerRoll)
    DS->>DS: Spawn 2× d6, wait settle, sum total

    alt Not in combat
        DS->>HC: MoveSteps(total)
        HC->>PM: GetPOIByOrder(nextPOIOrder)
        HC->>HC: NavMesh partial path + leftoverDiceValue
        HC->>EN: Proximity less than 3m
        HC->>HC: EnterCombat + InitialAttackCoroutine
    else In combat
        DS->>EN: Combat roll damage + anim timing
        EN-->>HC: PerformAttack if alive
    end

    DS->>LM: AddXP(total) unless early exit on kill
    LM->>HC: PlayLevelUpCelebration (if leveled)
    HC->>RL: RunPostCelebrationRewards (after anim)
    Note over RL: Popup: pick Strength / Agility / Vitality / Luck / Energy / Coins
```

---

## 5. Dice roll pipeline

Detailed `RollRoutine` coroutine.

```mermaid
flowchart TD
    Start([RollDice / OnRoll]) --> Can{CanRoll?}
    Can -->|no| Stop([Return])
    Can -->|yes| Lock[isRolling = true]

    Lock --> Energy[EnergyManager.Deplete]
    Energy --> Throw{hero.InCombat?}
    Throw -->|no| Anim[Animator Throw + 0.2s wait]
    Throw -->|yes| SkipAnim[Skip throw anim]
    Anim --> Cleanup
    SkipAnim --> Cleanup[Destroy old DieResult objects]

    Cleanup --> Spawn[Spawn 2× d6Prefab at chest height]
    Spawn --> Settle[Wait until DieResult settled or 3s timeout]
    Settle --> Buffer[+1.0s visual buffer]
    Buffer --> Sum[LastRoll = sum of dice]

    Sum --> Branch{hero.InCombat?}
    Branch -->|yes| CombatPath[Face → wind-up → Attack trigger → TakeDamage → retaliate]
    Branch -->|no| Move[hero.MoveSteps total]

    CombatPath --> Dead{enemy.isDead?}
    Dead -->|yes| XP2[AddXP total×2 + Victory + exit]
    Dead -->|no| Delay[combatReactionDelay]
    Delay --> Retaliate[enemy.PerformAttack]

    Move --> XP[LevelManager.AddXP total]
    CombatPath --> XP
    Retaliate --> XP
    XP2 --> Finally
    XP --> LevelUp{levels gained?}
    LevelUp -->|yes| WaitCelebration[wait while hero.IsCelebrating]
    LevelUp -->|no| Finally
    WaitCelebration --> Finally[finally: isRolling = false]
```

**`CanRoll()` gates**

| Check | Blocks roll when |
|--------|-------------------|
| `isRolling` | Already in `RollRoutine` |
| `hero.IsMoving` | NavMesh walk in progress |
| Energy | `currentEnergy < energyDepletionPerRoll` |

*Note:* `InCombat` does **not** block rolling (combat rolls are intentional).

---

## 6. Movement and POI routing

Ordered POI visits (`POINode.order`) replaced random-only targeting in v0.0.025+.

```mermaid
flowchart LR
    subgraph Selection
        O[nextPOIOrder]
        PM[POIManager.GetPOIByOrder]
        O --> PM
        PM --> T[currentTarget POI root]
    end

    subgraph Path
        T --> Calc[NavMesh.CalculatePath]
        Calc --> Partial[Walk min roll distance along path]
        Partial --> Left[leftoverDiceValue if path shorter than roll]
    end

    subgraph Arrival
        Partial --> Prox{dist to POI less than 3m?}
        Prox -->|yes| Inc[nextPOIOrder++]
        Inc --> Fight{Has Enemy?}
        Fight -->|yes| Combat[EnterCombat + arrival attack]
        Fight -->|no| Resolve[POIManager.ResolvePOI]
    end
```

**`GetPOIByOrder(order)` logic**

1. Return first POI with `poi.order == order`.
2. Else return POI with smallest `poi.order > order`.
3. Else `null` (no walk).

```mermaid
stateDiagram-v2
    [*] --> Idle: scene load
    Idle --> Walking: MoveSteps SetDestination
    Walking --> Idle: destination reached OR proximity finalize
    Walking --> InCombat: Enemy within 3m
    InCombat --> Idle: ExitCombat on enemy Die
    Idle --> InCombat: dice combat branch while engaged
```

---

## 7. Combat flows

Two entry paths share similar animation timing but different damage formulas.

### 7.1 Arrival combat (first hit)

Triggered from `HeroController` when Steve reaches POI range.

```mermaid
sequenceDiagram
    participant HC as HeroController
    participant EN as Enemy
    participant GS as GlobalSettings
    participant PS as PlayerStats

    HC->>EN: FaceTarget (instant)
    HC->>HC: Wait 0.45s
    Note over HC: damage = leftoverSteps × leftoverStepDamageMultiplier + AttackDamage×0.5
    HC->>PS: Crit roll
    HC->>HC: CrossFade battle stance → Attack trigger
    HC->>EN: TakeDamage(finalDamage)
    alt enemy dead
        HC->>HC: VictoryFlourish
    else alive
        HC->>HC: Wait combatReactionDelay
        EN->>HC: PerformAttack
    end
```

### 7.2 In-combat dice attack

Triggered from `DiceSpawner` when `hero.InCombat`.

```mermaid
sequenceDiagram
    participant DS as DiceSpawner
    participant HC as HeroController
    participant EN as Enemy
    participant PS as PlayerStats

    Note over DS: damage = AttackDamage × (roll / 7)
    DS->>PS: Crit check
    DS->>HC: FaceTarget instant
    DS->>EN: FaceTarget instant
    DS->>DS: Wait 0.25s + anim wind-up 0.6s + Attack 0.35s
    DS->>EN: TakeDamage
    alt dead
        DS->>HC: VictoryFlourish
        DS->>DS: AddXP roll×2, yield break
    else
        DS->>DS: combatReactionDelay
        EN->>HC: PerformAttack
    end
```

### 7.3 Combat state (hero + enemy)

```mermaid
stateDiagram-v2
    [*] --> Exploring
    Exploring --> Walking: roll → MoveSteps
    Walking --> Engaged: proximity + EnterCombat
    Engaged --> Engaged: combat rolls
    Engaged --> Exploring: Enemy.Die → ExitCombat → ResolvePOI
    Walking --> Exploring: non-enemy POI ResolvePOI
```

---

## 8. Enemy AI

`Enemy.HandleAI()` each frame while alive.

```mermaid
stateDiagram-v2
    [*] --> Patrol
    Patrol --> Taunt: patrolPointsBeforeTaunt reached
    Taunt --> Patrol: TauntRoutine done
    Patrol --> Chase: hero near spawn OR hero within 6m
    Chase --> Engaged: hero.currentEnemy == this
    Engaged --> Engaged: agent stopped, FaceTarget hero
    Chase --> Patrol: hero far
    Engaged --> Dead: HP <= 0
    Patrol --> Dead
    Chase --> Dead
    Dead --> [*]: Die → DelayedResolve POI
```

| State | Behavior |
|--------|----------|
| **Patrol** | Random NavMesh points within `patrolRadius` of `spawnPosition` |
| **Taunt** | `Taunting` trigger, ~2.2s, sets `isAttacking` |
| **Chase** | `SetDestination(hero)`, `chaseSpeed` |
| **Engaged** | Stop agent, slerp face toward Steve |

---

## 9. POI lifecycle

```mermaid
flowchart TD
    subgraph Editor
        E[POINodeEditor] --> Inst[Instantiate monster prefab child]
        Inst --> HB[Instantiate GUI Pro slider prefab]
        HB --> Comp[Add Enemy + NavMeshAgent on root]
    end

    subgraph Runtime Start
        PN[POINode.Start] --> Reg[POIManager.RegisterPOI]
        Reg --> Init[Enemy.Initialize full HP + health UI]
    end

    subgraph Encounter
        Init --> Wait[Active in scene]
        Wait --> Fight[Combat]
    end

    subgraph End
        Fight --> Die[Enemy.Die]
        Die --> Exit[hero.ExitCombat]
        Exit --> Delay[Wait 2s]
        Delay --> Res[POIManager.ResolvePOI]
        Res --> Unreg[UnregisterPOI]
        Unreg --> Destroy[Destroy POI root]
    end

    Editor -.->|visuals saved in scene| Wait
```

**`POINode` root structure (typical)**

```
POI_Orc (POINode, Enemy, NavMeshAgent, tag=POI)
├── OrcPBRDefault (animator mesh child)
└── Slider_Border_Tapered_02_Green (world-space Canvas)
    ├── Bg / Border / Fill Area / Fill
```

---

## 10. Editor vs runtime (POI setup)

```mermaid
flowchart TB
    subgraph Editor only
        INS[Inspector: type + order]
        INS --> REF[Force Refresh / type change]
        REF --> POINodeEditor.UpdateVisuals
        POINodeEditor --> DestroyChildren[Clear all children]
        DestroyChildren --> Prefab[Load Orc/Skeleton/Slime prefab]
        Prefab --> HealthPrefab[Slider_Border_Tapered_02_Green]
    end

    subgraph Play mode
        START[POINode.Start] --> REG[RegisterPOI]
        REG --> INIT[Enemy.Initialize]
        INIT --> AI[Enemy Update AI]
    end

    POINodeEditor -->|saves to| SCENE[main.unity]
    SCENE --> START
```

| Concern | Editor | Play mode |
|---------|--------|-----------|
| Monster mesh | `POINodeEditor` spawns prefab | Already in scene |
| Health bar | Editor positions at +2.8y | `LateUpdate` pins at +3.0y world, billboards |
| Stats | `Enemy` inspector / `OnValidate` | `Initialize()` sets HP = maxHP |

---

## 11. Stats and damage formulas

Shared RPG formulas on **hero** (`PlayerStats`) and **enemy** (`Enemy`).

| Derived stat | Formula |
|--------------|---------|
| maxHP | `vitality × 10 + 100` |
| attackDamage | `strength × 4 + 20` |
| attackSpeed | `1.0 + agility × 0.03` |
| critChance (%) | `luck × 0.8` |
| critDamage (%) | `50 + luck × 1.5` |
| dodgeChance (%) | `agility × 0.6` |

**Damage applications**

| Source | Formula |
|--------|---------|
| Arrival hit | `AttackDamage × (lastRoll / 7)` (+ crit) via `HeroController.CalculateRollDamage` |
| Combat roll | Same as arrival hit while `InCombat` |
| Enemy hit | `Enemy.attackDamage` (+ crit); hero dodges via `PlayerStats` |

**Where to tune**

| What | Component |
|------|-----------|
| Steve HP, damage, crit, dodge | `PlayerStats` on Steve |
| Enemy HP, damage, patrol | `Enemy` on each monster (or `EnemyData` prefab) |
| Melee engage distance, combat delays | `GlobalSettings.meleeEngageDistance` |
| Combat console spam | `GlobalSettings.combatLogEnabled` |
| Dice / movement / XP logs | `GlobalSettings.verboseGameplayLogs` (off by default) |

---

## 12. UI and health bars

```mermaid
flowchart LR
    subgraph Hero HUD
        FIND[GameObject.Find MainUI paths]
        FIND --> SL1[Slider_Bottom PlayerStats HP]
        EM --> SL2[Energy text + regen timer]
    end

    subgraph Enemy HUD
        CAN[Child World Space Canvas]
        CAN --> BAR[Slider Bg / Border / Fill]
        EN --> VIS[SetHealthBarVisible on combat / damage]
        EN --> LATE[LateUpdate: world pos + billboard]
    end

    subgraph Floating
        FT[FloatingText TMP world]
        FT --> DMG[Damage numbers]
        FT --> NRG[Energy depleted text]
    end
```

---

## 13. Title scene and level-up rewards

### 13.1 Title → main

| Scene | Role |
|-------|------|
| `Assets/Scenes/title.unity` | Build index **0** — loading bar, tap to start, preloads `main` |
| `Assets/Scenes/main.unity` | Build index **1** — gameplay |

`TitleFlowController` drives loading UI, async preload (`allowSceneActivation = false`), then activates `main` on tap. Editor: **FatesRoll → Scenes** menus (`Setup Title Loading Scene`, play-mode start from title).

### 13.2 Level-up roguelite popup

After Steve’s level-up animation (`HeroController.LevelUpCelebrationSeconds`, default **2.7s**), `RogueLiteManager` shows a centered `Popup_01_Basic_Demo` panel (dimmed fullscreen overlay) with **two random different** upgrade buttons.

```mermaid
sequenceDiagram
    participant LM as LevelManager
    participant HC as HeroController
    participant RL as RogueLiteManager
    participant PS as PlayerStats
    participant EM as EnergyManager
    participant Loot as LootManager

    LM->>LM: LevelUp → EnqueueLevelUp(level)
    LM->>HC: PlayLevelUpCelebration
    HC->>HC: Wait celebration duration
    HC->>RL: RunPostCelebrationRewards
    RL->>RL: timeScale = 0, show popup
    Note over RL: Title + flavor text; 2 colored buttons
    RL->>PS: Apply Strength / Agility / Vitality / Luck
    RL->>EM: energyRegenTimeReduction += pick
    RL->>Loot: bonusCoinsPerEnemyKill += pick
    RL->>RL: Hide popup, restore timeScale
```

**Per-stat Inspector fields** (`RogueLiteManager`, each of six stats):

| Field | Meaning |
|-------|---------|
| Upgrade Amount | Flat bonus (e.g. +1 STR, −1s regen interval, +1 coin per kill) |
| Offer Chance % | Weight in random pool (20 on all six ≈ equal odds) |
| Upgrade Scaler | Repeat picks: `amount × scaler^timesAlreadyChosen` (default **1** = flat) |
| Button Color | `Button_Rectangle_01_Convex_*` prefab color |

**Popup typography** (same component): title/body/button font sizes, panel scale, choice button scale.

**Setup:** **FatesRoll → Roguelite → Add RogueLiteManager To Scene** (under `Managers`), save `main.unity`.

**Energy regen:** `EnergyManager` uses `RogueLiteManager.GetEffectiveEnergyRegenInterval()` (`base interval − total reduction`).

**Coins:** `LootManager` adds `RogueLiteManager.BonusCoinsPerEnemyKill` to per-enemy drop count.

---

## 14. Repo, version, and README automation

```mermaid
flowchart LR
    DEV[Developer commit] --> WRAP[git-commit.ps1 optional]
    WRAP --> PRE[pre-commit hook]
    PRE --> BUMP[bump-version.ps1]
    BUMP --> VER[VERSION + ProjectSettings.bundleVersion]
    PRE --> STG1[git add version files]
    DEV --> MSG[commit-msg hook]
    MSG --> README[update-readme.ps1]
    README --> CHG[README version line + changelog row]
    MSG --> STG2[git add README.md]
```

Files:

| Path | Role |
|------|------|
| `VERSION` | `v0.0.XXX` tag string |
| `scripts/bump-version.ps1` | Increment patch |
| `scripts/update-readme.ps1` | Sync README from commit subject |
| `scripts/git-commit.ps1` | `git -c core.hooksPath=.githooks commit` |
| `.githooks/pre-commit` | Version bump |
| `.githooks/commit-msg` | README update |

---

## 15. Script index

| Script | Responsibility |
|--------|----------------|
| `GameServices` | Early bootstrap registry; `Hero`, `Get<T>()`; Inspector wiring |
| `GameServiceBehaviour<T>` | Base for managers — registers in Awake, no scene search in getters |
| `DiceSpawner` | Input, roll physics, energy, branch move vs combat |
| `DieResult` | Dice face value + settled detection |
| `HeroController` | NavMesh move, POI target, arrival combat, hero HP HUD |
| `PlayerStats` | Hero primary/derived stats, dodge |
| `Enemy` | Enemy stats, AI, HP bar, damage, death |
| `EnemyData` | ScriptableObject; assign on `POINode.enemyData` to override enemy stats at Start |
| `POINode` | POI tag, type, visit `order`, register/init enemy |
| `POIManager` | POI list, resolve, nearest/random/order query |
| `POINodeEditor` | Editor prefab + health bar setup |
| `GlobalSettings` | Tunable singleton |
| `EnergyManager` | Energy pool + UI |
| `LevelManager` | XP + level UI; queues `RogueLiteManager` on level-up |
| `RogueLiteManager` | Level-up reward popup, stat upgrades, energy/coin modifiers |
| `RogueLiteStatConfig` | Serializable per-stat tuning (amount, chance %, scaler, button color) |
| `TitleFlowController` | Title scene: loading → tap → load `main` |
| `TitleSceneSetup` | Editor: create/consolidate title scene, build order, play-mode start |
| `LootManager` | Enemy coin celebration drops + gold HUD |
| `FloatingText` | World TMP popups |
| `QADashboard` | Debug HUD for roll/distance |
| `QAVersionDisplay` / `GitVersionProvider` | Build version overlay |
| `CameraFollow` | Camera follow Steve |
| `UIButtonHoldProxy` / `UIPressedEffect` | UI feedback |

---

## Related docs

- [README.md](../README.md) — setup, play instructions, changelog, troubleshooting
- [VERSION](../VERSION) — current patch label
