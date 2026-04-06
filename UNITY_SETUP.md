# VILL4GE — Setup no Unity (RE4 Tribute Edition)

## Pré-requisitos
- **Unity 2021.3 LTS** ou superior (recomendado: 2022.3 LTS)
- Template **3D (Built-in Render Pipeline)**

## Passo a passo

### 1. Criar projeto
1. **Unity Hub** → **New Project** → template **3D (Core)** → nome `VILLAG`

### 2. Importar scripts
1. Copie toda a pasta `Assets/` para `<SeuProjeto>/Assets/`
   - Scripts em `Assets/Scripts/` (subdiretorios: Core, Audio, Enemy, Gameplay, Save, UI)
   - Pastas vazias: Scenes, Prefabs, Art, Audio, UI, Settings, Resources
2. Unity compila automaticamente

### 3. Configurar a cena
1. Na SampleScene, **delete** `Main Camera` e `Directional Light`
2. **GameObject → Create Empty** → renomear `GameBootstrap`
3. **Add Component** → `GameManager`
4. **Play!** — tudo é gerado proceduralmente

### 4. Controles (estilo RE4)

| Tecla | Ação |
|---|---|
| WASD | Mover |
| Mouse | Olhar |
| RMB (segurar) | Mirar over-the-shoulder |
| LMB | Atirar (mirando) / Faca (quadril) |
| Shift | Sprint |
| R | Recarregar |
| Scroll / 1-5 | Trocar arma |
| F | Chute (inimigo staggered) |
| G | Granada |
| H | Erva (+40 HP) |
| V | Faca |
| E | Merchant |
| Tab | Attache Case (inventario) |
| Escape | Pause |
| F5 | Quick Save |
| F9 | Quick Load |

### 5. Novos sistemas (v2)

#### InputManager (`Scripts/Core/InputManager.cs`)
Abstrai todos os inputs em propriedades simples. Para trocar para o novo Input System, modifique apenas este arquivo.

```csharp
// Exemplos de uso:
InputManager.I.AimHeld      // bool — RMB segurado
InputManager.I.FireDown     // bool — LMB pressionado neste frame
InputManager.I.MouseSensitivity  // float — configuravel no Inspector
```

#### AudioManager (`Scripts/Audio/AudioManager.cs`)
Pool de 16 AudioSources para SFX + fonte dedicada para musica.

```csharp
AudioManager.I.PlaySFX(AudioManager.SFX.Gunshot);
AudioManager.I.PlayMusic(AudioManager.Music.Combat);
AudioManager.I.SetVolumes(master: 1f, sfx: 0.8f, music: 0.6f);
```

Para adicionar clips: selecione o `AudioManager` na cena e arraste
AudioClips para os campos `Sfx Entries` / `Music Entries` no Inspector.

#### SaveSystem (`Scripts/Save/SaveSystem.cs`)
Salva HP, armas/municao, pesetas, ervas, wave, kills e posicao em JSON.

- **F5** = Quick Save (slot 0)
- **F9** = Quick Load (slot 0)
- Arquivos em `Application.persistentDataPath/saves/` (nunca commitados)

```csharp
SaveSystem.I.Save(slot: 1);   // salvar no slot 1
SaveSystem.I.Load(slot: 1);   // carregar slot 1
bool exists = SaveSystem.I.SlotExists(2);
```

#### CheckpointSystem (`Scripts/Gameplay/CheckpointSystem.cs`)
Auto-save quando o jogador entra num trigger.

Setup:
1. Crie um GameObject com BoxCollider (IsTrigger = true)
2. Adicione o componente `CheckpointSystem`
3. Configure `checkpointIndex` (unico por checkpoint) e `saveSlot`

#### EnemySpawner (`Scripts/Enemy/EnemySpawner.cs`)
Spawner por encontro com configuracao de waves no Inspector.

```
Waves[0]: villagers=3, pitchforks=1, startDelay=0, spawnInterval=0.5
Waves[1]: villagers=2, heavies=1, startDelay=2, spawnInterval=0.8
Waves[2]: chainsaws=1, heavies=2, startDelay=1, spawnInterval=1.0
```

#### EnemyPatrol (`Scripts/Enemy/EnemyPatrol.cs`)
Patrulha com waypoints ou aleatoria. Deteccao de som (circulo) e visao (cone).

```
Mode = Waypoints
Waypoints = [wp1, wp2, wp3]
HearRadius = 6
SightRadius = 14
SightAngleHalf = 55
```

### 6. Assembly Definitions (asmdef)

| Assembly | Pasta | Propósito |
|---|---|---|
| RE4Core | Scripts/Core/ | Input e servicos centrais |
| RE4Gameplay | Scripts/Gameplay/ | Sistemas de gameplay |
| RE4Enemy | Scripts/Enemy/ | IA e spawning de inimigos |

Asmdef aceleram compilacao: apenas os assemblies modificados recompilam.

### 7. Estrutura de scripts

| Script | Responsabilidade |
|---|---|
| `GameManager.cs` | Vila procedural, church, fogueira, cenario, waves |
| `Player.cs` | OTS camera, laser, 5 armas, faca, kick, chuva |
| `Enemy.cs` | 4 tipos Ganado, headshot, stagger, grab |
| `GameUI.cs` | HUD, merchant, wave intro, death screen, checkpoints |
| `AttacheCase.cs` | Maleta grid (inventario RE4) |
| `Pickup.cs` | Drops (erva/municao/pesetas) |
| `GrenadeProjectile.cs` | Timer + explosao + luz dinamica |
| `Core/InputManager.cs` | Abstracao de input |
| `Audio/AudioManager.cs` | Pool de SFX + musica |
| `Save/SaveSystem.cs` | Save/Load JSON |
| `Gameplay/CheckpointSystem.cs` | Checkpoint trigger + auto-save |
| `Enemy/EnemySpawner.cs` | Spawner de encontros configuravel |
| `Enemy/EnemyPatrol.cs` | Patrulha + deteccao |

### 8. Performance
- Objetos de cenario marcados `isStatic` para static batching
- **Window → Rendering → Lighting → Generate Lighting** para bake
- Shadow Distance: 45m (configurado em codigo)
- Pool de SFX no AudioManager evita Instantiate/Destroy de AudioSources

### 9. Git LFS (recomendado)
O `.gitattributes` ja esta configurado para rastrear arquivos grandes via LFS.

Para ativar:
```bash
git lfs install
git lfs track "*.fbx" "*.obj" "*.png" "*.wav"
```

Arquivos de modelos 3D em `Modelos/` devem usar LFS para evitar repositorio lento.

