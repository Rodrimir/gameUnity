# Bloco 1 — Movimentação, câmera e sincronização

## Fluxo de autoridade

`Input System (IsClient + IsOwner) → PlayerNetwork → RPC confiável → validação no servidor → evento C# → PlayerController.FixedUpdate → CharacterController.Move → NetworkTransform → clients`.

O client transmite duas intenções `Vector2`: movimento e mira no plano XZ. Velocidade, gravidade, colisões, posição e rotação são decididas pelo servidor, inclusive para o jogador do Host. O `CharacterController` fica desativado nos clients para não disputar a posição com a replicação. Não há predição de movimento neste bloco: o Client percebe a latência de ida e volta, acrescida da interpolação.

`PlayerNetwork` normaliza diagonais, preserva a intensidade do analógico e rejeita NaN/Infinity. Mudanças são enviadas imediatamente; o estado também é reenviado a cada 0,2 s. Sem input por 1 s, o servidor zera a intenção. A perda de foco e a desativação do componente também interrompem a intenção. Cada jogador local usa uma cópia própria da action, evitando que um despawn remoto desative seus controles.

`CameraController` assina os eventos de spawn/despawn locais e consulta `PlayerNetwork.LocalPlayer` ao ser ativado. Segue somente `IsSpawned && IsClient && IsOwner`, com suavização em `LateUpdate`, ângulo top-down fixo e offset em eixos do mundo. A câmera acompanha respawns e limpa o alvo ao desconectar.

`PlayerAimInput` converte o cursor em uma direção usando um plano horizontal na altura do personagem (`Aim Height = 1`). Essa projeção ignora obstáculos e dispensa Colliders no chão. O analógico direito aponta em relação aos eixos da câmera e tem prioridade enquanto estiver inclinado. Ao soltá-lo, o personagem mantém a orientação até você mover o mouse novamente. Sem mouse ou sem uma projeção válida, a orientação também é preservada. `PlayerController` gira apenas no servidor, com limite inicial de `720°/s`, sem alterar os eixos de movimento.

## Setup na Unity (CachyOS/Linux)

O projeto URP já foi montado nesta raiz, com `Packages`, `ProjectSettings`, Input Actions, cenas e prefabs configurados. Use `Tools/unity.sh` para manter caches e temporários dentro do projeto. A instalação é Unity **6000.3.27f2 (6.3 LTS)**. Veja [Projeto Unity e personagens](ProjetoUnity.md). As instruções abaixo documentam o setup dos componentes e servem de referência para alterações.

