# GameYunit — Contexto do projeto

Documento de entrada para quem chega ao projeto, seja uma pessoa da equipe ou um assistente de IA iniciando uma nova sessão. Resume o que existe, como está organizado, o que ainda não foi validado e como continuar. Os detalhes de cada bloco ficam em `Docs/BlocoN.md`; o roteiro original (prompts) está em `PROMPTS.md`.

*Atualizado em 02/11/2026. Protótipo, personagens, combate, vida, pausa, upgrades e HUD conectados à cena `Game`. A Unity compilou os scripts sem erros; partida e Host–Client ainda não foram testados. O mapa da casa não está presente no checkout. Alterações locais não commitadas.*

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
| 2 — Hordas | `EnemySpawner` (fluxo contínuo, 1,5/s, até 128, anel 12–18 m), `EnemyAI` (persegue o jogador mais próximo a 1,5 m/s), `NetworkObjectPool` | ✅ Código compilado; ritmo reduzido pela metade; sessão ainda não retestada |
| 3 — Ataque automático | `AutoAttackSystem`, `Projectile`, `ProjectileVolley`, `IDamageable`; 3 armas iniciais em `WeaponDefinition` (Varinha teleguiada, Leque de 5, Perfurante); preparo de mapa (`MapSetupTool`, `PlayerSpawnPoint`, spawn de inimigos apoiado no chão) | ✅ Compilado; 5 testes de combate passaram |
| 4 — Vida/dano/morte | `HealthComponent` autoritativo (100 HP jogador, 60 inimigo), dano de contato (8/s), mortes, barras de HP | ✅ Integrado ao `IDamageable`; falta validar em Play Mode |
| 5 — XP e níveis | `XpDropper`, `XpDropService`, `XpGem`, `PlayerLevel` (XP individual, curva 5 + 5×(N−1)) | ✅ Compilado; 3 testes de progressão passaram |
| 6 — Mapa/NavMesh/PSX | `PsxRenderTextureSetup` (320x240, Point, letterbox), shader `GameYunit/PSX Lit` (snap + afim + Gouraud), regras de importação retrô, `NavMeshSetupTool`, `NavMeshSteering` | ✅ Compilado; 4 testes de mapa/renderização passaram |
| Personagens e cena | Biomechanical como inimigo; Survivalist, Adventure Female e Gravemind selecionáveis; `PlayerAppearance`, prefabs e cenas `Game`/`Characters` | ✅ Teste da cena real: Host, seleção dos três personagens e spawn do monstro |
| Menu e partida | `MainMenuHUD`, Coop/vídeo, ocultação ao entrar, pausa global por `Esc`, HUD de XP/nível e upgrades a cada 30 abates | ✅ Ligado à cena; falta validar cliques e fluxo Host–Client |

"Compilado e testado" acima registra a validação anterior à integração de vida, pausa e upgrades. Esta integração foi compilada pela Unity, mas nenhum teste automatizado ou fluxo completo foi executado nesta etapa. `Logs/TestResults.xml` contém resultados antigos e não representa esta integração. Uma instância Client separada ainda precisa de teste manual.

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
    Gameplay/    GameSessionController (pausa replicada e marcos de abates)
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

O **Bloco 4** implementa `IDamageable.ApplyDamageServer` em `HealthComponent`; projéteis atingem o contrato no servidor. A morte do Biomechanical chama `EnemyAI.ReturnToPoolServer()`, acionando o drop de XP existente. Inimigos causam 8 de dano de contato por segundo quando próximos; o servidor decide vida, dano, morte e upgrades.

Outras APIs públicas úteis:
- **Jogador local:** `PlayerNetwork.LocalPlayer` e os eventos `OnLocalPlayerSpawned`/`OnLocalPlayerDespawned`, usados por câmera e HUDs.
- **Combate:**
  - `AutoAttackSystem.OnServerProjectileHit(GameObject, Vector3, float dano)` serve para números de dano;
  - `OnVolleyFired` e `OnProjectileImpact` servem para SFX/VFX;
  - `RequestWeapon(int)` troca a arma pelo servidor.
- **Progressão:** `PlayerLevel.OnXpChanged(xp, necessário)`, `OnLevelUp(nível)` e `OnAnyPlayerLevelUp`; o marco separado de 30 mortes globais abre upgrades individuais.
- **Inimigos:** `EnemyAI.ActiveOnServer` (lista no servidor) e `EnemySpawner.OnEnemySpawned`.

## 6. Projeto Unity e arte

Agora existem `Packages/`, `ProjectSettings/`, os `.meta`, Input Actions, configurações URP e os prefabs Player, BiomechanicalEnemy, Projectile, XpGem e os quatro visuais. A cena `Game` reúne os sistemas num chão de teste com NavMesh e dois pontos de spawn; `Characters` mostra os modelos para inspeção. Veja `Docs/ProjetoUnity.md`.

**Papéis confirmados pelo usuário:** só Biomechanical Mutant é inimigo. Survivalist, Adventure Female e Gravemind Dreadnought são os três personagens selecionáveis. A seleção é validada no servidor e replicada, sem alterar os atributos/controlador. Materiais PSX, texturas Point limitadas a 256 pixels, escala ajustada e root motion desativado. Os três selecionáveis têm ciclos idle/run; o monstro agora usa o Animator humanoide com os clipes existentes. Armas estilizadas são anexadas à mão direita; magia e disparos balísticos têm cores, projéteis e rastros próprios.

