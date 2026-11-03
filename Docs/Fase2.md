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

## Verificação executada

| Verificação | Evidência | Resultado |
|---|---|---|
| Preparação das cópias | `Logs/Fase2-prepare.log` | Saída 0 |
| Regras de aprovação/endereço/nomes, prefab/cena, ciclo Host e regressão single-player | `Logs/TestResults.xml`, execução de 03/11/2026 02:32 UTC | 20 testes passaram, 0 falhas |
| Ciclo do Host no teste | `CoopTests.HostLoadsCoopAndReturnsToMenuOnShutdown` | Level1_Coop carregada, PlayerObject criado e retorno a CoopMenu após shutdown |

## Ensaios ainda em execução

Build Linux Development e sessões reais Editor Host + Linux Client em UDP/WebSocket. Os resultados serão registrados após observação; testes de uma única instância não provam comunicação entre processos.

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
