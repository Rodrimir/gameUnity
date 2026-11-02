# GameYunit — Contexto do projeto

Documento de entrada para quem chega ao projeto, seja uma pessoa da equipe ou um assistente de IA iniciando uma nova sessão. Resume o que existe, como está organizado, o que ainda não foi validado e como continuar. Os detalhes de cada bloco ficam em `Docs/BlocoN.md`; o roteiro original (prompts) está em `PROMPTS.md`.

*Atualizado em 02/11/2026, após montar o protótipo, adaptar os personagens e aprovar 25 testes. A primeira versão do menu Coop/vídeo foi conectada à cena `Game`, mas ainda não foi validada em Play Mode. Alterações locais não commitadas.*

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
| 3 — Ataque automático | `AutoAttackSystem`, `Projectile`, `ProjectileVolley`, `IDamageable`; 3 armas iniciais em `WeaponDefinition` (Varinha teleguiada, Leque de 5, Perfurante); preparo de mapa (`MapSetupTool`, `PlayerSpawnPoint`, spawn de inimigos apoiado no chão) | ✅ Compilado; 5 testes de combate passaram |
| 4 — Vida/dano/morte | `HealthComponent`, `OnDeath`, barra de HP | 🔧 **Em desenvolvimento por outra pessoa**; nada no repositório ainda |
| 5 — XP e níveis | `XpDropper`, `XpDropService`, `XpGem`, `PlayerLevel` (XP individual, curva 5 + 5×(N−1)) | ✅ Compilado; 3 testes de progressão passaram |
| 6 — Mapa/NavMesh/PSX | `PsxRenderTextureSetup` (320x240, Point, letterbox), shader `GameYunit/PSX Lit` (snap + afim + Gouraud), regras de importação retrô, `NavMeshSetupTool`, `NavMeshSteering` | ✅ Compilado; 4 testes de mapa/renderização passaram |
| Personagens e cena | Biomechanical como inimigo; Survivalist, Adventure Female e Gravemind selecionáveis; `PlayerAppearance`, prefabs e cenas `Game`/`Characters` | ✅ Teste da cena real: Host, seleção dos três personagens e spawn do monstro |
| Menu Coop e vídeo | `MainMenuHUD`, IP do Host, escolhas iniciais, resolução de saída e parâmetros PSX | 🟡 Painel inteiro conferido visualmente em Play Mode; cliques e fluxo Client ainda pendentes |

"Compilado e testado" vale para Unity 6000.3.27f2 + NGO 2.13.3. A montagem anterior ao menu teve **25 testes aprovados**, com gráficos habilitados na RTX 3060, incluindo sessões Host reais e a cena `Game`. O menu foi conferido visualmente em Play Mode; seus controles ainda não foram testados interativamente. O teste de renderização corrigiu a limpeza para destruir a câmera antes da RenderTexture. Resultados locais em `Logs/TestResults.xml`; execução reproduzível por `Tools/unity.sh --verify-and-open`. Uma instância Client separada ainda precisa de teste manual.

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

## 6. Projeto Unity e arte

Agora existem `Packages/`, `ProjectSettings/`, os `.meta`, Input Actions, configurações URP e os prefabs Player, BiomechanicalEnemy, Projectile, XpGem e os quatro visuais. A cena `Game` reúne os sistemas num chão de teste com NavMesh e dois pontos de spawn; `Characters` mostra os modelos para inspeção. Veja `Docs/ProjetoUnity.md`.

**Papéis confirmados pelo usuário:** só Biomechanical Mutant é inimigo. Survivalist, Adventure Female e Gravemind Dreadnought são os três personagens selecionáveis. A seleção é validada no servidor e replicada, sem alterar os atributos/controlador. Materiais PSX, texturas Point limitadas a 256 pixels, escala ajustada e root motion desativado. Survivalist e Adventure Female usam animações humanoides do pacote Survivalist; Gravemind usa seus clipes próprios. O pacote Biomechanical não fornece animações de locomoção.

Os ZIPs originais permanecem localmente em `modelo-jogo/`; os arquivos grandes de geometria e textura em `Assets/_Project/Art/Models/Characters/**/Source/` também ficam apenas na máquina. ZIPs, FBX e texturas de origem estão ignorados pelo Git, mas não foram removidos. Uma instalação limpa precisa receber esses arquivos à parte para restaurar a arte. O importador `Tools/import_models.py` seleciona apenas arte das versões Unity e preserva GUIDs; os visuais adaptados ficam em `Prefabs/Characters/`.

**Restrição do usuário:** nenhum download, extração, cache, build, log ou temporário fora desta raiz. Execute a Unity por `Tools/unity.sh`: ele lê o Editor já instalado e restringe gravações ao projeto; `.unity/`, `Library/`, `Temp/` e `Logs/` são ignorados pelo Git. Não use o launcher global da Unity nem projetos temporários externos.

**Fluxo do Editor:** após cada mudança visível, feche graciosamente a instância anterior, aguarde a janela e o lock do projeto fecharem, e reabra apenas uma instância na cena atualizada. Use `Tools/unity.sh --reopen-game` para `Game`, `--reopen-preview` para `Characters`, ou `--reopen-play` para testar o menu; o launcher aborta se o lock continuar ativo. Alterações não visuais não exigem reinício.

Ainda faltam o mapa definitivo, as animações do monstro e o Bloco 4. O protótipo pode ser aberto e iniciado como Host, mas ainda não há vida, dano efetivo ou morte dos inimigos por projéteis.

## 7. Como prosseguir (ordem recomendada)

1. **Revisar e versionar a montagem local.** `Packages/`, `ProjectSettings/`, cenas, prefabs, Input Actions e `.meta` já foram criados. As alterações ainda não foram commitadas. Preserve os ZIPs originais e não versione caches/licenças/logs.
2. **Inspecionar a arte na Unity.** Abra `Tools/unity.sh`, use **GameYunit → Projeto → Ver personagens** e confira materiais, escala e animações. Os testes dos Blocos 3, 5 e 6 já passaram; repita a suíte após mudanças relevantes.
3. **Testar Host–Client em instâncias separadas.** A cena `Game` e seus prefabs já estão montados. Falta validar Host no Editor + Client numa build Linux de Development, incluindo seleção replicada, movimentação e câmera.
4. **Integrar o Bloco 4** quando estiver pronto (contratos da §5). Depois, adicione no `README` os links para `Docs/Bloco5.md` e `Docs/Bloco6.md`; eles ficaram de fora para evitar conflito.
5. **Importar o mapa**, nesta ordem:
   1. GameYunit → PSX → Aplicar importação retrô (pasta do mapa);
   2. GameYunit → Mapa → Preparar mapa selecionado;
   3. GameYunit → PSX → Converter materiais;
   4. GameYunit → Mapa → Configurar e assar NavMesh;
   5. posicionar os `PlayerSpawnPoint`.
6. **Continuar o menu** — primeira versão Coop/vídeo e seleções ligada à cena `Game` e conferida visualmente; falta testar os controles, validar a conexão Client e polir o fluxo. Efeitos de projéteis seguem pendentes — escopo em `Docs/Menu.md`;
7. **Seguir para os próximos sistemas**:
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
- **Menus:** Coop (criar/entrar), vídeo, mapa (um por enquanto), seleção de personagem/arma inicial e projéteis com efeitos de disparo/rastro/impacto. A primeira versão de Coop/vídeo está em `Docs/Menu.md`; VFX e validação em Play Mode continuam pendentes.

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
