# Bloco 3 — Ataque automático e projéteis de rede

## Fluxo de autoridade

`AutoAttackSystem (servidor) → arma equipada → inimigo mais próximo no raio → rajada de Projectiles do pool local → SphereCast por frame → IDamageable.ApplyDamageServer → EndShotRpc → clients`.

- **Alvo e cadência no servidor.** A cada `Fire Interval` da arma, o `AutoAttackSystem` percorre `EnemyAI.ActiveOnServer` e escolhe o inimigo mais próximo no plano XZ dentro de `Attack Radius`. Sem alvo, a arma fica engatilhada e dispara assim que um inimigo entra no raio. Atrasos não geram rajadas de compensação.
- **Projéteis não são NetworkObjects.** Em um bullet heaven, replicar Transform de centenas de projéteis custaria muita banda. O servidor envia **um RPC por rajada** (`ProjectileVolley`): id inicial, arma, quantidade e abertura do leque, origem, direção, velocidade, tempo de vida, curva do teleguiado, alvo e `ServerTime`. Cada client retira os visuais do **próprio** `ObjectPoolManager` e os adianta pelo tempo de trânsito (`ServerTime atual − FireTime`), reproduzindo também a curva do teleguiado. Assim o visual aparece onde o projétil está no servidor.
- **Colisão só no servidor.** O projétil autoritativo varre o trecho percorrido no frame com `Physics.SphereCastNonAlloc` e processa os contatos em ordem de distância, então não atravessa alvos em FPS baixo. Jogadores e outros projéteis são ignorados. Inimigos (ou qualquer `IDamageable`) recebem dano; o cenário sempre encerra o projétil. Quando o projétil para, um `EndShotRpc` remove o visual nos clients e ele volta ao pool. Ao fim do tempo de vida, servidor e clients expiram sozinhos, sem RPC.
- **Host.** A instância simulada pelo servidor também é o visual; os RPCs usam `SendTo.NotServer`.
- **Ordem garantida.** Disparo e fim usam entrega confiável, então um fim nunca chega antes do disparo correspondente.
- **Observer.** `OnWeaponChanged` (todos os peers; HUD), `OnVolleyFired` (todos; SFX/muzzle flash), `OnProjectileImpact(Vector3)` (todos; VFX de impacto) e `OnServerProjectileHit(GameObject, Vector3, float dano)` (servidor; números de dano/estatísticas).
- **Bloco 4.** `IDamageable` é o contrato que o `HealthComponent` vai implementar. Até lá os projéteis acertam e perfuram, mas os inimigos não perdem vida.

## Armas iniciais

Cada arma é um asset `WeaponDefinition` (**Create → GameYunit → Combat → Weapon Definition**). Os comportamentos são parâmetros combináveis, não classes separadas: um upgrade pode, por exemplo, dar perfuração a um leque. As três armas iniciais ficam em `Assets/_Project/ScriptableObjects/Weapons`:

| Asset | Comportamento | Intervalo | Raio | Dano | Velocidade × vida | Parâmetros próprios |
|---|---|---|---|---|---|---|
| `Weapon_HomingWand` — Varinha Clássica | Teleguiado, não erra | 0,6 s | 12 | 12 | 12 × 2,0 s | curva `720°/s`; a menos de 1,5 m aponta direto |
| `Weapon_FanBurst` — Rajada em Leque | 5 projéteis retos em cone | 1,0 s | 10 | 6 cada | 18 × 0,8 s | `5` projéteis, abertura `60°` |
| `Weapon_PiercingBolt` — Projétil Perfurante | Atravessa inimigos | 0,9 s | 14 | 15 | 24 × 1,0 s | `3` perfurações, dano `×0,75` por inimigo atravessado |

- **Mira.** As três miram o inimigo mais próximo (`Aim = NearestEnemy`). `PlayerFacing` dispara para a frente do personagem, usando a mira independente do Bloco 1. A arma continua só disparando quando há um inimigo no raio.
- **Teleguiado.** Persegue o alvo escolhido no disparo. Se o alvo sumir, segue reto. Depois de acertar, também segue reto (relevante quando combinado com perfuração).
- **Perfuração.** Cada inimigo é atingido no máximo uma vez por projétil.
- **Seleção.** A arma equipada é uma `NetworkVariable<int>` escrita só pelo servidor. O dono chama `AutoAttackSystem.RequestWeapon(índice)`, e o servidor valida o índice. Enquanto não existe a tela definitiva, use o **WeaponSelectDebugHUD** (botões por arma, só no Editor/Development Build). A troca hoje é permitida a qualquer momento; travá-la após o início da partida fica para a tela de seleção.

## Setup na Unity

