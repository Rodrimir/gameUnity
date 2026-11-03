# Magias dos jogadores

Frente aberta a pedido do usuário em 03/11/2026, fora da sequência do [Plano](Plano.md): a Fase 3 previa
combate de cajado corpo a corpo, e as magias passam a ser a forma de ataque. **A Fase 2 ainda não está
fechada** (replicação de movimento sem prova), então nada aqui está verificado em partida.

## Arte

Dois pacotes gratuitos da Asset Store, adicionados à conta do Rodrigo em 03/11/2026:

| Pacote | Id | Uso |
|---|---|---|
| [Magic Effects FREE](https://assetstore.unity.com/packages/vfx/particles/spells/magic-effects-free-247933) | 247933 | VFX de partículas de cada magia |
| [FREE - RPG Fantasy Spell Icons](https://assetstore.unity.com/packages/2d/gui/icons/free-rpg-fantasy-spell-icons-200511) | 200511 | Ícones da escolha no menu |

Como o 3D Game Kit, ficam **fora do Git** (EULA e tamanho) e precisam ser importados por máquina:
**GameYunit → Preparação → Importar efeitos de magia** e **→ Importar ícones de magia** abrem o Package
Manager já no pacote; o download e o import são manuais. Depois,
**GameYunit → Preparação → Vincular arte das magias** religa ícones e VFX às definições.

## Como funciona

- `SpellDefinition` (ScriptableObject): identidade, área, dano, recarga e apresentação de uma magia. É só
  configuração, para o balanceamento ser ajustável sem recompilar e para os dois peers lerem os mesmos números.
- `SpellCatalog`: lista ordenada das magias. **A posição na lista é o que viaja pela rede**, então a ordem
  precisa ser idêntica nas duas máquinas — por isso é um asset versionado, e não uma varredura de pasta.
- `SpellCaster` (em `KitBridge`, por tocar no `PlayerController` do kit): o dono lê o clique esquerdo,
  respeita a recarga e chama o servidor. O servidor valida a recarga, monta a hitbox a partir da **própria**
  cópia do jogador (nunca de coordenadas enviadas pelo cliente) e manda os peers tocarem o VFX.
- A escolha viaja no payload de conexão, igual a nome e cor, e é replicada por
  `NetworkPlayerIdentity.SpellIndex`. Índice fora do catálogo não derruba a conexão: cai na primeira magia.

### Animação

O kit já resolve: `PlayerController` dispara o trigger `MeleeAttack` por `SetNetworkTrigger` no clique
esquerdo, então **o gesto do cajado já replica** pela rede. A conjuração reaproveita esse gesto em vez de
exigir um novo pacote de animação — o animator da Ellen não tem estado de "cast". `CastDelay` casa o efeito
com o ponto do gesto, e a recarga desliga `canAttack` para o clique não animar um golpe sem efeito.

### Hitbox

Montada no servidor por `Physics.OverlapSphereNonAlloc` com `BroadRadius` como filtro barato, seguido do
teste exato de `SpellDefinition.Contains`, que considera forma e altura (para não atingir quem está em
outro andar do cenário). Os alvos saem pelo evento `SpellCaster.TargetsHitServer`, que é o ponto onde
**dano, vida, XP e upgrades** vão se encaixar sem mexer na área nem no balanceamento.

Hoje não há vida, XP nem upgrades no projeto: a vida virá do `Damageable` do kit (hoje desabilitado no
prefab `EllenCoop`), e XP/upgrades são da Fase 4. O `Legacy/` tem `PlayerLevel`, `XpDropService` e `XpGem`
para reaproveitar; o Bloco 4 de vida é de outra pessoa e precisa de alinhamento antes de ser reusado.

## Balanceamento

Orçamento parecido de dano por segundo no caso ideal, com trocas diferentes. O pico supõe acertar o número
máximo de alvos — na prática a Nova de Gelo escala com o tamanho da horda e o Impacto Arcano é o mais
confiável.

| Magia | Área | Dano | Alvos | Recarga | Pico teórico |
|---|---|---|---|---|---|
| Bola de Fogo | esfera 2,2 m a 9 m | 26 | 3 | 2,2 s | ~35/s |
| Nova de Gelo | esfera 4,5 m no conjurador | 14 | sem limite | 3,0 s | ~28/s (6 alvos) |
| Relâmpago | cone 70° e 8 m | 18 | 5 | 2,6 s | ~35/s |
| Impacto Arcano | esfera 2,4 m a 4 m | 15 | 2 | 1,1 s | ~27/s |

Trocas: quem bate mais forte acerta menos alvos ou recarrega mais devagar; quem recarrega rápido tem
alcance curto. Os números vivem nos assets em `Assets/_Project/Spells/` e podem ser ajustados sem recompilar.

## Preparar numa máquina

1. Importe os dois pacotes pelos itens de menu acima.
2. **GameYunit → Preparação → Preparar magias** cria as definições e o catálogo e já tenta vincular a arte.
3. **GameYunit → Preparação → Atualizar cooperativo para magias** religa o prefab `EllenCoop` e a cena
   `CoopMenu` ao catálogo. Necessário onde prefab e cena já existiam, porque `Preparar cooperativo` só cria
   o que falta.

## Não verificado

Nada foi testado em partida: o sistema foi escrito mas os pacotes de arte ainda não tinham sido importados
nesta máquina, então não houve conjuração observada, nem medição de área, nem ensaio em rede com dois
jogadores conjurando.
