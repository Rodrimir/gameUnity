# Plano — Refazer o GameYunit sobre o 3D Game Kit (co-op 2 jogadores, Host–Client)

*Plano aprovado em 02/11/2026. A §1 registra o diagnóstico feito antes da Fase 0; o que mudou desde então está em [CONTEXTO.md](../CONTEXTO.md) e em `Docs/FaseN.md`. Itens marcados como "a confirmar" dependem da auditoria do kit na Fase 1.*

## Contexto
O GameYunit atual (survivors-like co-op em NGO) acumulou sistemas que nunca rodaram juntos. O mapa (Medieval Kit) e os personagens dependem de arquivos fora do Git, e a documentação marca como "testado" o que não foi validado em conjunto. O usuário quer recomeçar usando o **3D Game Kit 1.9.5** (Unity 6.3 + URP) como base de arte, fases, personagem e inimigos. O alvo é um jogo **jogável em co-op por IP direto**, primeiro funcional e testado em rede, depois expandido. O que existe hoje é preservado como legado.

### Decisões tomadas (§8)
| # | Decisão |
|---|---|
| 1 | **Movimento do dono:** o `PlayerController` da Ellen roda só no dono; `NetworkTransform` com autoridade do dono + `NetworkAnimator`. Todo o resto é decidido no servidor. |
| 2 | Protocolo padrão **UDP** (porta 7777); WebSocket/TCP selecionável. |
| 3 | **Combate do kit:** cajado da Ellen (`MeleeWeapon`), **golpe manual** como no kit. Descartar AutoAttack, armas e projéteis antigos. |
| 4 | **Visual do kit**; o PSX vai para o legado. |
| 5 | **Parceiro revive** o jogador caído; game over quando os dois caem. |
| 6 | **Kit fora do Git** (`.gitignore`). O `README` abre com o guia "o que falta no clone e como rodar localmente". |
| 7 | **Esc abre menu local**; só a escolha de upgrade pausa os dois. |

## 1. Diagnóstico do repositório (leitura feita)
- **Git:** `main` em `65b167c`, working tree **limpo** (nada a perder aqui). Os arquivos ignorados — `modelo-jogo/*.zip`, `Source/**/*.fbx|png|tga|bmp`, `Packages/com.danielgruginski.medievalkit/` — **não existem nesta máquina**, só na outra (a do autor "Labubu"). Sem tags e sem branches além de `main`.
- **Projeto:** Unity 6000.3.27f2 (`ProjectVersion.txt`), URP 17.3, NGO 2.13.3, UTP 2.7.4, Input System 1.20.1, AI Navigation 2.0.9. 7 asmdefs (Core, Runtime, Rendering, Editor, Editor.Bootstrap, Editor.Navigation, Editor.Tests), cerca de 8 mil linhas em `Assets/_Project`.
- **Quebrado e por quê:**
  - a cena `Game` (18,7 MB) é o `VK_ValleyTown` inteiro, com 89.548 referências ao kit medieval ausente: **sem chão no clone**;
  - os 4 visuais de personagem apontam para 37 FBX ausentes;
  - não há fim de partida, e a morte do jogador não tem política;
  - menu e HUD são IMGUI;
  - IA e spawn não usam regras de visão nem de NavMesh;
  - nada foi testado em Host–Client real com a cena montada.
- **Ambiente nesta máquina:**
  - Editor em `~/Unity/Hub/Editor/6000.3.27f2` (o launcher aponta para `/mnt/SSD/...`, que não existe aqui; dá para usar `GAMEYUNIT_UNITY_EDITOR`);
  - **sem licença, sem Unity Hub e sem `wmctrl`** (o `--reopen-*` do launcher depende dele). *Atualização da Fase 0: o Hub foi instalado e a licença Personal ativada; o `wmctrl` continua ausente.*
- **Launcher (`Tools/unity.sh`):**
  - `UPM_CACHE_ROOT` correto;
  - a Asset Store usa `UNITY_ASSET_STORE_CACHE_PATH`, mas a variável documentada é **`ASSETSTORE_CACHE_PATH`**;
  - `~/.local/share` (destino padrão `unity3d/Asset Store-5.x`) fica **somente leitura** no sandbox, então o download do kit falharia. *Corrigido na Fase 0 (ver [Fase0.md](Fase0.md)).*
