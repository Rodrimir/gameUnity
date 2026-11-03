# GameYunit — Contexto do projeto

Fonte da verdade do projeto: o que existe, **o que foi verificado e como**, as decisões e o próximo passo. Este arquivo registra só o que foi realmente executado e conferido. Plano completo: [Docs/Plano.md](Docs/Plano.md). Como rodar: [README.md](README.md).

*Atualizado em 03/11/2026 — Fase 2 em implementação: conexão co-op UDP verificada entre dois processos reais, tela preta da build corrigida e dois pacotes visuais integrados como inimigos de rede iniciais; replicação de movimento ainda não provada.*

## 1. O jogo
Survivors-like 3D em **co-op para 2 jogadores**, Host–Client por IP direto: mesma máquina em `127.0.0.1` ou LAN com o Host em `0.0.0.0`. O jogo está sendo **refeito sobre o 3D Game Kit** da Unity (Asset Store, 1.9.5, Unity 6.3 + URP), usando dele a arte, as fases (Level1, depois Level2), a Ellen e os inimigos (Chomper na horda; Spitter e Grenadier como escalada).

**Loop alvo:**
- hordas contínuas;
- combate do kit;
- vida, dano, caído e revive;
- gemas de XP com nível individual;
- upgrades periódicos;
- fim de partida e reinício.

A prioridade é ter algo funcional e testado em rede com o mínimo de sistemas.

## 2. Estado real

| Fase | Conteúdo | Estado |
|---|---|---|
| 0 | Preservar legado, esqueleto, launcher, docs | ✅ Concluída e verificada ([Fase0.md](Docs/Fase0.md)) |
| 1 | Projeto limpo + 3D Game Kit em single-player + auditoria multiplayer | ✅ Concluída: 3 testes automáticos passaram; single-player e instalação na máquina do Rodrigo aprovados pelo usuário ([Fase1.md](Docs/Fase1.md)) |
| 2 | Co-op mínimo: conexão UDP/WebSocket, duas Ellens, câmera/input locais | ⏳ Conexão UDP entre dois processos verificada; falta provar movimento/animação replicados e o ensaio WebSocket ([Fase2.md](Docs/Fase2.md)) |
| 3 | Inimigos e combate em rede, caído/revive, HUD de vida | — |
| 4 | Loop survivors: spawner, XP, upgrades, fim de partida | — |
| 5 | Level2, áudio, balanceamento | — |

**Verificado na máquina do Rodrigo (Fase 0):**
- o launcher encontra o Editor 6000.3.27f2 em `~/Unity/Hub/Editor` e copia a licença ativada pelo Hub;
- o sandbox redireciona todas as gravações da Unity para `.unity/` e bloqueia o resto do home;
- `Tools/unity.sh -batchmode -quit` abre o projeto com licença válida e sai com código 0.

**Verificado na máquina do Labubu (Fase 1):**
- Os 60 itens ignorados do legado foram movidos para `Legacy/`, e a `Library/` foi recriada dentro deste projeto.
- Editor 6000.3.27f2, licença Personal, `wmctrl` e sessão do Hub disponíveis. O launcher expõe o socket IPC do Hub ao sandbox; My Assets mostrou o kit 1.9.5 da conta.
- Download em `.unity/asset-store/` e importação completa, com saída 0. URP 17.3.0, Cinemachine 3.1.3, uGUI 2.0.0, NGO 2.13.3 e UTP 2.7.4 resolvidos.
- Start e Level1 copiadas para `Assets/_Project/Scenes/` e usadas no build. Os 274 scripts originais do kit conferem byte a byte com o pacote baixado.
- A cópia Level1 (~38,5 MiB) e os recursos essenciais do TextMesh Pro (fonte principal ~2,15 MiB) ficam apenas locais, ignorados por tamanho. O README explica sua obtenção na máquina do Rodrigo: gerar Level1 pelo menu Preparar single-player, com GUID estável, e importar TMP Essential Resources a partir do uGUI.
- O pacote referencia assets de pipeline URP que não inclui. Após incorporar `a7b412c` do Rodrigo, foram preservados os assets `PC_RPAsset`/`PC_Renderer` e seus GUIDs em `_Project/Settings/Rendering`, completando-os com os render features originais de céu, água e blur, além do SSAO já presente. Input Handling = Both.
- `Tools/unity.sh --verify`: **3 testes passaram**, incluindo ausência de scripts faltantes nas duas cenas e o botão Start carregando Level1 com Ellen, controlador, animator e câmera.
- Conciliação com o commit **`a7b412c` do Rodrigo** por fast-forward, seguida da integração das adições locais. As 64 dependências dele foram preservadas nas mesmas versões; a configuração URP terminou com saída 0 e os **3 testes passaram novamente** após a conciliação.
- Level1 aberta e renderizada no Editor, sem exceções/erros C# no log conferido. Prévia em **Full HD 1920×1080**, com escala ajustada à janela (0,48x nesta disposição), sem o zoom 3x anterior. `--reopen-play` preserva a escolha da primeira cena habilitada no build, introduzida por Rodrigo; o menu **Jogar Level1** abre a fase diretamente.

