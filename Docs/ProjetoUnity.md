# Projeto Unity e personagens

O projeto Unity fica na raiz deste repositório, com Unity **6000.3.27f2**, URP **17.3.0**, NGO **2.13.3**, Transport **2.7.4** e Input System **1.20.1**. Os pacotes estão fixados em `Packages/manifest.json` e `packages-lock.json`.

## Abrir e testar

Execute `Tools/unity.sh`. O menu **GameYunit → Projeto → Ver personagens** abre a cena `Characters`; **Abrir cena de jogo** abre `Game`.

Depois de qualquer alteração visível (cena, prefab, material ou UI), feche a instância aberta e reabra uma só, já na cena atualizada: `Tools/unity.sh --reopen-game` abre `Game`, `--reopen-preview` abre `Characters` e `--reopen-play` inicia `Game` em Play Mode para inspecionar o menu. O launcher aguarda a janela fechar e o `Temp/UnityLockfile` ser liberado; se o lock continuar ativo, ele para sem iniciar outra instância. Mudanças só em código ou documentação não precisam desse reinício.

`Tools/unity.sh --verify-and-open` executa os testes da assembly `GameYunit.Editor.Tests` e abre o Editor na cena dos personagens após a aprovação dos testes. Usa gráficos também nos testes, para validar a RenderTexture PSX da cena real. Resultados e logs ficam em `Logs/`.

O launcher utiliza a instalação já existente em `/mnt/SSD/Unity/editors/6000.3.27f2/Editor/Unity`, em modo somente leitura. Downloads de pacotes, cache, preferências, licença copiada, logs e temporários permanecem dentro do projeto, em `.unity/`, `Library/`, `Temp/` e `Logs/`. O sistema de arquivos externo fica somente leitura; sockets do desktop são acessados para abrir a janela. `HOME` não é alterado. Não use o launcher global da instalação, pois ele grava em outras pastas.

Nesta instalação, o depurador identificou uma falha do módulo de entrada GTK Wayland ao iniciar a janela X11. O launcher fixa GTK/SDL em X11 e `GTK_IM_MODULE=xim`; a janela do Editor abriu com essa configuração.

## Modelos e papéis confirmados

| Modelo | Papel |
|---|---|
| Biomechanical Mutant | Único inimigo |
| Survivalist | Personagem selecionável |
| Adventure Female | Personagem selecionável |
| Gravemind Dreadnought | Personagem selecionável |

Os ZIPs originais permanecem localmente em `modelo-jogo/` e os FBX/texturas-fonte em `Assets/_Project/Art/Models/Characters/**/Source/`; ambos são ignorados pelo Git por causa do tamanho. Esses arquivos não são removidos do projeto atual, mas precisam ser fornecidos à parte numa instalação limpa para restaurar a arte. `Tools/import_models.py` importa somente arte necessária das versões Unity, preservando os GUIDs. Não importa scripts de controle, cenas de demonstração nem projetos Unreal. Os relatórios `modelo-jogo/*-import.json` registram as origens.

Arte de origem: `Assets/_Project/Art/Models/Characters/<modelo>/Source/`.

Prefabs adaptados: `Assets/_Project/Prefabs/Characters/*Visual.prefab`. Os materiais usam `GameYunit/PSX Lit`; a importação limita texturas a 256 pixels, filtro Point e sem mipmaps. Os modelos são ajustados à altura do controlador, com pivot nos pés, e seus colliders originais são removidos. Os controladores de rede e a colisão continuam no objeto raiz do jogador/inimigo.

Survivalist e Adventure Female usam animações humanoides de idle/corrida fornecidas no pacote Survivalist. Gravemind usa seus próprios clipes de idle/caminhada. `CharacterAnimationDriver` só atualiza o visual usando o deslocamento replicado e mantém root motion desligado. O pacote Biomechanical não inclui clipes de locomoção: a navegação funciona, mas o modelo ainda precisa dessas animações.

## Cena de teste e menu

Abra `Game` e pressione Play. O menu oferece Coop Host/Client por IP, escolha de personagem e arma inicial, além de resolução, tela cheia, VSync e parâmetros PSX. A cena mostra apenas o mapa de teste disponível. O painel completo foi conferido visualmente no Game View em Play Mode; os cliques e a conexão Client ainda precisam de validação. VFX de projéteis seguem pendentes. Veja [o escopo e estado do menu](Menu.md).

Ao iniciar como Host, o Host escuta UDP/7777 em todas as interfaces; Client conecta ao IP informado. Movimento: WASD/setas ou analógico esquerdo; mira: mouse/analógico direito. A seleção de personagem e arma é replicada pelo servidor. Os controles de debug antigos ficam desativados nessa cena.

A cena inclui chão de teste com NavMesh, dois pontos de spawn, jogador com ataque automático/XP, inimigos Biomechanical em pool e gemas. Uma build Linux de Development pode conectar como Client usando `127.0.0.1:7777` no mesmo computador.

O **Bloco 4 continua pendente com a outra pessoa**. Os monstros perseguem o jogador e recebem colisões de projéteis, mas ainda não perdem vida nem morrem por dano. O drop/coleta de XP pode ser validado pelos testes existentes ou com o despawn no servidor. Não foi criado um sistema alternativo de vida.

`GameYunit → Projeto → Montar protótipo e personagens` só faz a montagem inicial se as cenas não existirem; preserva cenas editadas posteriormente. A prévia visual pode ser salva em `Logs/Characters.png` pelo menu **Salvar prévia dos personagens**.