- **Conflito de regra:** o `AGENTS.md` diz que o Bloco 4 é de outra pessoa; este plano substitui o `HealthComponent` pela vida do kit. Isso precisa de alinhamento com essa pessoa (ver Riscos).

## 2. Arquitetura alvo — quem decide o quê
| Assunto | Servidor | Dono | Cliente local |
|---|---|---|---|
| Movimento, rotação e animação da Ellen | — | ✔ (`NetworkTransform` + `NetworkAnimator` do dono) | — |
| Golpe do cajado | detecção de acerto e dano (`MeleeWeapon` na cópia do servidor) | input e animação | — |
| Vida, caído, revive, morte | ✔ (`NetworkVariable`) | recebe a reação visual por RPC | HUD |
| Inimigos: spawn, IA, NavMesh, animação | ✔ (`NetworkTransform`/`Animator` do servidor) | — | só visual |
| Gemas, XP, nível, oferta de upgrade, aplicação | ✔ | escolhe o upgrade (RPC) | — |
| Partida: timer, game over, reinício | ✔ | — | telas |
| Cena (Level1, depois Level2) | ✔ (`NetworkSceneManager`) | — | — |
| Cor e nome da Ellen | replica (`NetworkVariable`) | escolhe | — |
| Câmera, input, menu Esc, HUD | — | — | ✔ |

- **Rede:** UTP UDP ou WebSocket, mesma escolha nos dois lados. Host escuta em `0.0.0.0:7777`; o Client conecta em `127.0.0.1` ou no IP da LAN.
- **Pooling:** `ObjectPoolManager` + `NetworkObjectPool` para inimigos, gemas e VFX.
- **Observer:** eventos C# com `ResetStatics`.

## 3. Pastas, assemblies e legado
- **Legado:**
  - tag `legacy/v1-pre-gamekit` + branch `legacy/v1` em `65b167c`;
  - na branch de trabalho `rebuild/gamekit`, mover para `Legacy/` (fora de `Assets/`, não compila): `Assets/_Project` (inteiro, **com `mv` físico** para levar também os arquivos ignorados na outra máquina), `Docs/` antigos, `CONTEXTO.md` antigo, `PROMPTS.md`, `Tools/import_models.py` e, se existir, `Packages/com.danielgruginski.medievalkit/` (deixá-lo em `Packages/` faria a Unity importá-lo);
  - `modelo-jogo/` fica onde está;
  - atualizar o `.gitignore` para os novos caminhos em `Legacy/`.
- **Novo:**
  - `Assets/_Project/{Scripts,Prefabs,Scenes,ScriptableObjects}`;
  - asmdefs `GameYunit.Core` (sem rede), `GameYunit.Runtime`, `GameYunit.Editor` e `GameYunit.Tests`;
  - **ponte com o kit:** `Assets/_Project/Scripts/KitBridge/` **sem asmdef**, para compilar no Assembly-CSharp. Uma asmdef não enxerga o Assembly-CSharp, e o kit provavelmente não tem asmdef (*a confirmar*). A ponte enxerga o kit e as nossas asmdefs; as asmdefs nunca enxergam o kit. Cópias de scripts do kit (`GameYunit.*`) também vivem aqui;
  - o kit fica intocado na pasta dele (provavelmente `Assets/3DGamekit/`, *a confirmar*), ignorado pelo Git.
- **Docs:** `CONTEXTO.md` novo (fonte da verdade), `Docs/FaseN.md`, `Docs/AlteracoesNoKit.md` e `README.md` abrindo com "Rodar localmente": o que não vem no clone (kit, Library, licença, Editor) e o passo a passo.

