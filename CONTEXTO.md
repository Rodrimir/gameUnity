# GameYunit — Contexto do projeto

Documento de entrada para quem chega ao projeto, seja uma pessoa da equipe ou um assistente de IA iniciando uma nova sessão. Resume o que existe, como está organizado, o que ainda não foi validado e como continuar. Os detalhes de cada bloco ficam em `Docs/BlocoN.md`; o roteiro original (prompts) está em `PROMPTS.md`.

*Atualizado em 02/11/2026, após o commit `6a34653` (Blocos 5 e 6).*

## 1. O jogo

Bullet heaven / survivors-like 3D em terceira pessoa (câmera top-down angular), com estética retrô de PlayStation 1. Co-op para **2 jogadores** em modo **Host–Client** pela rede local.

| Item | Escolha |
|---|---|
| Engine | Unity **6000.3.27f2 (6.3 LTS)**; o código também usa só APIs disponíveis na 2022.3 |
| Render pipeline | URP |
| Rede | Netcode for GameObjects (NGO **2.13.3** validado; mínimo 1.8 por causa de `[Rpc]`) + Unity Transport **2.7.4** |
| Transporte | **UDP**, porta 7777 (o `UnityTransport` não implementa TCP). Mesma máquina: `127.0.0.1`. LAN: Host com Listen `0.0.0.0` |
| Input | Input System **1.20.1** (pacote novo) |
| Desenvolvimento | Linux (CachyOS) |

## 2. Regras de arquitetura (obrigatórias)

1. **Server authoritative.** O client envia apenas **intenção** (movimento e mira, `PlayerInputData`). Posição, colisão, alvo, dano, XP e nível são decididos no servidor. No Host, a lógica de servidor roda localmente pelo mesmo caminho. O item "replicar do Client para o Host" da versão anterior deste arquivo **foi descartado**: o `NetworkTransform` usa autoridade de servidor.
2. **Object Pooling** para tudo que nasce em massa. Há dois caminhos:
   - objetos locais (projéteis) usam `ObjectPoolManager`;
   - objetos de rede (inimigos, gemas de XP) usam `NetworkObjectPool`, que adapta o mesmo pool ao `INetworkPrefabInstanceHandler` do NGO.
   - Nunca use `Instantiate`/`Destroy` diretamente nesses objetos.
3. **Observer.** Sistemas se comunicam por eventos C# (`event Action<...>`). Eventos estáticos têm `ResetStatics` com `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`, por causa do *Enter Play Mode* sem domain reload.
4. **Projéteis não são NetworkObjects.** Um RPC por rajada (`ProjectileVolley`) e um por fim de projétil; cada client simula o visual localmente, adiantado pela latência.
5. **Desacoplamento entre desenvolvedores.** Blocos feitos em paralelo devem preferir arquivos e asmdefs novos e integrar-se pelas APIs públicas já existentes (ver §5). Se precisar mexer em arquivo de outro bloco, faça a mudança mínima e registre no `Docs/BlocoN.md`.

## 3. Estado por bloco

| Bloco | Conteúdo | Estado |
|---|---|---|
| 0 — Fundação | Pastas, `NetworkManagerSetup` (Host/Client, aprovação com máx. 2), `ObjectPoolManager`, `PlayerNetwork` | ✅ Compilado e testado |
| 1 — Movimento/câmera | `PlayerController` (CharacterController no servidor), `CameraController` (só o jogador local), mira independente mouse/analógico (`PlayerAimInput`) | ✅ Compilado; 11 testes EditMode passaram |
| 2 — Hordas | `EnemySpawner` (fluxo contínuo, 3/s, até 128, anel 12–18 m), `EnemyAI` (persegue o jogador mais próximo), `NetworkObjectPool` | ✅ Compilado; 12 testes passaram (com sessão Host real) |
| 3 — Ataque automático | `AutoAttackSystem`, `Projectile`, `ProjectileVolley`, `IDamageable`; 3 armas iniciais em `WeaponDefinition` (Varinha teleguiada, Leque de 5, Perfurante); preparo de mapa (`MapSetupTool`, `PlayerSpawnPoint`, spawn de inimigos apoiado no chão) | ⚠️ **Não compilado** |
| 4 — Vida/dano/morte | `HealthComponent`, `OnDeath`, barra de HP | 🔧 **Em desenvolvimento por outra pessoa**; nada no repositório ainda |
| 5 — XP e níveis | `XpDropper`, `XpDropService`, `XpGem`, `PlayerLevel` (XP individual, curva 5 + 5×(N−1)) | ⚠️ **Não compilado**; feito só com arquivos novos |
| 6 — Mapa/NavMesh/PSX | `PsxRenderTextureSetup` (320x240, Point, letterbox), shader `GameYunit/PSX Lit` (snap + afim + Gouraud), regras de importação retrô, `NavMeshSetupTool`, `NavMeshSteering` | ⚠️ **Não compilado** (shader incluso) |