Os ZIPs originais permanecem localmente em `modelo-jogo/`; os arquivos grandes de geometria e textura em `Assets/_Project/Art/Models/Characters/**/Source/` também ficam apenas na máquina. ZIPs, FBX e texturas de origem estão ignorados pelo Git, mas não foram removidos. Uma instalação limpa precisa receber esses arquivos à parte para restaurar a arte. O importador `Tools/import_models.py` seleciona apenas arte das versões Unity e preserva GUIDs; os visuais adaptados ficam em `Prefabs/Characters/`.

**Restrição do usuário:** nenhum download, extração, cache, build, log ou temporário fora desta raiz. Execute a Unity por `Tools/unity.sh`: ele lê o Editor já instalado e restringe gravações ao projeto; `.unity/`, `Library/`, `Temp/` e `Logs/` são ignorados pelo Git. Não use o launcher global da Unity nem projetos temporários externos.

**Fluxo do Editor:** após cada mudança visível, feche graciosamente a instância anterior, aguarde a janela e o lock do projeto fecharem, e reabra apenas uma instância na cena atualizada. Use `Tools/unity.sh --reopen-game` para `Game`, `--reopen-preview` para `Characters`, ou `--reopen-play` para testar o menu; o launcher aborta se o lock continuar ativo. Alterações não visuais não exigem reinício.

O kit `Medieval Village & Dungeon Kit` v0.3.0 foi baixado para `modelo-jogo/MedievalVillageKit_Unity_0.3.0.zip` e extraído como pacote local em `Packages/com.danielgruginski.medievalkit/`. Tanto o ZIP quanto o pacote de 601 MB estão ignorados pelo Git para evitar adicionar centenas de megabytes ao repositório. Ele inclui `VK_ValleyTown` (216 × 168 m), `VK_Hamlet`, interiores, masmorras e cavernas, sob licença CC0. `VK_ValleyTown` foi integrado à cena `Game`: o piso de teste foi removido, o NavMesh pré-assado do pacote foi mantido, foram adicionados colliders estáticos às malhas e os dois spawns foram projetados no NavMesh. A cena precisa de inspeção visual e validação de desempenho/colisão em Play Mode. O kit tem estética medieval estilizada, foi feito para câmera top-down e não equivale a uma casa contemporânea. Vida, dano, morte e XP também precisam de validação em partida e co-op.

## 7. Como prosseguir (ordem recomendada)

1. **Revisar e versionar a montagem local.** `Packages/`, `ProjectSettings/`, cenas, prefabs, Input Actions e `.meta` já foram criados. As alterações ainda não foram commitadas. Preserve os ZIPs originais e não versione caches/licenças/logs.
2. **Validar a integração em Play Mode.** Conferir Host, dano, mortes/XP, contato, upgrades, pausa, armas na mão, animações e efeitos. Os testes automatizados ainda não foram executados nesta integração.
3. **Testar Host–Client em instâncias separadas.** Falta validar seleção replicada, pausa e upgrades globais, movimento e HUD numa build Linux de Development.
4. **Validar o mapa integrado** em `Game`: conferir visual, spawns, colisão dos jogadores e caminhos dos inimigos; medir o custo dos 5.344 colliders estáticos e simplificar se necessário.
5. **Polir o menu e ajustar o balanceamento** após os testes. A tela cheia e os VFX estão ligados ao fluxo atual.
6. **Seguir para os próximos sistemas:** fim de partida/reinício, seleção de arma travada durante a partida e áudio.

## 8. Decisões

Tomadas:
- **Autoridade:** movimento autoritativo no servidor, sem predição no client.
- **Mira:** independente do movimento (mouse ou analógico direito).
- **Spawn de inimigos:** fluxo contínuo, como ponto de partida.
- **Armas:** as três ficam disponíveis como arma inicial. Todas miram o inimigo mais próximo e só disparam com inimigo no raio.
- **XP:** individual por jogador. Acima de 300 gemas ativas, o XP é somado à gema mais antiga.
- **Pausa:** `Esc` alterna uma pausa global replicada; upgrades são individuais e a partida retoma quando todos os jogadores conectados escolhem. Desconexões são retiradas da fila.
- **Melhorias:** a cada 30 mortes globais, cada jogador escolhe +5% de dano, +5% de vida máxima ou cura completa.
- **Armas e VFX:** armas baixas em polígonos ficam presas à mão; efeitos locais de disparo e impacto são reaproveitados pelo pool.
- **NavMesh:** consultas de caminho (`NavMesh.CalculatePath`) em vez de `NavMeshAgent`, para manter o `CharacterController` e a replicação.
- **Menus:** Coop (criar/entrar), vídeo, mapa (um por enquanto), seleção de personagem/arma inicial e projéteis com efeitos de disparo/rastro/impacto. A primeira versão de Coop/vídeo está em `Docs/Menu.md`; VFX e validação em Play Mode continuam pendentes.

Pendentes:
- **Mapa:** `VK_ValleyTown` está aplicado à cena `Game`; validar visualmente e em Play Mode. A solicitação anterior de uma casa contemporânea continua sem asset correspondente.
- **Validação Host–Client e balanceamento:** as mecânicas novas foram compiladas, mas ainda não testadas em partida.

## 9. Limitações conhecidas

- A troca de arma é permitida a qualquer momento; ainda não há trava após o início da partida.
- Quando o `EnemySpawner` é despublicado (fim de partida), os inimigos removidos também geram gemas.
- O spawner apoia inimigos no chão por raycast, mas não verifica se o ponto está no NavMesh, então um inimigo pode nascer numa área isolada.
- No client, o visual de um projétil atravessa a parede até chegar o RPC de fim (cerca de um RTT).
- O shader PSX não recebe sombras nem luzes adicionais, por escolha estética.
- O Client sente a latência de ida e volta no próprio movimento (sem predição).
