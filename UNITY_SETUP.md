# VILL4GE — Setup no Unity (RE4 Tribute Edition)

## Pré-requisitos
- **Unity 2021.3 LTS** ou superior (recomendado: 2022.3 LTS)
- Template **3D (Built-in Render Pipeline)**

## Passo a passo

### 1. Criar projeto
1. **Unity Hub** → **New Project** → template **3D (Core)** → nome `VILLAG`

### 2. Importar scripts
1. Copie `Assets/Scripts/` → `<Projeto>/Assets/Scripts/`
   - 7 classes em 7 arquivos (.cs) + 1 classe utility `TorchFlicker`
2. Unity compila automaticamente

### 3. Configurar a cena
1. Na SampleScene, **delete** `Main Camera` e `Directional Light`
2. **GameObject → Create Empty** → renomear `GameBootstrap`
3. **Add Component** → `GameManager`
4. **Play!**

### 4. Controles (estilo RE4)

| Tecla         | Ação                          |
|---------------|-------------------------------|
| WASD          | Mover (lento ao mirar)        |
| Mouse         | Olhar                         |
| **RMB**       | **Mirar (over-the-shoulder)** |
| **LMB**       | **Atirar (mirando) / Faca**   |
| Shift         | Sprint (sem mirar)            |
| R             | Recarregar                    |
| Scroll / 1-5  | Trocar arma                   |
| G             | Granada                       |
| H             | Usar erva (+40 HP)            |
| E             | Falar com Merchant            |

### 5. Features RE4-faithful
- **Câmera over-the-shoulder** com offset, FOV dinâmico
- **Mira laser vermelha** (aparece ao segurar RMB)
- **Faca** (LMB sem mirar = melee slash)
- **4 tipos de Ganado**: Villager (foice), Pitchfork (forcado), Heavy (machado), **Dr. Salvador (motosserra!)**
- **Headshot** = dano ×3 + cabeça explode com partículas de sangue
- **Stagger** ao acertar inimigos (Chainsaw só com headshot)
- **Vila procedural**: igreja com torre/cruz/sino, cemitério com lápides, fogueira central, casas com telhado, caminhos de terra, cercas, carroças, feno, poços
- **Tochas com flicker** (luzes que variam de intensidade)
- **Iluminação sombria**: sol fraco, ambient trilight escuro, fog exponencial
- **Chuva** via ParticleSystem
- **Merchant**: "What are ya buyin'?" — capa roxa, olhos brilhantes, tapete de itens
- **HUD**: barra LIFE, munição estilo RE4, moeda ₧ (pesetas), kill counter
- **Wave Intro** com anúncio "— WAVE X —"
- **Tela de morte**: "YOU ARE DEAD" (RE4 style)
- **Muzzle flash** + **hit sparks**
- **Vignette** que intensifica com dano

### 6. Estrutura

| Arquivo               | Responsabilidade                                    |
|-----------------------|-----------------------------------------------------|
| `GameManager.cs`      | Vila procedural, igreja, fogueira, cenário, waves   |
| `Player.cs`           | OTS camera, laser, 5 armas, faca, granada, chuva   |
| `Enemy.cs`            | 4 tipos Ganado, armas, headshot, stagger, grab      |
| `GameUI.cs`           | HUD RE4, merchant panel, wave intro, death screen   |
| `Pickup.cs`           | Drops (erva/munição/pesetas)                        |
| `GrenadeProjectile.cs`| Timer + explosão em área + luz dinâmica             |

### 7. Performance
- Objetos de cenário marcados `isStatic` para batching
- **Window → Rendering → Lighting → Generate Lighting** para bake
- Shadow Distance: 45m (configurado via código)