"Compilado e testado" vale para a instalação Unity 6000.3.27f2 + NGO 2.13.3 descrita em `Docs/Bloco1.md` e `Docs/Bloco2.md`. Os Blocos 3, 5 e 6 foram escritos numa máquina sem Unity e têm testes prontos que **nunca foram executados**.

## 4. Organização do código

```
Assets/_Project/
  Art/Shaders/PSXLit.shader        Shader URP PSX
  ScriptableObjects/Weapons/       Weapon_HomingWand, Weapon_FanBurst, Weapon_PiercingBolt
  Scripts/
    Core/        GameYunit.Core — sem rede nem input: Pooling/, Rendering/CameraViewportMapping
    Network/     NetworkManagerSetup, NetworkObjectPool
    Player/      PlayerNetwork, PlayerController, PlayerAimInput, PlayerInputData, CameraController
    Enemies/     EnemyAI, EnemySpawner, IEnemySteering, NavMeshSteering
    Combat/      AutoAttackSystem, Projectile, ProjectileVolley, WeaponDefinition, IDamageable
    Progression/ PlayerLevel, XpGem, XpDropper, XpDropService
    Map/         PlayerSpawnPoint
    Rendering/   GameYunit.Rendering — PsxRenderTextureSetup (usa uGUI)
    UI/Debug/    NetworkDebugHUD, WeaponSelectDebugHUD, LevelDebugHUD (IMGUI, só Editor/Development Build)
    Editor/      GameYunit.Editor — MapSetupTool, Psx/ (importação e materiais)
      Navigation/ GameYunit.Editor.Navigation — NavMeshSetupTool (só compila com AI Navigation)
  Tests/Editor/  GameYunit.Editor.Tests — testes EditMode e de sessão Host
Docs/BlocoN.md   Setup na Unity, fluxo de autoridade e roteiro de teste de cada bloco
```

| Assembly | Referências |
|---|---|
| `GameYunit.Core` | nenhuma |
| `GameYunit.Runtime` | Core, Netcode, Transport, Collections, InputSystem |
| `GameYunit.Rendering` | Core, `UnityEngine.UI` (separada para não alterar a Runtime) |
| `GameYunit.Editor` | Core, Runtime |
| `GameYunit.Editor.Navigation` | AI Navigation; `defineConstraints` exige o pacote |
| `GameYunit.Editor.Tests` | Core, Runtime, Netcode, InputSystem + test frameworks |

Convenções: namespaces `GameYunit.<Pasta>`; o código em `UI/Debug` usa o namespace `GameYunit.UI`, porque `.Debug` sombrearia `UnityEngine.Debug`. Campos privados `_camelCase`. Comentários e logs em português, com prefixo `[NomeDaClasse]`. Métodos que só valem no servidor terminam em `Server` (`AddXpServer`, `ReturnToPoolServer`). Cada bloco tem um `Docs/BlocoN.md`.

## 5. Pontos de integração (contratos entre blocos)

Para o **Bloco 4**:
- **Dano:** implemente `GameYunit.Combat.IDamageable.ApplyDamageServer(float amount, ulong sourceClientId, Vector3 hitPoint)` no `HealthComponent`. Os projéteis já o chamam no servidor, procurando com `GetComponentInParent<IDamageable>()` a partir do collider atingido.
- **Morte do inimigo:** chame `EnemyAI.ReturnToPoolServer()`. Esse método despublica o inimigo e dispara `EnemyAI.OnServerDespawned`. Os sistemas que já escutam esse evento passam a funcionar sozinhos:
  - o `EnemySpawner` atualiza a contagem;
  - o `XpDropper` gera a gema de XP.
- **Se houver despawn que não é morte:** desligue *Drop On Server Despawn* no `XpDropper` e chame `XpDropper.NotifyKilledServer()` a partir do `OnDeath`.
- **Arquivos já alterados:** o `EnemyAI.FixedUpdate` foi alterado no Bloco 6 (consulta a `IEnemySteering`). Se o Bloco 4 mexer no mesmo método, o conflito de merge fica restrito a esse trecho.
- **Canvas da barra de HP:** use Sort Order maior que `-100` para ficar acima da tela PSX.

Outras APIs públicas úteis:
- **Jogador local:** `PlayerNetwork.LocalPlayer` e os eventos `OnLocalPlayerSpawned`/`OnLocalPlayerDespawned`, usados por câmera e HUDs.
- **Combate:**
  - `AutoAttackSystem.OnServerProjectileHit(GameObject, Vector3, float dano)` serve para números de dano;
  - `OnVolleyFired` e `OnProjectileImpact` servem para SFX/VFX;
  - `RequestWeapon(int)` troca a arma pelo servidor.
