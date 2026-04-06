# VILL4GE — A Resident Evil 4 Tribute (Unity)

![Unity](https://img.shields.io/badge/Unity-2021.3%2B-blue?logo=unity)
![C#](https://img.shields.io/badge/C%23-Scripts-239120?logo=csharp)
![License](https://img.shields.io/badge/License-MIT-green)

> Jogo de sobrevivência em terceira pessoa inspirado em **Resident Evil 4**, feito inteiramente com código procedural em Unity — sem assets, sem prefabs, tudo via C#.

---

## Screenshots (In-Game)

*O jogo gera tudo proceduralmente: vila, inimigos, armas, UI — basta dar Play.*

---

## Features RE4-Faithful

### Gameplay
- **Câmera Over-the-Shoulder** — Offset dinâmico (hip/aim), FOV 70→55
- **Mira Laser Vermelha** — Aparece ao segurar RMB (LineRenderer + dot emissivo)
- **5 Armas**: Handgun, Shotgun, Rifle, TMP, Rocket Launcher
- **Faca Melee** — LMB sem mirar = slash de faca
- **Roundhouse Kick** — Pressione `F` em inimigo staggered (knockback + dano em área)
- **Granadas** — Timer 2s, explosão com luz dinâmica, dano em área 6m
- **Attaché Case (Maleta)** — Grid de inventário estilo RE4 com rotação de itens

### Inimigos (Ganados)
- **Villager** — Foice, chapéu de camponês
- **Pitchfork** — Forcado 3 pontas, mais rápido
- **Heavy** — Machado, 140 HP, lento e forte
- **Dr. Salvador (Chainsaw!)** — Saco na cabeça, motosserra, 350 HP, sem stagger

### Sistemas
- **Headshot** — Dano ×3, cabeça explode com partículas de sangue
- **Stagger** — Inimigos cambaleiam ao tomar dano (exceto Chainsaw)
- **Kick System** — Prompt contextual `[F] KICK` quando inimigo está staggered
- **Wave System** — Ondas crescentes com spawn inteligente
- **Merchant** — *"What are ya buyin'?"* — Compre armas, munição, ervas, upgrades
- **Attaché Case** — Grid 10×6 (expansível até 14×8), armas ocupam espaços diferentes

### Ambiente
- **Vila Procedural 70×70**: Igreja com torre/cruz/sino, cemitério, fogueira central
- **6 Casas** com telhado e cumeeira, caminhos de terra
- **Cenário**: Barris, caixotes, árvores, tochas, poços, feno, cercas, carroças, lápides
- **Tochas com flicker** (luz oscilante realista)
- **Chuva** via ParticleSystem (3000 partículas)
- **Fog Exponencial** + Ambient Trilight escuro

### UI
- Barra **LIFE** estilo RE4 (bottom-left)
- Munição e nome da arma (bottom-right)
- Moeda **₧ (Pesetas)**
- Wave intro *"— WAVE X —"*
- Tela de morte *"YOU ARE DEAD"*
- **Damage vignette** que intensifica com dano

---

## Estrutura do Projeto

```
re4_unity_projectf/
├── Assets/
│   └── Scripts/
│       ├── GameManager.cs      # Vila procedural, waves, lighting, scenery
│       ├── Player.cs           # OTS camera, laser, 5 armas, faca, kick, chuva
│       ├── Enemy.cs            # 4 tipos Ganado, headshot, stagger, knockback
│       ├── GameUI.cs           # HUD RE4, merchant, wave intro, death screen
│       ├── AttacheCase.cs      # Maleta grid com rotação (inventário RE4)
│       ├── Pickup.cs           # Drops: erva, ammo, pesetas
│       └── GrenadeProjectile.cs # Granada com explosão + luz
├── Modelos/                    # Modelos 3D (Leon, Handgun, Vila RE4)
├── projeto_antigo/             # Versões anteriores (Python/Ursina/Pygame)
├── UNITY_SETUP.md              # Guia de setup detalhado
├── .gitignore
└── README.md
```

---

## Setup Rápido

### Requisitos
- **Unity 2021.3 LTS** ou superior (recomendado 2022.3 LTS)
- Template **3D (Built-in Render Pipeline)**

### Passo a Passo
1. **Unity Hub** → New Project → **3D (Core)** → nome que quiser
2. Copie `Assets/Scripts/` para `<SeuProjeto>/Assets/Scripts/`
3. Na cena, **delete** `Main Camera` e `Directional Light`
4. **GameObject → Create Empty** → renomear `GameBootstrap`
5. **Add Component** → `GameManager`
6. **Play!**

---

## Controles

| Tecla | Ação |
|-------|------|
| WASD | Mover (lento ao mirar) |
| Mouse | Olhar |
| **RMB** | **Mirar (over-the-shoulder)** |
| **LMB** | **Atirar (mirando) / Faca (sem mirar)** |
| **F** | **Kick (inimigo staggered)** |
| Shift | Sprint |
| R | Recarregar |
| Scroll / 1-5 | Trocar arma |
| **TAB** | **Abrir Attaché Case (maleta)** |
| G | Granada |
| H | Usar erva (+40 HP) |
| E | Falar com Merchant |

---

## Modelos 3D Incluídos

A pasta `Modelos/` contém assets para uso futuro:
- `resident-evil-4-leon/` — Modelo do Leon
- `t77-handgun/` — Modelo da pistola
- `village-re4/` — Cenário da vila

---

## Roadmap

- [ ] Importar modelos 3D no lugar dos primitivos
- [ ] Sistema de áudio (tiros, passos, ambient, motosserra)
- [ ] Boss fight (El Gigante / Del Lago)
- [ ] Animações com Animator Controller
- [ ] Save system
- [ ] Otimização (LOD, object pooling)

---

## Créditos

Inspirado em **Resident Evil 4** da Capcom. Este é um projeto de estudo/tribute, sem fins comerciais.
