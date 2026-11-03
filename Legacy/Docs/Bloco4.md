# Bloco 4 — Vida, dano e eventos

## Estado da integração

A base de vida, morte de inimigos e HUD local está integrada ao sistema de combate deste checkout. `HealthComponent` mantém a API autoritativa de dano, cura e morte e implementa `IDamageable`; o projétil aplica dano pelo contrato, e `EnemyAI` remove o inimigo morto através do pool.

## Autoridade e Observer

`HealthComponent` mantém vida e vida máxima em `NetworkVariable<int>` com leitura para todos e escrita exclusiva do servidor. `TakeDamageServer(int)` e `HealServer(int)` recusam chamadas de clients, valores não positivos, objetos não spawnados e personagens mortos. O dano para em zero; a cura para no máximo, inclusive com valores extremos como `int.MaxValue`. `ApplyDamageServer(float, ...)` adapta o contrato do combate para a mesma implementação.

O evento C# `OnHealthChanged(previous, current)` repassa as alterações da NetworkVariable em cada peer. Quem assina lê `CurrentHealth` imediatamente para obter o estado inicial. O evento `OnDeath(HealthComponent)` é emitido somente no servidor, uma vez por vida; cura comum não ressuscita. A vida é restaurada em `OnNetworkSpawn`, inclusive após reutilização do pool. `Max Health` é um atributo fixo do prefab neste bloco; não há alteração dinâmica desse máximo pela rede.

`EnemyAI` assina `OnDeath`, publica o despawn e deixa o handler de rede devolver a instância ao pool em cada peer. A IA remove a assinatura no despawn e deixa de selecionar jogadores mortos. `PlayerController` impede movimento horizontal e rotação a zero HP, mantendo a gravidade. A política de respawn/reanimação do jogador ainda não foi definida.

`PlayerHealthUI` acompanha os eventos locais de `PlayerNetwork`, aceita apenas `IsClient && IsOwner` e assina `OnHealthChanged` do jogador correto. Atualiza um `Slider` com `SetValueWithoutNotify`, sem RPC nem polling. Um `CanvasGroup` esconde a barra quando não há jogador; o GameObject permanece ativo para receber eventos de um novo spawn. Reativar o HUD recupera imediatamente o valor atual.

## Configuração na Unity

1. **HealthComponent** já está na raiz dos prefabs de **Player** (100 HP) e **BiomechanicalEnemy** (60 HP). Os componentes PlayerController e EnemyAI declaram essa dependência. Recompile Host e Client com os mesmos componentes e na mesma ordem: adicionar um NetworkBehaviour altera o layout de rede do prefab.
2. Mantenha o inimigo registrado tanto em **Network Prefabs** quanto no **NetworkObjectPool**, conforme o Bloco 2. Não devolva uma instância ainda spawnada diretamente ao ObjectPoolManager.
3. Instale/ative **Unity UI (uGUI)** no Package Manager. `GameYunit.Runtime` e os testes agora referenciam a assembly `UnityEngine.UI`.
4. Em um **Canvas local** da cena, crie um **UI → Slider** e adicione **PlayerHealthUI** ao próprio Slider. **CanvasGroup** é adicionado como dependência. Pode remover o Handle visual; a barra não recebe interação. Não coloque o Canvas dentro do prefab de rede do jogador.
5. Mantenha a barra ativa na cena. O script controla seu preenchimento e visibilidade. Não é necessário atribuir manualmente uma referência de jogador no Inspector.

## Integração com Projectile

Após resolver a colisão no servidor e identificar o `IDamageable` do alvo, o `AutoAttackSystem` aplica dano pelo contrato antes de devolver o projétil ao pool. Perfuração, filtros de alvo, consumo do projétil e previsão visual permanecem sob as regras do Bloco 3; o Client não altera a vida.

## Validação realizada

O relatório do Bloco 4 registra **14 testes aprovados** na Unity **6000.3.27f2**, com **NGO 2.13.3**, **Unity Transport 2.7.4** e **uGUI 2.0.0**. Os testes cobrem vida, pool, replicação e HUD; não foram reexecutados nesta integração.

Além dos testes anteriores, `HealthNetworkTests` entra em Play Mode e verifica:

- limites de dano/cura, rejeição de valores inválidos e morte única;
- morte de inimigo, retorno ao pool e restauração da vida na mesma instância;
- Host e Client reais, conectados por loopback, com replicação da NetworkVariable;
- recusa de dano/cura iniciados no Client;
- atualização da barra apenas pelo jogador local e isolamento da vida do outro jogador;
- desativação/reativação do HUD, HP zero replicado, morte decidida só pelo servidor e limpeza ao desconectar.

A integração é coberta pelos testes de rede registrados neste bloco. A partida final e o fluxo Host–Client ainda precisam de validação visual nesta cena, incluindo a barra acima dos personagens.

## Decisões seguintes

Após concluir a integração do projétil, podemos definir a morte no co-op: **reanimação pelo parceiro**, **respawn com atraso** ou **derrota quando os dois morrerem**. O Bloco 5, de XP e níveis, aguarda confirmação.
