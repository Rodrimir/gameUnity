# Fase 1 — Importar e verificar o 3D Game Kit

*Fase encerrada em 02/11/2026, após aprovação explícita do usuário. Preparação e três testes automáticos concluídos; single-player e preparação na máquina do Rodrigo avaliados e aprovados pelo usuário.*

## Preparação verificada

- `Tools/move_legacy_ignored.sh` moveu 60 itens locais para `Legacy/`, incluindo os FBX/texturas antigos e o pacote do Medieval Kit. O conteúdo antigo deixou de estar em `Assets/` e `Packages/`.
- Configurações anteriores preservadas em `.unity/setup-gamekit-before/`; a `Library/` antiga foi movida para `.unity/Library-before-gamekit/`. A Unity gerou uma nova `Library/` neste projeto.
- Editor 6000.3.27f2, licença Personal e `wmctrl` disponíveis nesta máquina.
- O Editor abriu pelo launcher e compilou o ponto de entrada `GameYunit.Editor.EditorEntryPoints.OpenKitImport` sem erros C# no log.
- O usuário adicionou o kit à conta na Asset Store. O Editor inicialmente não recebia a sessão do Hub: o socket do Hub ficava fora do `/tmp` isolado.
- O launcher passou a expor somente o socket `Unity-hubIPCService.sock` ao Editor e a usar `-useHub -hubIPC` quando ele está disponível. Nenhum token foi copiado ou versionado.
- Após reabrir, o Package Manager mostrou **3D Game Kit | 3D Sample Project 1.9.5**, da Unity Technologies, em **My Assets**, com o botão Download habilitado. Conferência por captura em `Logs/PackageManager.png`.
- O usuário iniciou o download. O arquivo temporário está em `.unity/asset-store/Asset Store-5.x/Unity Technologies/Complete ProjectsPacks/`, dentro da raiz.

## Auditoria do pacote e dos pontos multiplayer

Leitura do pacote completo: 2.962 caminhos, com cópias dos 274 scripts para inspeção em `.unity/kit-inspection/`. Os 274 scripts importados foram comparados byte a byte com os do pacote e não apresentaram diferenças. Esta auditoria de código não substitui validação de execução.

| Ponto | Evidência no conteúdo examinado | Consequência para o co-op futuro |
|---|---|---|
| Pasta | `Assets/3DGamekit/` | O `.gitignore` já cobre o caminho correto. |
| Cenas de build do kit | `Scenes/Start.unity`, `Scenes/GamePlay/Level1.unity`, `Scenes/GamePlay/Level2.unity` | Cenas usadas no jogo serão copiadas para `Assets/_Project/Scenes/`. |
| Input | `PlayerInput.cs`: singleton `Instance`, exceção se houver duas instâncias, `Input.GetAxis`, `GetButton` e `GetButtonDown` | Precisa de uma cópia adaptada na Fase 2; Input Handling = Both ao reintegrar o Input System. |
| Controlador e root motion | `PlayerController.Awake` grava `s_Instance`; `OnAnimatorMove` move/rotaciona o `CharacterController`; `OnEnable` registra o receptor de dano e oculta o cajado inicialmente | A cópia adaptada deve condicionar o registro local e o movimento ao dono. Desativar o componente não impede seu `Awake`; considerar também receptores registrados e eventos de animação. |
| Câmera | Cinemachine **3.1.3**; `CameraSettings` usa duas `CinemachineVirtualCameraBase`, `CinemachineOrbitalFollow` e `CinemachineInputAxisController` | A câmera e seus controles devem seguir somente a Ellen local; as APIs são do Cinemachine 3. |
| Vida | `Damageable.cs`: `currentHitPoints` com setter privado, `ResetDamage`, UnityEvents e mensagens `DAMAGED`/`DEAD` | A ponte deverá observar o sistema do kit. `Update` desliga a invulnerabilidade após o intervalo, portanto definir `isInvulnerable` uma única vez não basta para bloquear dano nas réplicas. |
| Inimigos | `EnemyController.cs`: `NavMeshAgent.updatePosition = false`, `OnAnimatorMove`, Rigidbody cinemático e consulta a `PlayerInput.Instance.HaveControl()` | IA e movimento do inimigo precisam de controle do servidor; a dependência do input global também precisa ser removida na cópia. |
| Alvo e Chomper | `TargetScanner.Detect` consulta exclusivamente `PlayerController.instance`; `ChomperBehavior` usa `TargetDistributor` para reservar posições ao redor do alvo | Seleção deve considerar os jogadores vivos; o reset de pool também deve liberar o registro do seguidor no distribuidor. |
| Cajado | `MeleeWeapon.BeginAttack/EndAttack` controlam a janela; `FixedUpdate` faz sphere casts e chama `Damageable.ApplyDamage` | Os eventos de animação podem abrir ataques em réplicas; somente o servidor deve aplicar os acertos e o dano. |
| Fluxo de cena | `SceneController` usa singleton, procura um único `PlayerInput` e chama `SceneManager.LoadSceneAsync` | Nas cópias co-op, o fluxo será substituído pelo gerenciador de cenas da rede. |
| Pausa | `StartUI.SwitchPauseState` usa `Time.timeScale`, bloqueia input e pausa os `PlayableDirector` encontrados | O menu Esc do co-op precisa ser local e separado dessa implementação. |
| HUD | `HealthUI.representedDamageable` é uma referência direta; o componente instancia os ícones a partir da vida máxima | O futuro HUD local deverá observar o estado da ponte de rede. |
| Assemblies | Os scripts centrais do jogo não têm asmdef; há asmdefs separados para WorldBuilding, SimpleSFX e PackageManagerCheck | Mantém-se a ponte `KitBridge` no Assembly-CSharp e as asmdefs próprias sem referências aos tipos do kit. |
| ProjectSettings | O pacote contém InputManager, TagManager, DynamicsManager, GraphicsSettings, QualitySettings e EditorBuildSettings, entre outros | Revisar o diff após a importação antes de validar o jogo. |

