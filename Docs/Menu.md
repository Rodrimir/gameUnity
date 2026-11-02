# Menu, preparação de partida e projéteis

Escopo solicitado pelo usuário em 02/11/2026. **Primeira versão integrada à cena `Game` e conferida visualmente em Play Mode.** A escala agora faz o painel inteiro caber na prévia PSX do Editor. Os cliques, a conexão Client e o refinamento visual ainda estão pendentes. Os controles de debug antigos ficam desativados nessa cena.

## Fluxo

1. **Menu principal:** Jogar Coop, Vídeo e Sair.
2. **Coop:** criar partida como Host ou entrar informando IP/porta do Host; mostrar estado da conexão e erros compreensíveis. Duas pessoas, rede local, UDP/7777.
3. **Preparação/lobby:** escolher personagem e arma inicial; o Host escolhe o mapa e inicia a partida. Mostrar o único mapa disponível enquanto só houver um. O chão atual é uma arena de teste, até a chegada do mapa definitivo.
4. **Partida:** manter as escolhas aprovadas no servidor, com o mesmo catálogo e mapa em ambos os peers. A escolha inicial de arma deve ser travada ao iniciar; upgrades continuam sendo um sistema separado.

## Personagens e armas

- Personagens: Survivalist, Adventure Female e Gravemind Dreadnought. Biomechanical Mutant é exclusivamente inimigo.
- Armas iniciais: Varinha Clássica (teleguiada), Rajada em Leque e Projétil Perfurante.
- Mostrar descrição e prévia do disparo antes da confirmação.
- Cliente envia a intenção de escolha; servidor valida o catálogo e replica a seleção. Dano, cadência, colisões e trajetória autoritativa continuam no servidor.

## Vídeo

Resolução da janela/monitor, janela ou tela cheia, VSync e opções PSX (resolução interna, vertex snapping, intensidade afim). A resolução de saída e a resolução interna PSX são controles distintos. Preferências locais devem usar os diretórios do projeto configurados pelo launcher, respeitando a restrição de gravação do usuário.

### Estado da primeira versão

- Coop Host e Client por IP, UDP/7777, com limite de dois jogadores.
- Seleção inicial dos três personagens e das três armas existentes; o servidor valida a arma e mantém o catálogo igual em todos os peers.
- O único mapa é mostrado como arena de teste, sem seletor fictício.
- Resolução de saída, janela/tela cheia, VSync e opções PSX (320×240, 426×240 ou 640×360, vertex snapping e intensidade afim) são ajustáveis localmente. As opções ainda não são persistidas.
- A escala da UI é reduzida no Editor segundo a resolução interna PSX, para manter o painel inteiro visível no Game View. No jogo compilado, a escala parte da resolução de saída.
- O botão Sair encerra o aplicativo fora do Editor.
- A interface ainda não cobre lobby pronto para iniciar, game over ou efeitos de disparo, rastro e impacto.

## Estilo dos projéteis e efeitos

Cada arma precisa apresentar um estilo visual reconhecível, com efeito de disparo, rastro e impacto. Os comportamentos teleguiado, leque e perfuração já são suportados por `WeaponDefinition`.

Os efeitos são visuais e usam pooling local em cada peer; não devem adicionar um NetworkObject por partícula/projétil. Usar os eventos `OnVolleyFired` e `OnProjectileImpact` para os efeitos, sem interferir na simulação autoritativa. Materiais, partículas e brilho devem acompanhar o estilo PSX. Confirmar escolhas artísticas antes de fechar os efeitos definitivos.

## Ordem de execução

Com a arte adaptada e os testes aprovados, montar o menu/lobby e as opções de vídeo, conectar os catálogos de mapa/personagens/armas e acrescentar as prévias/efeitos. Integrar o Bloco 4 quando chegar e validar o fluxo completo Host–Client em uma build Linux.
