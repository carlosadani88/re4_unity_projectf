# VILL4GE — A Resident Evil 4 Tribute (Unity)

![Unity](https://img.shields.io/badge/Unity-2021.3%2B-blue?logo=unity)
![C#](https://img.shields.io/badge/C%23-Scripts-239120?logo=csharp)
![License](https://img.shields.io/badge/License-MIT-green)
![Status](https://img.shields.io/badge/Status-Active-yellow)

> Third-person survival horror inspired by **Resident Evil 4**, built in Unity with procedural C#.
> Hit **Play** and everything generates at runtime — no external assets needed.

---

## Quick Start

### Requirements
- **Unity 2021.3 LTS** or newer (recommended: **2022.3 LTS**)
- **3D (Built-in Render Pipeline)** template
- No additional packages required

### Setup (5 minutes)

1. **Unity Hub** → **New Project** → **3D (Core)**
2. Copy the `Assets/` folder into your new project
3. In SampleScene, **delete** `Main Camera` and `Directional Light`
4. **GameObject → Create Empty** → rename it `GameBootstrap`
5. **Add Component → GameManager**
6. Press **Play** — village, enemies, UI and systems spawn automatically

---

## Controls

| Key / Button | Action |
|---|---|
| WASD | Move (slower while aiming) |
| Mouse | Look |
| RMB (hold) | **Aim over-the-shoulder** |
| LMB | **Shoot (aiming) / Knife (hip)** |
| Shift | Sprint |
| R | Reload |
| Scroll / 1-5 | Switch weapon |
| F | **Roundhouse kick** (enemy staggered) |
| G | Throw grenade |
| H | Use herb (+40 HP) |
| V | Knife |
| E | Talk to Merchant |
| Tab | Open **Attache Case** (inventory) |
| Escape | Pause menu |
| F5 | **Quick Save** |
| F9 | **Quick Load** |

---

## Feature List

### Gameplay (RE4-Faithful)
- **Over-the-shoulder camera** with dynamic offset, FOV 70 to 55 when aiming
- **Red laser sight** on RMB hold (LineRenderer + emissive dot)
- **5 weapons**: Handgun, Shotgun, Rifle, TMP, Rocket Launcher
- **Knife melee**, **roundhouse kick** on stagger, **grenades**
- **Healing herbs**, sprint with headbob
- **Attache Case** grid inventory with item rotation

### Weapons

| Weapon | Damage | Mag | Reserve |
|---|---|---|---|
| Handgun | 18 | 12 | 60 |
| Shotgun | 90 | 6 | 24 |
| Rifle | 50 | 10 | 30 |
| TMP | 10 | 50 | 200 |
| Rocket Launcher | 350 | 1 | 3 |

### Enemy AI (Ganados)

| Type | HP | Speed | Notes |
|---|---|---|---|
| Villager | 60 | 2.2 | Sickle |
| Pitchfork | 50 | 2.8 | Faster |
| Heavy | 140 | 1.6 | High damage |
| Dr. Salvador | 350 | 3.2 | Chainsaw, no stagger |

- **Headshot** = 3x damage + head explodes with particles
- **Stagger** on hit, contextual kick prompt
- **Patrol AI** (EnemyPatrol.cs): waypoint/random wander + hear/sight detection
- **Wave system** + configurable **EnemySpawner** per encounter area

### Core Systems

| System | Script | Description |
|---|---|---|
| InputManager | Core/InputManager.cs | Centralized input wrapper |
| AudioManager | Audio/AudioManager.cs | 16-source SFX pool + music layers |
| SaveSystem | Save/SaveSystem.cs | JSON save/load (5 slots), F5/F9 shortcuts |
| CheckpointSystem | Gameplay/CheckpointSystem.cs | Trigger-based auto-save with visual indicator |
| EnemySpawner | Enemy/EnemySpawner.cs | Designer-friendly per-encounter wave configs |
| EnemyPatrol | Enemy/EnemyPatrol.cs | Waypoint patrol + hear/sight cone detection |
| Attache Case | AttacheCase.cs | Grid inventory (10x6, expandable to 14x8) |
| Merchant | GameUI.cs | Buy/sell weapons, ammo, upgrades with pesetas |

### UI / HUD
- LIFE bar (bottom-left), ammo counter + weapon name (bottom-right)
- Pesetas display, wave counter, kill count
- Wave intro banner, YOU ARE DEAD screen
- Damage vignette, kick prompt overlay
- Pause menu, checkpoint + area-cleared messages

### Procedural Village (70x70)
- Church with bell tower, cemetery, central bonfire (dynamic flicker)
- 6 houses, fences, carts, barrels, trees, torches, wells
- Exponential fog, dim trilight ambient, rain (3000 particles)

---

## Project Structure

```
re4_unity_projectf/
├── Assets/
│   ├── Scripts/
│   │   ├── Core/          InputManager.cs, RE4Core.asmdef
│   │   ├── Audio/         AudioManager.cs
│   │   ├── Enemy/         Enemy.cs, EnemyPatrol.cs, EnemySpawner.cs
│   │   ├── Gameplay/      CheckpointSystem.cs, GrenadeProjectile.cs, Pickup.cs
│   │   ├── Save/          SaveSystem.cs
│   │   ├── VILL4GE.asmdef (root assembly, references RE4Core)
│   │   ├── GameManager.cs (village, waves, lighting, materials)
│   │   ├── Player.cs      (OTS camera, 5 weapons, kick, rain)
│   │   ├── GameUI.cs      (HUD, merchant, menus)
│   │   └── AttacheCase.cs (grid inventory)
│   ├── Scenes/
│   ├── Prefabs/
│   ├── Art/   (Materials/, Textures/)
│   ├── Audio/ (Music/, SFX/)
│   ├── UI/    (Fonts/, Sprites/)
│   ├── Settings/
│   └── Resources/
├── Modelos/           3D model files (Git LFS recommended)
├── projeto_antigo/    Old Python/Pygame prototypes
├── README.md
├── UNITY_SETUP.md     Detailed setup guide
├── CONTRIBUTING.md
├── LICENSE            MIT
├── .gitignore
└── .gitattributes     Git LFS + line endings
```

---

## Save System

Saves player HP, weapons/ammo, pesetas, herbs, wave, kills, position.
Files stored in `Application.persistentDataPath/saves/` (not committed to git).

| Key | Action |
|---|---|
| F5 | Quick Save (slot 0) |
| F9 | Quick Load (slot 0) |

Add a **CheckpointSystem** component on any trigger collider to auto-save on player entry.

---

## Assembly Definitions

| Assembly | Path | Purpose |
|---|---|---|
| `RE4Core` | `Scripts/Core/` | InputManager (no external deps) |
| `VILL4GE` | `Scripts/` | All gameplay scripts (auto-references RE4Core) |

Assembly definitions speed up compile times by only recompiling changed assemblies.

---

## Roadmap

- [ ] Import 3D models (replace procedural primitives)
- [ ] Audio clips (gunshots, footsteps, chainsaw, ambient)
- [ ] Animator Controller with walk/aim/shoot blend trees
- [ ] Boss fight placeholder (El Gigante)
- [ ] Door / key / puzzle system
- [ ] Save slot selection UI
- [ ] Object pooling for enemies and projectiles
- [ ] LOD system for performance
- [ ] Controller / mobile input support

---

## Asset Licensing

All assets are **procedurally generated via C#** (Unity primitives, dynamic materials, lights, particles).
No copyrighted Resident Evil 4 assets are included.
This is a non-commercial fan tribute for educational purposes.

See [LICENSE](LICENSE) for full license terms.

---

*Inspired by Resident Evil 4 (Capcom, 2005 / 2023). Not affiliated with or endorsed by Capcom.*