## Importação e preparação

- Download completo pelo Package Manager; pacote `3D Game Kit 3D Sample Project.unitypackage` no cache local (cerca de 2.203 MiB).
- Dependências do manifesto do kit mescladas pelo importador oficial. Versões resolvidas: URP 17.3.0, Cinemachine 3.1.3 e uGUI 2.0.0. O pacote antigo `com.unity.textmeshpro` não foi adicionado: seus tipos estão no uGUI desta versão.
- Importação completa pelo launcher em batch, incluindo ProjectSettings, com saída **0** e sem erros C#: `Logs/Fase1-import.log`.
- Revisados os grupos de configurações: layers e matriz de colisões do kit, eixos de input (incluindo Pause), URP/global settings e quality levels foram mantidos. Restaurados identidade/GUID do projeto, resolução 1920×1080, coleta incremental e **Input Handling = Both**.
- NGO **2.13.3** e UTP **2.7.4** adicionados e resolvidos após o kit. Ainda não há sistemas de rede nas cenas desta fase.
- `PrepareSinglePlayer` copiou `Start.unity` e `Level1.unity` para `Assets/_Project/Scenes/`, com GUIDs novos, e configurou essas duas cópias no build. Preparação em batch com saída **0**, sem erros C#: `Logs/Fase1-setup.log`.
- `GameYunit.Editor` só usa APIs do Editor, sem dependência dos tipos do kit. `OpenGame`/`PlayGame` usam a primeira cena habilitada no build; `OpenLevel1`/`PlayLevel1` abrem a fase diretamente e `PlayMenu` abre Start.

### Pipeline ausente no pacote

O teste de integridade encontrou uma referência sem asset: o `GraphicsSettings` importado aponta para um pipeline que não está entre os 2.962 caminhos do pacote. Após a conciliação descrita abaixo, a configuração usa os assets `PC_Renderer.asset` e `PC_RPAsset.asset` trazidos por Rodrigo, mantendo seus GUIDs, depth/opaque textures, HDR e SSAO, acrescentando os render features de céu, reflexão de água e blur do kit. A configuração fica em `KitBridge/Editor/GameKitRenderingSetup.cs`, sem asmdef e sem editar os scripts originais. O pipeline é usado em Graphics e Quality, e o perfil de volume aponta para um asset existente.

### Conciliação com o commit `a7b412c`

O commit remoto de Rodrigo apareceu após a comparação inicial com `4670e16`. O trabalho local foi preservado em cópia dentro de `.unity/reconcile-review/` e em um stash identificado antes de avançar a branch por **fast-forward** para `a7b412c`, sem criar commit de merge. As alterações locais foram recompostas e reconciliadas por arquivo.