1. Instale **Netcode for GameObjects**, **Unity Transport** e **Input System** pelo Package Manager. O código existente usa `[Rpc]`, disponível a partir do NGO 1.8; escolha uma versão compatível com seu Editor. Em **Player → Other Settings → Active Input Handling**, selecione **Input System Package (New)** ou **Both**. Ative **Run In Background** para que o Host continue simulando quando outra janela receber foco.
2. Crie um asset de Input Actions em `Assets/_Project/Settings/Input`. Adicione a action `Player/Move`, tipo **Value**, controle **Vector2**. Configure um **2D Vector Composite** com W/S/A/D, outro com setas e um binding `<Gamepad>/leftStick`. Use o modo **Digital Normalized** nos composites. Atribua `Player/Move` ao campo **Move Action** do `PlayerNetwork`. Não é necessário adicionar o componente `PlayerInput`.
3. Crie um GameObject `Player` com escala `(1, 1, 1)` e os componentes **NetworkObject**, **PlayerNetwork**, **PlayerAimInput**, **CharacterController**, **NetworkTransform** e **PlayerController**. Use `CharacterController`: Height `2`, Radius `0.5`, Center `(0, 1, 0)`, Step Offset `0.3`, Skin Width `0.08` e Min Move Distance `0`. O pivot fica nos pés. Crie uma cápsula visual filha em `(0, 1, 0)` e remova seu `CapsuleCollider`; a colisão é responsabilidade do `CharacterController`. Adicione um pequeno cubo visual na frente (`+Z`) para enxergar a orientação da cápsula. Salve em `Assets/_Project/Prefabs/Player/Player.prefab`. Em **PlayerAimInput**, a action de mira é opcional: por padrão usa `<Gamepad>/rightStick`. Para personalizar bindings, crie `Player/Aim` do tipo **Value / Vector2**, vincule ao analógico direito e atribua em **Aim Action**.
4. Use o **NetworkTransform padrão**, com autoridade **Server** (se houver esse seletor na versão instalada), posição X/Y/Z e **rotação Y** sincronizadas, **Interpolate** ativo e **In Local Space** desativado. Desative a sincronização de escala e de rotação X/Z. Não adicione `ClientNetworkTransform` ou outro script que escreva a posição/rotação no client. O padrão server authoritative é descrito na [documentação da Unity](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@1.13/manual/components/networktransform.html#authority-modes).
5. Na cena, crie o GameObject de rede com **NetworkManager**, **UnityTransport**, **NetworkManagerSetup** e **NetworkDebugHUD**. Atribua o prefab a **NetworkConfig → Player Prefab** e confira seu registro em **Network Prefabs**. Use um Tick Rate inicial de `30`. Não deixe uma instância extra do jogador na cena: o NGO cria o player de cada conexão.
6. Adicione **CameraController** à **Main Camera** da cena e mantenha sua tag **MainCamera**, usada para localizar a câmera da mira. Mantenha um único **AudioListener**, nessa câmera. A câmera não deve ser filha do prefab de jogador e não precisa de NetworkObject. O offset inicial é `(0, 12, -8)` e o ponto observado é `(0, 1, 0)` acima do pivot. Com esse enquadramento, W avança em `+Z`, D em `+X`; o movimento não depende da rotação do personagem.
7. Crie um chão com Collider em `Y = 0` e uma parede com Collider para verificar colisões. Deixe a origem `(0, 0, 0)` livre para o spawn inicial do NGO. Os jogadores começam na mesma origem neste setup mínimo; afaste o Host antes de conectar o segundo jogador. Salve a cena em `Assets/_Project/Scenes` e inclua-a no Build Profile/Build Settings.

## Teste Host–Client no Linux

Faça uma build para **Linux x86_64**. Inicie o Host no Editor e o Client na build usando o HUD. Na mesma máquina, mantenha `Connect Address` e `Listen Address` em `127.0.0.1`, porta `7777`. O jogador só lê controles enquanto sua janela está em foco; o Host continua simulando em segundo plano com **Run In Background** ativo.

Em duas máquinas na LAN, configure o Host com **Listen Address = 0.0.0.0** e aponte **Connect Address** do Client para o IPv4 do Host. O `UnityTransport` já presente no projeto usa **UDP/7777 sobre IP**; o setup não implementa transporte TCP. Se o firewall estiver ativo, permita a porta UDP configurada na interface da LAN.

- Movimente os dois jogadores separadamente usando WASD e, se disponível, o analógico. Cada janela deve controlar e acompanhar apenas seu próprio personagem; ambos devem enxergar o outro se mover.
- Mire com o mouse enquanto anda para outra direção. Repita com analógico esquerdo para andar e direito para mirar. Ambos devem enxergar a rotação do outro jogador. Solte o analógico direito com o mouse parado: a mira deve permanecer; mova o mouse para voltar a usá-lo.
- Compare movimento lateral e diagonal: a diagonal não deve aumentar a velocidade, e o analógico deve preservar intensidades parciais.
- Caminhe contra a parede, suba um degrau baixo e saia da borda do chão. O servidor deve impedir atravessar obstáculos e aplicar gravidade.
- Segure uma direção e troque de janela. O personagem deve parar; o Host deve continuar atualizando o mundo. Sem novos comandos, o timeout do servidor também interrompe o movimento.
- Ative a câmera depois do spawn e desative/reative seu componente: ela deve recuperar o jogador local. Desconecte e conecte novamente: a câmera deve trocar para o novo jogador sem manter o anterior.
- No Client, confira que o CharacterController está desativado e que o NetworkTransform recebe a posição. A velocidade do jogo não deve depender do FPS do Client.

## Decisão adotada

A opção escolhida foi **mira independente com mouse ou analógico direito**. O formato do snapshot de input agora inclui `Aim`; Host e Client devem ser recompilados juntos. A continuação de hordas está descrita no [Bloco 2](Bloco2.md).

## Validação realizada

Os scripts do repositório foram importados e compilados em um projeto temporário com **Unity 6000.3.27f2**, **NGO 2.13.3**, **Unity Transport 2.7.4** e **Input System 1.20.1** no CachyOS. As assemblies `GameYunit.Core` e `GameYunit.Runtime` foram geradas, incluindo o processamento de RPCs pelo NGO, e o Editor encerrou com código `0`. A partida com duas instâncias e os testes de física/câmera acima ainda precisam ser executados na cena configurada.

Após a escolha da mira independente, **11 testes EditMode passaram** na mesma instalação. Cobrem projeção do mouse, direção do analógico relativa à câmera, zona morta, troca entre mouse/gamepad, desativação do input, validação independente das intenções e serialização de movimento/mira. Os testes ficam em `Assets/_Project/Tests/Editor`; para executá-los, instale **Unity Test Framework** e use **Window → General → Test Runner → EditMode**. Eles usam o [InputTestFixture do Input System](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.20/manual/Testing.html) para simular os dispositivos.
