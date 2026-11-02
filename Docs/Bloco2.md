# Bloco 2 — Hordas e perseguição no servidor

## Implementação

- `EnemySpawner` é um `NetworkBehaviour` de cena. Somente `IsServer` calcula posições e solicita spawns. Por padrão cria 3 inimigos por segundo, até 128 ativos, em um anel de 12 a 18 unidades. Alterna os jogadores usados como centro do anel e recusa posições a menos de 12 unidades de qualquer jogador. O limite vale para este spawner; o máximo de objetos inativos do pool não limita a população ativa.
- `EnemyAI` seleciona o jogador conectado mais próximo com `Vector3.Distance`, no servidor, a cada 0,25 s. O `CharacterController` aplica perseguição, gravidade e colisões; fica desativado nos clients. `NetworkTransform` replica posição e rotação.
- `NetworkObjectPool` integra o `ObjectPoolManager` existente com `INetworkPrefabInstanceHandler`. O servidor publica o spawn e cada client retira sua representação do próprio pool. O despawn passa pelo handler e devolve o objeto ao pool em cada peer, incluindo o Host. Essa extensão segue o [suporte a pooling do NGO](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/advanced-topics/object-pooling.html).
- O evento C# `OnServerDespawned` desacopla a IA da contagem do spawner; `OnEnemySpawned` permite observar novos inimigos. Não há referências estáticas entre inimigos e spawner.

## Configuração da cena

1. No mesmo GameObject do **NetworkManager**, adicione **ObjectPoolManager** e **NetworkObjectPool**. Mantenha esses serviços vivos até a rede encerrar. O pool base inicializa antes da ponte de rede. Não crie pools duplicados na mesma cena.
2. Crie `Enemy.prefab` em `Assets/_Project/Prefabs/Enemies`, com **NetworkObject**, **NetworkTransform**, **CharacterController** e **EnemyAI** na raiz. Use pivot nos pés, cápsula de altura 2, raio 0,5 e centro `(0, 1, 0)`, com visual filho. A direção frontal é `+Z`. Não adicione Rigidbody. O `PooledObject` é inserido automaticamente pelo pool.
3. No **NetworkTransform**, use autoridade **Server**, sincronização de posição X/Y/Z e rotação Y, interpolação ativa, escala desativada e espaço global. O CharacterController é controlado pelo script; não o reative manualmente nos clients.
4. Registre **o mesmo prefab** na lista **Network Prefabs** do NGO e na lista **Prefabs** do **NetworkObjectPool**, tanto no Host quanto no Client. Para começar, configure **Prewarm Count = 128** e **Max Size >= 128** no pool base. Faça isso antes de iniciar a sessão; o pré-aquecimento também evita criar objetos em massa no primeiro spawn do Client.
5. Na cena, crie um GameObject com **NetworkObject** e **EnemySpawner**. Atribua **Enemy Prefab**; o **Network Pool** pode ser atribuído ou localizado no NetworkManager. Com o gerenciamento de cenas padrão do NGO, este objeto de cena será spawnado ao iniciar a partida. Se você usa gerenciamento de cenas próprio, publique seu NetworkObject explicitamente no servidor.
6. Ajuste **Spawn Interval**, **Enemies Per Spawn**, **Max Active Enemies** e os raios no Inspector. Use um chão plano com Collider cobrindo a área de spawn. A altura inicial acompanha o pivot do jogador; ainda não há amostragem de terreno ou NavMesh.

## Retorno ao pool

Chame **`EnemyAI.ReturnToPoolServer()` no servidor**. Esse método publica `NetworkObject.Despawn(destroy: true)`; o handler registrado intercepta a destruição e recicla o objeto. Não chame `ObjectPoolManager.Release`, `SetActive(false)` ou `Destroy` diretamente em um inimigo ainda spawnado. Posição, alvo, gravidade e timers são reiniciados ao reutilizar a instância.

O spawner libera seus inimigos quando seu NetworkObject recebe despawn. Ao encerrar a sessão, o próprio NGO conduz a limpeza. O sistema de vida/morte será conectado apenas no Bloco 4; por enquanto os inimigos permanecem ativos até uma devolução explícita ou encerramento, e a população para de crescer no limite configurado.

## Verificação

`EnemyNetworkTests` usa o Unity Test Framework para entrar em Play Mode, criar uma sessão Host local com Unity Transport e verificar spawn em anel, limite de população, perseguição, despawn, reuso da mesma instância e limpeza do spawner. Os prefabs temporários do teste são removidos ao terminar. Os testes anteriores de mira/serialização continuam na mesma assembly.

**Resultado:** os **12 testes passaram** na Unity **6000.3.27f2** instalada no CachyOS, com **NGO 2.13.3** e **Unity Transport 2.7.4**, encerrando com código `0`. Esse resultado inclui uma sessão Host real; a replicação para uma instância Client separada e a troca de alvo entre dois jogadores ainda dependem do teste manual da cena abaixo.

Na cena configurada, execute Host no Editor e Client em uma build Linux:

- Os dois peers devem exibir a mesma população e posições; apenas o Host simula os CharacterControllers dos inimigos.
- Afaste os jogadores entre si e confirme que o inimigo troca para o mais próximo. A reavaliação acontece a cada 0,25 s; um jogador desconectado deixa de ser alvo.
- Devolva um inimigo pelo método acima e confirme que ambos os peers o desativam, depois reutilizam uma instância do pool no próximo spawn.
- Conecte um Client com inimigos já ativos e confira sua sincronização inicial. Desconecte, encerre e inicie outra sessão para verificar a limpeza e a reutilização dos handlers.
- Sem PlayerObjects conectados e spawnados, não deve aparecer nenhum inimigo. A população deve estabilizar no limite, sem rajadas extras após frames lentos.

Neste bloco a IA anda diretamente para o alvo e colide com obstáculos; o desvio de caminhos com NavMesh fica para o Bloco 6. A validade do piso e a ausência de obstáculos na posição inicial de spawn devem ser garantidas pelo mapa plano de teste.

## Próxima decisão

Foi adotado **fluxo contínuo** como ponto de partida. Podemos mantê-lo, trocar para **ondas temporizadas** ou usar **fluxo contínuo com picos de horda**. O Bloco 3, de ataque automático e projéteis, aguarda confirmação.
