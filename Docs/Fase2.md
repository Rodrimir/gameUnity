# Fase 2 — Cooperativo mínimo

Em implementação. A Fase 1 foi encerrada por aprovação explícita do usuário; o commit de encerramento é `eb7ebb6`.

## Implementação

- `CoopMenu`: conexão uGUI/TMP com IPv4, porta, nome, cor e UDP/WebSocket; menu Esc local sem alterar `Time.timeScale`.
- `CoopSession`: Host escuta em `0.0.0.0`, aprovação de até dois jogadores contando o Host, reserva de vagas durante aprovação, carga de Level1_Coop pelo `NetworkSceneManager`, criação do PlayerObject após a carga/sincronização, encerramento e retorno ao menu.
- `EllenCoop`: prefab e animator próprios; `NetworkTransform` e `NetworkAnimator` com autoridade do dono. Controlador/input copiados para `KitBridge`, sem singleton, movimento e root motion somente no dono. Réplicas recebem transform/animação e não leem input local.
- Câmera do kit ligada à Ellen local; nome e cor replicados pelo servidor.
- Dois pontos de nascimento reaproveitam o marcador do legado, sem reaproveitar a vida do Bloco 4.
- Level1_Coop é gerada do cenário original, com GUID estável e ignorada pelo tamanho. UI/fluxo single-player, inimigos, puzzles, destrutíveis e cutscenes foram removidos da cópia. Os arquivos originais do kit permanecem intocados.
- Vida e dano ainda pertencem à Fase 3. Nesta fase, uma queda abaixo do cenário devolve o dono ao nascimento.
- `Tools/unity.sh --client` executa o build Linux no mesmo sandbox, com dados, temporários e logs dentro do projeto.
- A build Linux de Development usa OpenGL Core, o backend já utilizado pelo Editor nesta máquina. O método de build restaura a seleção de APIs anterior do projeto em `finally`. Isso reduz o custo de variantes de shader no build de teste.
- **Correção da tela preta na build (03/11/2026).** `GaussianBlurRenderFeature` do kit resolve o material por `Shader.Find("Hidden/Universal/GaussianBlur")`. Nenhum material referencia esse shader, então ele ficava fora do build: `Shader.Find` devolvia `null`, `new Material(null)` lançava `ArgumentNullException` e o Render Graph abortava o frame inteiro, a cada frame. A janela abria e nada era desenhado. O shader passou a constar em **Always Included Shaders** (`ProjectSettings/GraphicsSettings.asset`); nenhum arquivo do kit foi alterado.
- **Licença no sandbox.** O launcher só copia a licença quando `.unity/config/unity3d/Unity/licenses` ainda não existe. A cópia tinha envelhecido e o build em `-batchmode` falhava com `No valid Unity Editor license found` (erro 404 de entitlements). Sincronizar a pasta a partir de `~/.config/unity3d/Unity` resolveu.
- Os assets importados `Cyber Monsters 2` e `DungeonCharacters/Skeletons_demo` foram preservados como fonte e receberam cópias jogáveis em `Assets/_Project/Prefabs/Enemies/`. As cópias usam `NetworkObject`, `NetworkTransform` e `NetworkEnemy`; o servidor controla perseguição, ataque, vida e morte. A primeira onda de `Level1_Coop` contém uma cópia de cada tipo, e ambos são registrados no `CoopSession`.
- O hitbox e o `Damageable` são garantidos pela ponte em runtime, porque os prefabs de origem são somente visuais e não devem receber componentes do kit diretamente.

## Verificação executada

| Verificação | Evidência | Resultado |
|---|---|---|
| Preparação das cópias | `Logs/Fase2-prepare.log` | Saída 0 |
| Regras de aprovação/endereço/nomes, prefab/cena, ciclo Host e regressão single-player | `Logs/TestResults.xml`, execução de 03/11/2026 02:32 UTC | 20 testes passaram, 0 falhas |
| Ciclo do Host no teste | `CoopTests.HostLoadsCoopAndReturnsToMenuOnShutdown` | Level1_Coop carregada, PlayerObject criado e retorno a CoopMenu após shutdown |
| Compilação e preparação dos inimigos importados | `Logs/EnemySetup.log`, execução de 03/11/2026 06:50 UTC | Saída 0; dois prefabs gerados, registrados e referenciados pela cena |
| Regressão EditMode após integração | `Logs/TestResults.xml`, execução de 03/11/2026 06:54 UTC | 20 de 22 passaram; 2 testes falharam apenas por `-nographics` não ter dispositivo para Global Illumination |