## 4. Reaproveitamento
| Item | Veredito | Motivo |
|---|---|---|
| `ObjectPoolManager` (+ `PrefabPool`, `PooledObject`, `IPoolable`) | **Reaproveitar** | Core sem dependências, correto; serve para inimigos, gemas e VFX. |
| `NetworkObjectPool` | **Reaproveitar** | Adaptador do `INetworkPrefabInstanceHandler` sem dependências de gameplay. |
| `NetworkManagerSetup` | **Reaproveitar + estender** | A aprovação com máx. 2 está correta. Falta: protocolo UDP/WebSocket (`UseWebSockets`), Host em `0.0.0.0` por padrão e carregamento de cena via `NetworkSceneManager`. Extrair a regra de aprovação para uma função pura testável. |
| `PlayerSpawnPoint` | **Reaproveitar** | Simples, já integrado à aprovação. |
| `PlayerLevel`, `XpDropService` | **Reaproveitar** | Curva 5 + 5×(N−1) e fusão acima de 300 corretas e independentes. |
| `XpGem` | **Reescrever a coleta** | O trigger com o CharacterController foi pensado para movimento no servidor. Com movimento do dono, coletar por distância no servidor é mais robusto. |
| `XpDropper` | **Reescrever** | Exige `EnemyAI` (descartado). Passa a ouvir a morte do inimigo do kit pela ponte. |
| `GameSessionController` | **Reescrever** como `MatchController` | A pausa global por Esc contraria a decisão 7. A fila de upgrades pendentes é reaproveitada como ideia. |
| `AutoAttackSystem`, `WeaponDefinition`, `Projectile`, `ProjectileVolley` | **Descartar** (legado) | Decisão 3: combate do kit. |
| `HealthComponent`, `PlayerHealthUI` | **Descartar** em favor do `Damageable` do kit + ponte `NetworkHealth` | Evita dois sistemas de vida. Depende de alinhar com o dono do Bloco 4. |
| `EnemyAI`, `EnemySpawner`, `NavMeshSteering` | **Descartar** a IA; **reescrever** o spawner | A IA vem do kit. O spawner aproveita a lógica de anel, chão e rampa, mas fica genérico para Chomper, Spitter e Grenadier. |
| `PlayerNetwork`, `PlayerController`, `PlayerAimInput`, `PlayerInputData`, `CameraController` | **Descartar** | Decisão 1: controlador e câmera da Ellen. |
| PSX, `CameraViewportMapping`, ferramentas de mapa e PSX, `ProjectSetup` | **Legado** | Decisão 4; o mapa vem do kit. |
| Menus e HUDs IMGUI | **Descartar** | Telas novas em uGUI. |
| Testes | **Reaproveitar** só os de pool, XP e aprovação (adaptados); **descartar** os de combate, mapa, mira e inimigo | Vários eram de sistemas descartados. |

## 5. Adaptações do kit para 2 jogadores (*a confirmar na auditoria da Fase 1*)
| Ponto | Problema esperado | Solução proposta |
|---|---|---|
| `PlayerController.instance` / `PlayerInput.Instance` (singletons) | Duas Ellens: o último `Awake` vence; câmera, `TargetScanner`, HUD e checkpoints apontam para a errada. | Cópia `GameYunit` do controlador e do input na `KitBridge`, sem singleton global (registro "jogador local" + lista de jogadores). Prefab da Ellen copiado com a referência trocada. Registrar em `AlteracoesNoKit.md`. |
| Input | O kit provavelmente usa o Input Manager antigo (`Input.GetAxis`, `Fire1`). | Active Input Handling = **Both**. Input só no dono (o componente de input é desativado nas outras cópias). |
| Root motion (`OnAnimatorMove`) | Nas cópias não-dono, o Animator moveria o CharacterController, brigando com o `NetworkTransform`. | Não-dono: `PlayerController` desativado, `applyRootMotion = false`, CharacterController só como collider. |
| Câmera (`CameraSettings`/Cinemachine; versão *a confirmar*) | Rig único ligado à Ellen da cena. | Rig local que se liga à Ellen do dono no spawn. |
| `Damageable` + mensagens (`IMessageReceiver`) | Dano local em cada peer, e as animações de ataque replicadas acertam réplicas nos clients. | Nas réplicas (não-servidor), `Damageable` invulnerável. No servidor, a ponte `NetworkHealth` espelha a vida em `NetworkVariable`, decide caído/revive e manda um RPC ao dono para a reação visual (hit/queda). O HUD lê a `NetworkVariable`. |
| `MeleeWeapon` (cajado; mordida do Chomper) | Os eventos de animação disparam o golpe em todos os peers. | Só a cópia do servidor aplica dano (réplicas invulneráveis). O `NetworkAnimator` leva o trigger do dono ao servidor. |
| Inimigos: `EnemyController` (`NavMeshAgent` + root motion), `ChomperBehaviour`, `TargetScanner` | O alvo é sempre `PlayerController.instance`; a IA roda em todos os peers. | Cópias `GameYunit` dos comportamentos com alvo = jogador vivo mais próximo. IA, Agent e scanner **só no servidor**; clients só recebem transform e animator. Reset para o pool: `Damageable.ResetDamage`, `Animator.Rebind`, `NavMeshAgent.Warp`. |
| `SceneController`, `TransitionPoint`, `PersistentDataManager`, `Checkpoint`, `RespawnPlayer` | Fluxo single-player com fade e respawn em checkpoint. | Remover nas **cópias** das cenas; carregamento via `NetworkSceneManager`; spawn pelos `PlayerSpawnPoint`; morte segue a decisão 5. |
| Menu de pausa (provável `Time.timeScale`) | Pausa a máquina inteira. | Menu Esc local próprio, sem `timeScale`. |
| Puzzles, portas, placas, cutscenes, diálogos (Level1) | Bloqueiam uma partida de horda. | Desativar só na cópia da cena (`Assets/_Project/Scenes/Level1_Coop`). |