1. Crie `Assets/_Project/Prefabs/Projectiles/Projectile.prefab`: uma **Sphere** com escala `0.3` e o componente **Projectile**. **Não** adicione NetworkObject, Rigidbody nem NetworkTransform. O SphereCollider da primitiva é desligado automaticamente no `Awake`.
2. **Não** registre o projétil em Network Prefabs nem no `NetworkObjectPool`: ele usa apenas o `ObjectPoolManager` local. Se quiser evitar o primeiro Instantiate, adicione o prefab em **Prewarm On Awake** do `ObjectPoolManager`; o `AutoAttackSystem` também pré-aquece `Prewarm Count` instâncias no spawn.
3. No `Player.prefab`, adicione **AutoAttackSystem**:
   - em **Weapons**, coloque os três assets na ordem Varinha, Leque, Perfurante (a ordem é o índice sincronizado; mantenha-a igual em Host e Client);
   - escolha **Starting Weapon Index**;
   - atribua **Projectile Prefab** como visual padrão. Cada `WeaponDefinition` pode ter um visual próprio em **Projectile Prefab**, que tem prioridade;
   - o disparo sai a `1` m de altura e `0.7` m à frente.
4. **Hit Mask** do Projectile: deixe em *Everything* ou exclua camadas puramente decorativas. Paredes e cenário com Collider param o projétil.
5. Adicione **WeaponSelectDebugHUD** ao mesmo GameObject do `NetworkDebugHUD` para trocar de arma durante os testes.

## Mapa

O mapa a ser usado na cena inicial ainda não foi adicionado ao repositório. Para qualquer mapa importado:

1. Arraste o modelo/prefab do mapa para a cena e **posicione-o de forma que o chão fique em torno de `Y = 0`**.
2. Com a raiz selecionada, use **GameYunit → Mapa → Preparar mapa selecionado**:
   - adiciona `MeshCollider` a cada malha sem collider (colliders feitos à mão são preservados);
   - marca as malhas como estáticas;
   - cria `PlayerSpawnPoint_0` e `_1` no centro, apoiados no chão, se a cena ainda não tiver spawn points.
3. Mova os **PlayerSpawnPoint** para uma área aberta: pivot no chão, eixo +Z para a direção inicial. O servidor usa o slot `0` para o Host e o `1` para o segundo jogador, via aprovação de conexão. Sem nenhum spawn point, todos nascem na origem, como antes.
4. O **EnemySpawner** agora apoia cada candidato no chão com um raycast de cima para baixo (`Ground Probe Height = 10`, `Ground Probe Depth = 20`). Ele descarta pontos sem chão (fora do mapa) e pontos onde a cápsula do inimigo (`0.5 × 2`) não cabe. Restrinja **Ground Mask** às camadas de cenário se houver colliders que não devem servir de chão. Em mapas com teto (interiores), reduza `Ground Probe Height` para que o raio não pouse no telhado.
5. O desvio de obstáculos pela IA (NavMesh) continua previsto para o Bloco 6. Por enquanto os inimigos andam em linha reta e colidem com paredes.

## Verificação

`CombatNetworkTests` inicia uma sessão Host real para cada caso e verifica:
- **Reto:** sem inimigos, nenhum disparo; com um inimigo a 6 m, o projétil o atinge com o dano configurado; após o despawn do inimigo, todos os projéteis voltam ao pool; `EnemyAI.ActiveOnServer` acompanha spawn e despawn.
- **Leque:** uma rajada cria 5 projéteis, com as pontas a 30° da direção central.
- **Perfurante:** um projétil atinge dois inimigos alinhados, com dano 20 e depois 10 (`falloff 0.5`).
- **Teleguiado:** disparando para a frente com o alvo a 90°, o projétil faz a curva e acerta.
- **Seleção:** o Owner troca de arma pelo servidor; índice inválido é recusado.

**Estado:** estes scripts **ainda não foram compilados nem executados**. A máquina em que foram escritos não tem Unity. Rode o Test Runner (EditMode) na instalação 6000.3.27f2 antes do teste manual.

Teste manual Host (Editor) + Client (build Linux):

- Os dois jogadores disparam sozinhos contra o inimigo mais próximo, cada um independente do outro. Os dois peers veem todos os projéteis.
- Troque de arma no HUD, no Host e no Client: os dois peers devem ver a curva da Varinha, o leque de 5 e o perfurante atravessando a fila de inimigos.
- No Client, os projéteis do Host saem já um pouco à frente do cano: é a compensação de latência. Eles somem no mesmo ponto em que acertam no servidor.
- Projéteis param em paredes do mapa e nunca acertam jogadores.
- Desconecte o Client com projéteis em voo: nada fica congelado na cena do Host.
- Com o mapa: os jogadores nascem nos spawn points e nenhum inimigo nasce dentro de paredes ou fora do chão.

## Próxima decisão

As três armas iniciais (Varinha Clássica, Rajada em Leque e Projétil Perfurante) estão disponíveis para escolha. O Bloco 4 (vida, dano e morte) aguarda confirmação.