**Verificado na máquina do Rodrigo (Fase 2, 03/11/2026):**
- **Tela preta da build resolvida.** `GaussianBlurRenderFeature` do kit resolve o material por `Shader.Find("Hidden/Universal/GaussianBlur")`; o shader não era referenciado por material nenhum e ficava fora do build, então `new Material(null)` lançava e o Render Graph abortava cada frame — 3 709 erros em `Logs/Client-run.log`. O shader entrou em **Always Included Shaders**; nenhum arquivo do kit foi tocado. Build regerada: **zero** erros de render, menu e fase desenhados.
- **Licença do sandbox desatualizada** fazia todo build em `-batchmode` falhar com `No valid Unity Editor license found`. O launcher só copia a licença quando `.unity/config/unity3d/Unity/licenses` não existe ainda; sincronizar a pasta a partir de `~/.config/unity3d/Unity` resolveu. Vale rever essa cópia única no launcher.
- **Co-op UDP entre dois processos reais** (duas instâncias da build em `127.0.0.1:7777`): conexão, carga de `Level1_Coop` pela rede, dois jogadores em pontos distintos, nomes e cores replicados, câmera e input exclusivos do dono, e entrada manual numa sala já aberta com HUD `2/2` nas duas janelas. Evidências em `Logs/Network/udp-*.json` e capturas em `Logs/Coop-*2jogadores.png`.
- `wmctrl` **já está instalado** nesta máquina; `--reopen*` e capturas funcionam.

**Validação do usuário:** single-player e preparação na máquina do Rodrigo avaliados e aprovados nesta conversa. O usuário autorizou encerrar a Fase 1 e iniciar a Fase 2. **Ainda não verificado:** movimento e animação replicados entre processos, o ensaio em WebSocket (TCP) e a partida entre duas máquinas da LAN. Há um warning de serialização `CompositionModifier` do Cinemachine 3.1.3; nenhum arquivo original foi alterado para suprimi-lo.

**Atualização de 03/11/2026:** `Cyber Monsters 2` e `DungeonCharacters/Skeletons_demo` foram importados e adaptados sem editar os arquivos de origem. As cópias `CyberMonster` e `DungeonSkeleton` ficam em `_Project/Prefabs/Enemies`, são registradas no NGO e são geradas pelo servidor na primeira onda de `Level1_Coop`. A compilação da integração passou; o teste EditMode teve 20 de 22 casos aprovados em modo sem gráficos, com os dois casos restantes bloqueados por erro de Global Illumination sem dispositivo gráfico. A sessão Host–Client com esses inimigos ainda não foi observada.

## 3. Decisões tomadas
| # | Decisão |
|---|---|
| 1 | **Movimento do dono:** a Ellen roda o controlador só na máquina do dono, replicada por `NetworkTransform` (autoridade do dono) + `NetworkAnimator`. O resto é decidido pelo servidor. |
| 2 | Protocolo padrão **UDP** (porta 7777); **WebSocket/TCP** selecionável na tela de conexão. Os dois lados usam o mesmo. |
| 3 | **Combate do kit:** cajado da Ellen (`MeleeWeapon`), **golpe manual** como no kit. As armas, projéteis e o ataque automático antigos foram descartados. |
| 4 | **Visual do kit.** O filtro PSX fica no legado. |
| 5 | **Parceiro revive** o jogador caído; game over quando os dois caem. |
| 6 | **Kit fora do Git** (EULA, ~2,2 GB); o `README` explica como importá-lo em cada máquina. |
| 7 | **Esc abre menu local** sem parar a partida; só a escolha de upgrade pausa os dois. |