## 6. Fases (cada uma termina com testes, compilação em batch, roteiro manual, docs, commit e **parada**)
**Fase 0 — Preservar e diagnosticar.**
- Tag e branch de legado; criar `rebuild/gamekit`.
- Mover para `Legacy/` e atualizar o `.gitignore`.
- Corrigir o launcher:
  - `ASSETSTORE_CACHE_PATH=.unity/asset-store`;
  - bind de `~/.local/share/unity3d` → `.unity/data/unity3d`;
  - Editor padrão resolvido por `GAMEYUNIT_UNITY_EDITOR` → `/mnt/SSD/...` → `~/Unity/Hub/Editor/<ProjectVersion>`;
  - licença copiada também de `~/.config/unity3d/Unity/licenses`.
- Novo `README` (guia "o que falta no clone") e `CONTEXTO.md`.
- **Pronto:** legado acessível pela tag/branch e pasta `Legacy/`; repositório sem nada em `Assets/` além do esqueleto.

**Fase 1 — Projeto limpo + kit em single-player.**
- Manifesto mínimo do template URP 6.3 (sem rede ainda); apagar `Library` local.
- **Passo manual do usuário:** abrir o Editor por `Tools/unity.sh`, entrar na conta, *Window → Package Manager → My Assets → 3D Game Kit → Download → Import* (tudo, aceitando sobrescrever o ProjectSettings).
- Revisar o diff de `ProjectSettings` (tags, layers, input, quality). Depois instalar NGO, UTP e uGUI nas versões do registro.
- Auditoria da §5 em `Docs/Fase1.md`.
- **Pronto:** menu do kit → Level1 jogável sozinho, sem erros no Console.

**Fase 2 — Co-op mínimo.**
- NetworkManager com aprovação (máx. 2).
- Tela de conexão uGUI: Criar, Entrar (IP:porta), protocolo UDP/WebSocket.
- Cópia `Level1_Coop` carregada pelo `NetworkSceneManager`; dois spawns.
- Ellen de rede com cor e nome; câmera e input locais; `NetworkAnimator`.
- Desconexão limpa.
- **Pronto:** Editor como Host + build Linux de Development como Client em `127.0.0.1`, os dois se movem e se veem, em UDP **e** em WebSocket.

**Fase 3 — Inimigos e combate.**
- Chomper vindo do `NetworkObjectPool`; IA só no servidor, mirando o jogador vivo mais próximo.
- Ponte `NetworkHealth` sobre o `Damageable`.
- Golpe do cajado com dano decidido no servidor; mordida do Chomper.
- Caído → revive pelo parceiro → game over se os dois caírem.
- HUD de vida local.
- **Pronto:** os dois enfrentam os mesmos Chompers, com o mesmo estado nos dois peers.