## Ensaio de rede entre processos — 03/11/2026 (máquina do Rodrigo)

Build Linux regerada após a correção do shader: `Builds/Linux`, 1 746 192 954 bytes, OpenGL Core, Development.

**Renderização.** Build executada por `Tools/unity.sh --client`: menu e `Level1_Coop` desenhados, **zero**
`Render Graph Execution error` nos três processos observados, contra 3 709 ocorrências na build anterior
(`Logs/Client-run.log`). Capturas em `Logs/Prova-CoopMenu.png`, `Logs/Janela1-host.png`,
`Logs/Coop-host-2jogadores.png` e `Logs/Coop-cliente-2jogadores.png`.

**Co-op UDP com dois processos reais**, duas instâncias da build em `127.0.0.1:7777`:

| Verificação | Evidência | Resultado |
|---|---|---|
| Host e cliente se conectam em UDP | `Logs/Network/udp-host.json` | ✅ |
| `Level1_Coop` carregada pela rede | `checks` das duas evidências | ✅ |
| Dois jogadores em pontos distintos | `(22.75, 1.42, 64.29)` e `(25.15, 1.45, 64.29)` | ✅ |
| Nomes e cores replicados | `checks` das duas evidências | ✅ |
| Câmera segue apenas o dono | `localCamera: true` nos dois | ✅ |
| Input e CharacterController exclusivos do dono | `remoteInputBlocked: true` nos dois | ✅ |
| Entrada manual na sala aberta | `[CoopSession] Jogador 2 no ponto 1`; HUD `2/2` nas duas janelas | ✅ |

## Pendente nesta fase

- **Movimento e animação replicados ainda não provados.** No ensaio automatizado o processo cliente encerrou de forma
  ordenada no instante em que o roteiro ia ativar o input de teste (`UseTestInput`), sem exceção no log; o Host
  registrou `Jogador desapareceu durante o ensaio`. Causa não identificada. Enquanto isso não fechar, `localDistance`,
  `remoteDistance`, `jumpHeight`, `remoteWalkAnimation` e `remoteJumpAnimation` seguem sem medição válida.
- Ensaio em **WebSocket (TCP)** entre processos ainda não executado.
- Partida entre **duas máquinas** da LAN ainda não executada.

## Como repetir

Siga o roteiro de rede no README. Após gerar o build, `python3 Tools/verify_coop.py udp` e `python3 Tools/verify_coop.py ws` executam o ensaio completo, fechando o Editor anterior graciosamente e preservando os logs. Ao terminar, o Editor Host permanece aberto. Para o ensaio instrumental, abra o Editor por:

```bash
Tools/unity.sh -executeMethod GameYunit.KitBridge.Editor.CoopProjectSetup.PlayCoop --coop-role host --coop-protocol udp --coop-smoke
```

Após a fase carregar, em outro terminal:

```bash
Tools/unity.sh --client -screen-fullscreen 0 -screen-width 960 -screen-height 540 --coop-role client --coop-protocol udp --coop-smoke
```

O ensaio injeta input apenas no dono e observa a réplica real no outro processo. Evidências ficam em `Logs/Network/udp-host.json` e `udp-client.json`. Repita com `--coop-protocol ws` nos dois lados. Feche a instância anterior graciosamente antes de iniciar outro Editor.

## Referência de API

A versão instalada NGO 2.13.3 expõe `NetworkAnimator.AuthorityMode = AuthorityModes.Owner`, conferido no código do pacote e na [documentação oficial 2.13](https://docs.unity.cn/Packages/com.unity.netcode.gameobjects%402.13/manual/components/helper/networkanimator.html). Não foi necessário derivar NetworkAnimator. Gatilhos do controlador adaptado passam por `NetworkAnimator.SetTrigger`.