- **Progressão:** `PlayerLevel.OnXpChanged(xp, necessário)`, `OnLevelUp(nível)` e o estático `OnAnyPlayerLevelUp(jogador, nível)`, que será a entrada da tela de Level Up.
- **Inimigos:** `EnemyAI.ActiveOnServer` (lista no servidor) e `EnemySpawner.OnEnemySpawned`.

## 6. O que ainda não existe no repositório

O repositório tem **apenas scripts, shader, assets de dados e docs**. Ainda não há:
- `Packages/` e `ProjectSettings/` (projeto Unity versionado);
- cenas, prefabs (Player, Enemy, Projectile, XpGem) e o asset de Input Actions;
- o mapa que será usado na cena inicial (foi combinado colá-lo em `Assets/`);
- o Bloco 4.

Por isso **nada é jogável direto do clone**: cada `Docs/BlocoN.md` descreve como montar a sua parte na Unity.

Pacotes necessários: Netcode for GameObjects, Unity Transport, Input System (Active Input Handling = *New* ou *Both*), URP, uGUI, **AI Navigation** (Bloco 6), Unity Test Framework e Input System Test Framework (testes). Ative também **Run In Background**.

## 7. Como prosseguir (ordem recomendada)

1. **Versionar o projeto Unity.** Crie um projeto URP 3D na 6000.3.27f2, incorpore `Assets/_Project` e commite `Packages/`, `ProjectSettings/` e todos os `.meta` gerados. Sem isso, cada pessoa monta cenas e prefabs diferentes.
2. **Compilar e rodar os testes** (Test Runner → EditMode) e corrigir o que falhar nos Blocos 3, 5 e 6, a começar pelo shader e pelos testes de sessão Host. Atualize o "Estado" de cada `Docs/BlocoN.md` com o resultado.
3. **Montar a cena `Game` e os prefabs**, seguindo `Docs/Bloco1.md` → `Bloco2` → `Bloco3` → `Bloco5` → `Bloco6`, e commitar cena, prefabs e Input Actions. Teste Host no Editor + Client numa build Linux de Development.
4. **Integrar o Bloco 4** quando estiver pronto (contratos da §5). Depois, adicione no `README` os links para `Docs/Bloco5.md` e `Docs/Bloco6.md`; eles ficaram de fora para evitar conflito.
5. **Importar o mapa**, nesta ordem:
   1. GameYunit → PSX → Aplicar importação retrô (pasta do mapa);
   2. GameYunit → Mapa → Preparar mapa selecionado;
   3. GameYunit → PSX → Converter materiais;
   4. GameYunit → Mapa → Configurar e assar NavMesh;
   5. posicionar os `PlayerSpawnPoint`.
6. **Tomar as decisões pendentes** (§8) e seguir para os próximos sistemas:
   - tela de Level Up com escolha de upgrades: os upgrades podem alterar os campos das `WeaponDefinition`, que já são combináveis;
   - loop de partida: início, timer/ondas, game over e reinício;
   - tela de seleção de arma que trava a escolha após o início;
   - menu e lobby (IP do Host);
   - áudio, VFX e balanceamento.

## 8. Decisões

Tomadas:
- **Autoridade:** movimento autoritativo no servidor, sem predição no client.
- **Mira:** independente do movimento (mouse ou analógico direito).
- **Spawn de inimigos:** fluxo contínuo, como ponto de partida.
- **Armas:** as três ficam disponíveis como arma inicial. Todas miram o inimigo mais próximo e só disparam com inimigo no raio.
- **XP:** individual por jogador. Acima de 300 gemas ativas, o XP é somado à gema mais antiga.
- **NavMesh:** consultas de caminho (`NavMesh.CalculatePath`) em vez de `NavMeshAgent`, para manter o `CharacterController` e a replicação.

Pendentes (aguardando o responsável pelo projeto):
- **Bloco 4:** morte direta ao pool ou com feedback antes; números de dano; dano de contato com invulnerabilidade ou golpe com recarga.
- **Tela de Level Up:** pausa global (lógica, no servidor), UI sobreposta em tempo real ou híbrido (câmera lenta + fila).
- **Formato do mapa:** arena fechada, treadmill infinito ou arena com bordas que se repetem.
- **Spawn de inimigos:** manter o fluxo contínuo, trocar para ondas ou usar fluxo contínuo com picos.

## 9. Limitações conhecidas

- A troca de arma é permitida a qualquer momento; ainda não há trava após o início da partida.
- Quando o `EnemySpawner` é despublicado (fim de partida), os inimigos removidos também geram gemas.
- O spawner apoia inimigos no chão por raycast, mas não verifica se o ponto está no NavMesh, então um inimigo pode nascer numa área isolada.
- No client, o visual de um projétil atravessa a parede até chegar o RPC de fim (cerca de um RTT).
- O shader PSX não recebe sombras nem luzes adicionais, por escolha estética.
- O Client sente a latência de ida e volta no próprio movimento (sem predição).
