# Contributing to VILL4GE

Thank you for your interest in contributing to this RE4 tribute project!

## Ground Rules

- **No copyrighted assets**: Do not submit models, textures, audio, music, or voice lines from Resident Evil 4 or any other commercial game.
- **No AI-generated copyrighted content**: Ensure all assets are original or properly licensed (CC0 / MIT / Apache 2.0 or similar).
- **Code quality**: Follow the existing C# style in the project (see below).
- **Scope**: Keep features RE4-inspired but implemented generically.

## How to Contribute

### 1. Fork & Clone

```bash
git clone https://github.com/YOUR_USERNAME/re4_unity_projectf.git
cd re4_unity_projectf
```

### 2. Create a Branch

Use a descriptive branch name:

```bash
git checkout -b feature/save-system
git checkout -b fix/enemy-patrol-stuck
git checkout -b docs/update-readme
```

### 3. Make Your Changes

- Scripts go in `Assets/Scripts/<Category>/`
- Follow the existing code style (see below)
- Keep each script focused on one responsibility
- Do not commit Unity Library/, Temp/, or .csproj/.sln files

### 4. Test

1. Open the project in Unity 2021.3 LTS or later
2. Ensure the project compiles with no errors
3. Hit **Play** and verify your changes work

### 5. Submit a Pull Request

- Write a clear PR title (e.g., `feat: add patrol AI to Ganado enemies`)
- Include a short description of what changed and why
- Reference any related issues

---

## Code Style

```csharp
// Class-level fields: camelCase
float staggerTimer;
bool isDead;

// Public fields and properties: PascalCase
public float MaxHp;
public bool IsStaggered => staggerTimer > 0;

// Methods: PascalCase
void BuildBody() { ... }
public void TakeDamage(float dmg) { ... }

// Use regions sparingly — only for large files with clear sections
// ====================================================================
// SECTION NAME
// ====================================================================
```

- Use `var` when the type is obvious from the right-hand side
- Prefer composition over inheritance
- Each public method should have a `/// <summary>` XML doc comment

---

## Folder Structure

```
Assets/
  Scripts/
    Audio/      AudioManager.cs
    Core/       InputManager.cs, RE4Core.asmdef
    Enemy/      Enemy.cs, EnemyPatrol.cs, EnemySpawner.cs
    Gameplay/   CheckpointSystem.cs, GrenadeProjectile.cs, Pickup.cs
    Save/       SaveSystem.cs
    UI/         (UI-specific scripts)
    AttacheCase.cs
    GameManager.cs
    GameUI.cs
    Player.cs
  Scenes/       (scene files)
  Prefabs/      (reusable prefabs)
  Art/          (materials, textures)
  Audio/        (sound clips)
  UI/           (sprites, fonts)
  Settings/     (quality settings, render settings)
```

---

## Commit Message Format

```
type: short description

Examples:
feat: add checkpoint save system
fix: enemy patrol stuck detection
docs: update UNITY_SETUP with asmdef guide
refactor: move AudioManager to Scripts/Audio/
```

Types: `feat`, `fix`, `docs`, `refactor`, `perf`, `test`, `chore`

---

## Reporting Bugs

Open an issue with:
- Unity version
- Steps to reproduce
- Expected vs actual behavior
- Any console errors (copy-paste, not screenshots)

---

*This project is a tribute — keep it respectful and fun!*