| Área | Resultado preservado |
|---|---|
| Dependências | As **64 dependências diretas** do commit de Rodrigo permanecem nas mesmas versões; NGO 2.13.3 e UTP 2.7.4 são os dois acréscimos locais. |
| Metadados | Mantidos os GUIDs de pastas, `EditorEntryPoints`, asmdef e assets PC introduzidos por Rodrigo. Nenhum GUID duplicado foi encontrado em `_Project`. |
| Pontos de entrada | Mantida a escolha genérica da primeira cena de build em `OpenGame`/`PlayGame`; acrescentadas opções explícitas para Level1, importação/preparação do kit e prévia Full HD. |
| Renderização | Mantidos `PC_RPAsset`/`PC_Renderer` e SSAO; integrados os efeitos de céu, água e blur. O perfil de volume ausente foi substituído pelo `DefaultVolumeProfile` existente. Os assets locais alternativos `GameURP`/`GameRenderer` não foram adicionados ao commit. |
| Launcher | Combinados os sockets de licença do launcher de Rodrigo com a descoberta de sockets em `/mnt/SSD/Unity/.tmp` e `/tmp`, argumentos de integração com o Hub e capturas/reabertura da preparação. Caches e temporários continuam confinados ao projeto. |
| Cenas/configurações | Mantidas as cópias Start/Level1, input Both, identidade anterior do projeto, resolução 1920×1080, coleta incremental e qualidade Fantastic nesta máquina. |

### Arquivos grandes fora do commit

Por solicitação do usuário, `Assets/_Project/Scenes/Level1.unity` (~38,5 MiB) e seu `.meta` ficam ignorados. A preparação gera uma cópia local a partir do kit com GUID fixo, preservando as referências do projeto entre máquinas. Start permanece versionada por ser pequena.

Também ficam ignorados `Assets/TextMesh Pro/` e seu `.meta`; a fonte SDF principal tem ~2,15 MiB. Os recursos essenciais foram importados localmente do pacote que acompanha o uGUI, fora de Play. O teste do fluxo passou a conferir as configurações e a fonte padrão do TMP. O README lista estes arquivos e as instruções para o Rodrigo obtê-los; não há download separado para as cópias geradas e os recursos embutidos.

### Verificação final executada

| Verificação | Resultado |
|---|---|
| Importação completa em batch | Saída 0; `Logs/Fase1-import.log` |
| Preparação das cenas em batch | Saída 0; `Logs/Fase1-setup.log` |
| Criação/configuração do URP | Saída 0; `Logs/Fase1-rendering.log` |
| `Tools/unity.sh --verify` | **3 testes passaram**, 0 falhas; `Logs/TestResults.xml` e `Logs/Tests.log` |
| Verificação após conciliar `a7b412c` | **3 testes passaram novamente**, 0 falhas, incluindo menu → Level1; configuração URP em batch com saída 0 em `Logs/Fase1-reconcile-rendering.log` |
| Geração de Level1 ignorada | Cópia anterior preservada, arquivo retirado temporariamente de `Assets` e regenerado por `PrepareSinglePlayer`; saída 0, conteúdo idêntico à origem e GUID esperado conferidos; `Logs/Fase1-regenerate-local-scene.log` |
| Recursos TMP e verificação final | Importação dos recursos essenciais em batch com saída 0; **3 testes passaram novamente**, incluindo configurações/fonte padrão do TMP; `Logs/Fase1-tmp-import.log` e `Logs/TestResults.xml` |
| Integridade de Start/Level1 | Sem componentes com scripts ausentes; pipeline disponível; Ellen com CharacterController e controlador de animação |
| Fluxo em Play no teste | Botão Start original acionado; Level1 carregou; Ellen, CharacterController, Animator e câmera encontrados; sem erros de execução capturados pelo runner |
| Inspeção visual no Editor | Level1 aberto por `--reopen-play`, Ellen/cenário/HUD renderizados; sem exceções/erros C# no log conferido |
| Prévia Full HD | Confirmada pela captura: **1920×1080**, escala de encaixe **0,48x**; zoom 3x e modo de baixa resolução da prévia removidos |

O primeiro ensaio apontou o pipeline ausente e um problema de limpeza do próprio teste ao restaurar uma configuração de cenas vazia. Ambos foram corrigidos antes da execução final. O ajuste posterior de resolução foi conferido diretamente no Editor e no log; ele altera somente a apresentação da aba Game.

**Warning observado:** `CompositionModifier` do Cinemachine 3.1.3 não tem o atributo de serialização esperado. O kit e o pacote permanecem sem alterações para suprimir esse warning. Não houve exceções de jogo no trecho de execução inspecionado.

As cenas incluídas no build desta etapa são Start e Level1; Level2 continua no kit e não foi preparado como fase do jogo.

## Encerramento aprovado

- O usuário informou: “single player avaliado” e “avaliado e ok tambem” sobre a preparação na máquina do Rodrigo. Esta é uma validação relatada pelo usuário, além dos testes automáticos registrados acima.
- Autorização explícita: “encerre a fase 1, parta para cooperativo”. A Fase 1 está encerrada; a Fase 2 está autorizada.
- Partidas em rede pertencem à Fase 2 e não foram validadas nesta fase.