## 4. Arquitetura (resumo; detalhes em [Docs/Plano.md](Docs/Plano.md) §2–§5)
- **Servidor decide:** spawn, IA, dano, vida, caído/revive, morte, XP, nível, upgrades, partida e cena (`NetworkSceneManager`).
- **Dono decide:** movimento, rotação e animação da própria Ellen; inicia o golpe, mas o acerto e o dano são confirmados na cópia do servidor.
- **Cliente local:** câmera, input, HUD e menu Esc.
- **Rede:** NGO 2.x + Unity Transport 2.x (o projeto atual resolve NGO 2.13.3 e UTP 2.7.4).
- **Pooling** (`ObjectPoolManager` + `NetworkObjectPool`, a reaproveitar do legado) para inimigos, gemas e VFX.
- **Observer:** eventos C# com reset estático.
- **Scripts do kit intocados.** Código que usa seus tipos fica em `Assets/_Project/Scripts/KitBridge/`, **sem asmdef**. A preparação de renderização fica em `KitBridge/Editor` (Assembly-CSharp-Editor). Existem `GameYunit.Editor` e `GameYunit.Tests`, sem dependência dos tipos do kit; `GameYunit.Core` e `GameYunit.Runtime` continuam previstas. Cópias de scripts do kit serão registradas em [Docs/AlteracoesNoKit.md](Docs/AlteracoesNoKit.md).
- **Convenções:**
  - namespaces `GameYunit.<Pasta>`;
  - campos privados `_camelCase`;
  - comentários e logs em português, com prefixo `[Classe]`;
  - métodos só de servidor terminam em `Server`.

## 5. Organização atual
```
Assets/_Project/Scenes/       Start e Level1 (cópias)
Assets/_Project/Scripts/      Editor e KitBridge/Editor (preparação)
Assets/_Project/Settings/Rendering/  PC_Renderer e PC_RPAsset
Assets/_Project/Tests/Editor/  testes de integridade e menu → Level1
Assets/3DGamekit/              3D Game Kit 1.9.5 (ignorado)
Docs/                     Plano.md, Fase0.md, Fase1.md, AlteracoesNoKit.md
Tools/unity.sh            launcher em sandbox
Tools/move_legacy_ignored.sh  move arquivos ignorados do legado após o checkout
Legacy/                   projeto anterior (código, cenas, docs), fora de Assets/
```

## 6. Ambiente e máquinas
- **Máquina do Rodrigo (estado registrado na Fase 0):**
  - Editor em `~/Unity/Hub/Editor/6000.3.27f2`;
  - Unity Hub instalado e licença Personal ativa;
  - `wmctrl` **não instalado**: `--reopen*`, `--close-editor` e capturas não funcionam até `sudo pacman -S wmctrl`.
- **Máquina do Labubu:**
  - Editor em `/mnt/SSD/Unity/editors/6000.3.27f2`;
  - licença Personal e `wmctrl` disponíveis; arquivos ignorados já migrados com `Tools/move_legacy_ignored.sh`;
  - Hub autenticado, conectado pelo launcher ao socket `/mnt/SSD/Unity/.tmp/Unity-hubIPCService.sock`; acesso ao kit em My Assets confirmado.
- O log inicial de licenciamento pode registrar `Access token is unavailable` antes de o Hub fornecer a sessão. Nesta máquina, o acesso à My Assets funcionou após disponibilizar o IPC do Hub dentro do sandbox.

## 7. Legado
- O projeto anterior (Blocos 1–6) está em `Legacy/` e na tag `legacy/v1-pre-gamekit` / branch `legacy/v1`.
- A documentação antiga marcava como testados sistemas que nunca rodaram juntos: **nada do legado é considerado funcionando**.
- O que será reaproveitado está na tabela §4 do [Plano](Docs/Plano.md):
  - pool, pool de rede, `NetworkManagerSetup` (com extensões), `PlayerSpawnPoint`, `PlayerLevel` e `XpDropService`;
  - `XpGem`, `XpDropper` e a partida, reescritos;
  - o restante, descartado.
- A vida passa a vir do `Damageable` do kit; o Bloco 4 antigo era de outra pessoa, então alinhe antes de reaproveitar algo dele.

## 8. Próximo passo
Fechar a **Fase 2**: descobrir por que o processo cliente encerra de forma ordenada ao ativar o input de teste do `CoopSmokeProbe` (o Host registra `Jogador desapareceu durante o ensaio`), provar movimento e animação replicados, repetir o ensaio em WebSocket e testar entre duas máquinas da LAN.

## 9. Pendências e riscos abertos
- `ConnectionRules.Version` é uma constante, não um hash do build: **builds diferentes conectam mesmo assim**, sem aviso. Convém derivar a versão de algo que mude a cada build antes de distribuir cópias.
- A build distribuível é só Linux x86_64 (`CoopProjectSetup.BuildLinux`); não há alvo Windows.
- Auditoria dos singletons, input, Cinemachine 3, pausa e dano está em [Fase1.md](Docs/Fase1.md). A invulnerabilidade do `Damageable` expira no `Update`; a ponte futura precisa considerar isso.
- API de `NetworkAnimator` com autoridade do dono no NGO 2.13.
- Desempenho dos inimigos do kit em horda (teto inicial de 40).