**Fase 4 — Loop survivors.**
- Spawner contínuo, começando com teto de **40** (medir antes de subir).
- Gemas e XP individual.
- Upgrades periódicos sobre atributos do kit (dano do `MeleeWeapon`, vida máxima do `Damageable`, velocidade), com pausa global só na escolha.
- `MatchController`: timer, game over, reinício sem reabrir o jogo.
- Spitter e Grenadier como escalada.
- **Pronto:** partida co-op completa, do menu ao game over e reinício.

**Fase 5 — Polimento.** Level2, áudio, balanceamento.

**Testes por fase (EditMode, lógica pura):**
- regra de aprovação e configuração do protocolo (Fase 2);
- pool e ponte de vida (Fase 3);
- curva de XP, fusão de gemas e regras de spawn (Fase 4).

**Roteiro manual por fase:** Editor Host + build Linux Client (o MPPM não existe no Linux), passo a passo.

## 7. Riscos e mitigação
| Risco | Mitigação |
|---|---|
| Kit sem asmdef (Assembly-CSharp) | Camada `KitBridge` sem asmdef; nossas asmdefs não dependem do kit. |
| Singletons e fluxo single-player espalhados pelo kit | Auditoria completa na Fase 1 antes de detalhar a Fase 2; cópias registradas em `AlteracoesNoKit.md`. |
| Import do kit sobrescreve ProjectSettings | Importar antes dos pacotes de rede, num projeto limpo; revisar o diff. |
| Download da Asset Store fora da raiz ou bloqueado pelo sandbox | Correção do launcher na Fase 0; verificar `.unity/asset-store` após o download. |
| Login da conta e licença nesta máquina | Hub + licença Personal instalados; o launcher copia a licença para `.unity/` (verificado na Fase 0). O Editor no sandbox registra `Access token is unavailable`: o login para a My Assets talvez precise ser feito dentro do Editor (*a confirmar* na Fase 1). |
| `NetworkAnimator` com autoridade do dono em NGO 2.x (API *a confirmar*) | Validar na Fase 2; alternativa é a subclasse `OnIsServerAuthoritative() => false`. |
| Inimigos do kit pesados (Animator + Agent + root motion) | Teto inicial de 40; medir no Profiler antes de subir. |
| Reuso no pool de inimigos do kit (estado sujo) | Reset explícito na ponte; teste de reuso. |
| Arquivos ignorados na outra máquina (FBX, kit medieval em `Packages/`) | `mv` físico para `Legacy/` e `.gitignore` ajustado; avisar quem usa a outra máquina. |
| `AGENTS.md`: Bloco 4 de outra pessoa | Confirmar com essa pessoa; atualizar `AGENTS.md` na Fase 0 (inclui caminhos `/home/labubu` e `/mnt/SSD`). |
| Sem `wmctrl` nesta máquina | O usuário instala, ou fecha e abre o Editor manualmente (sem `--reopen-*`). |

## 8. Verificação
- **Fase 0:** `git tag`/`git branch` mostram o legado; `git status` limpo após o commit; o launcher imprime os caminhos dentro da raiz.
- **Fase 1 em diante:**
  - `Tools/unity.sh -batchmode -quit` sem erros no `Logs/Editor.log`;
  - `-runTests -testPlatform EditMode` com todos verdes;
  - roteiro manual Host (Editor) + Client (`Builds/` Linux Development) em `127.0.0.1`, UDP e WebSocket, e em LAN com o Host em `0.0.0.0`;
  - `CONTEXTO.md` registra só o que foi verificado e como.
- O MCP `voz_tts` do `AGENTS.md` não está disponível nesta sessão: os resumos ficam em texto.

Fontes: [ASSETSTORE_CACHE_PATH (Unity Manual)](https://docs.unity.com/en-us/engine/current/manual/packages-list/package-manager-caches/upm-config-cache-as), [3D Game Kit Reference Guide](https://learn.unity.com/tutorial/3d-game-kit-reference-guide?version=2020.3), [3D Game Kit Quick Start](https://learn.unity.com/course/3d-game-kit/tutorial/quick-start).
