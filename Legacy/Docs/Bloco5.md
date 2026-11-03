# Bloco 5 — Drop de XP e sistema de níveis

## Isolamento em relação ao Bloco 4

O Bloco 4 (vida, dano e morte) está sendo desenvolvido em paralelo. Para evitar conflitos de merge, este bloco **só adiciona arquivos novos**: `Scripts/Progression/*`, `Scripts/UI/Debug/LevelDebugHUD.cs`, `Tests/Editor/ProgressionNetworkTests.cs` e este documento. Nenhum arquivo existente foi alterado; o `README` também ficou intacto e deve receber o link deste documento depois do merge.

O único ponto de contato é uma API pública que já existe desde o Bloco 2: **`EnemyAI.OnServerDespawned`**. Pelo roteiro do Bloco 4, a morte faz o `EnemyAI` chamar `ReturnToPoolServer()`, que gera esse despawn. Portanto o drop de XP passa a funcionar sozinho quando o Bloco 4 entrar. Até lá, qualquer chamada a `ReturnToPoolServer()` no servidor já gera uma gema, o que permite testar este bloco isoladamente.

Se o Bloco 4 adotar outro caminho de morte (ou se surgirem despawns que não são morte), desligue **Drop On Server Despawn** no `XpDropper` e chame `XpDropper.NotifyKilledServer()` a partir do evento `OnDeath`. É uma linha no código do Bloco 4, sem mudança aqui.

## Fluxo de autoridade

`EnemyAI.OnServerDespawned → XpDropper (pedido) → XpDropper.OnServerDropRequested → XpDropService (LateUpdate) → NetworkObjectPool.Spawn(XpGem) → trigger no servidor → PlayerLevel.AddXpServer → NetworkVariables → OnXpChanged / OnLevelUp em todos os peers`.

- **XpDropper** (no prefab do inimigo) só publica um pedido com prefab, posição e valor; não conhece o pool nem a rede.
- **XpDropService** (no GameObject do NetworkManager) atende os pedidos no servidor. Os pedidos são enfileirados porque nascem dentro do despawn do inimigo, e publicar outro NetworkObject no meio desse callback é frágil. Acima de **Max Active Gems** (300), o XP é somado à gema ativa mais antiga em vez de criar outra.
- **XpGem** é um NetworkObject reciclado pelo `NetworkObjectPool` do Bloco 2. Como a gema é estática, não usa NetworkTransform: a posição viaja no spawn, e clients que entram depois também recebem as gemas existentes. O valor existe só no servidor.
- **Coleta física no servidor.** A gema tem um `SphereCollider` trigger e um `Rigidbody` cinemático, configurados no `Awake`. O `CharacterController` do jogador só fica ativo no servidor, e o `OnTriggerEnter` também só é tratado lá. A gema soma o XP, publica o despawn, e o handler do pool a devolve em cada peer.
- **PlayerLevel** guarda `Level` e `Xp` (XP dentro do nível) em NetworkVariables escritas só pelo servidor. XP é **individual**. A curva é linear: para sair do nível N são necessários `Base (5) + Incremento (5) × (N − 1)`, e a sobra passa adiante. Ganhar vários níveis de uma vez dispara um evento por nível.
- **Observer.**
  - `PlayerLevel.OnXpChanged(xp, necessário)` e `OnLevelUp(nível)` disparam em todos os peers.
  - `PlayerLevel.OnAnyPlayerLevelUp(jogador, nível)` é estático e será o ponto de entrada da tela de Level Up.
  - `XpGem.OnServerCollected`, `XpDropService.OnGemSpawned` e `XpDropper.OnServerDropRequested` são só do servidor.

## Setup na Unity

1. Crie `Assets/_Project/Prefabs/Pickups/XpGem.prefab`: um GameObject com o componente **XpGem**. NetworkObject, SphereCollider e Rigidbody são adicionados automaticamente; o trigger e o modo cinemático são configurados em runtime. Adicione um visual filho pequeno (ex.: Cube de escala `0.3`, girado 45°) **sem collider**. Não adicione NetworkTransform.
2. Registre o prefab em **Network Prefabs** do NGO e na lista **Prefabs** do **NetworkObjectPool**, com Prewarm Count `128` como ponto de partida. Faça isso em Host e Client, igual ao inimigo.
3. No GameObject do NetworkManager (junto de `ObjectPoolManager` e `NetworkObjectPool`), adicione **XpDropService**.
4. No `Enemy.prefab`, adicione **XpDropper**, atribua **Gem Prefab** e defina **Xp Value** (`1` por inimigo comum).
5. No `Player.prefab`, adicione **PlayerLevel**.
6. Opcional: adicione **LevelDebugHUD** ao GameObject dos HUDs de debug para ver nível e XP.

## Verificação

`ProgressionNetworkTests` inicia uma sessão Host real e verifica:
- **Curva de XP:** 31 pontos levam do nível 1 ao 4, sobrando 1, com um `OnLevelUp` (e um `OnAnyPlayerLevelUp`) para cada nível 2, 3 e 4.
- **Drop e coleta:** o despawn do inimigo no servidor gera uma gema de valor 3, 0,5 m acima da posição de morte. Ao mover o `CharacterController` do jogador até ela, o XP sobe para 3 e a gema volta ao pool.
- **Teto de gemas:** com `Max Active Gems = 1`, dois drops resultam em uma única gema de valor 6.

**Estado (02/11/2026):** scripts compilados na Unity 6000.3.27f2; os **3 testes de progressão passaram**, dentro da suíte atual de **25 testes aprovados**. Os prefabs e a cena estão montados; veja [Projeto Unity e personagens](ProjetoUnity.md). A integração com a morte real aguarda o Bloco 4.

Teste manual Host (Editor) + Client (build Linux), após o Bloco 4 ou chamando `ReturnToPoolServer()` em inimigos:

- As gemas aparecem nos dois peers na posição da morte e giram localmente.
- Só quem encosta coleta. O XP e o nível de cada jogador aparecem corretos no HUD de ambos.
- Conecte o Client com gemas já no chão: ele deve vê-las.
- Desconecte e reconecte: nenhuma gema fica duplicada ou presa.

## Próxima decisão

Ver as opções de tela de Level Up no multiplayer, apresentadas ao final do bloco.
