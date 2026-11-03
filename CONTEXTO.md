# GameYunit — Contexto do projeto

Fonte da verdade do projeto: o que existe, **o que foi verificado e como**, as decisões e o próximo passo. Este arquivo registra só o que foi realmente executado e conferido. Plano completo: [Docs/Plano.md](Docs/Plano.md). Como rodar: [README.md](README.md).

*Atualizado em 02/11/2026 — Fase 0 concluída na branch `rebuild/gamekit`.*

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
| 1 | Projeto limpo + 3D Game Kit em single-player + auditoria multiplayer | ⏳ Próxima (exige um passo manual: importar o kit pela My Assets) |
| 2 | Co-op mínimo: conexão UDP/WebSocket, duas Ellens, câmera/input locais | — |
| 3 | Inimigos e combate em rede, caído/revive, HUD de vida | — |
| 4 | Loop survivors: spawner, XP, upgrades, fim de partida | — |
| 5 | Level2, áudio, balanceamento | — |

**Verificado nesta máquina (Fase 0):**
- o launcher encontra o Editor 6000.3.27f2 em `~/Unity/Hub/Editor` e copia a licença ativada pelo Hub;
- o sandbox redireciona todas as gravações da Unity para `.unity/` e bloqueia o resto do home;
- `Tools/unity.sh -batchmode -quit` abre o projeto com licença válida e sai com código 0.

**Ainda não existe:** nenhum código, cena ou prefab do jogo novo. O kit ainda não foi importado.

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
- **Kit intocado.** Código que usa tipos do kit fica em `Assets/_Project/Scripts/KitBridge/`, **sem asmdef** (Assembly-CSharp). As asmdefs `GameYunit.Core` (sem rede), `GameYunit.Runtime`, `GameYunit.Editor` e `GameYunit.Tests` não dependem do kit. Cópias de scripts do kit são registradas em [Docs/AlteracoesNoKit.md](Docs/AlteracoesNoKit.md).
- **Convenções:**
  - namespaces `GameYunit.<Pasta>`;
  - campos privados `_camelCase`;
  - comentários e logs em português, com prefixo `[Classe]`;
  - métodos só de servidor terminam em `Server`.

## 5. Organização atual
```
Assets/_Project/{Scripts,Prefabs,Scenes,ScriptableObjects}   esqueleto vazio
Assets/3DGamekit/                                            3D Game Kit (ignorado; nome a confirmar na Fase 1)
Docs/                     Plano.md, Fase0.md, AlteracoesNoKit.md
Tools/unity.sh            launcher em sandbox
Tools/move_legacy_ignored.sh  move arquivos ignorados do legado após o checkout
Legacy/                   projeto anterior (código, cenas, docs), fora de Assets/
```

## 6. Ambiente e máquinas
- **Esta máquina (Rodrigo):**
  - Editor em `~/Unity/Hub/Editor/6000.3.27f2`;
  - Unity Hub instalado e licença Personal ativa;
  - `wmctrl` **não instalado**: `--reopen*`, `--close-editor` e capturas não funcionam até `sudo pacman -S wmctrl`.
- **Máquina do Labubu:**
  - Editor em `/mnt/SSD/Unity/editors/6000.3.27f2`;
  - tem os arquivos ignorados do projeto antigo; depois do checkout desta branch, precisa rodar `Tools/move_legacy_ignored.sh`.
- O log da Unity no sandbox registra `Access token is unavailable`. O login para a My Assets pode ter de ser feito dentro do Editor (a confirmar na Fase 1).

## 7. Legado
- O projeto anterior (Blocos 1–6) está em `Legacy/` e na tag `legacy/v1-pre-gamekit` / branch `legacy/v1`.
- A documentação antiga marcava como testados sistemas que nunca rodaram juntos: **nada do legado é considerado funcionando**.
- O que será reaproveitado está na tabela §4 do [Plano](Docs/Plano.md):
  - pool, pool de rede, `NetworkManagerSetup` (com extensões), `PlayerSpawnPoint`, `PlayerLevel` e `XpDropService`;
  - `XpGem`, `XpDropper` e a partida, reescritos;
  - o restante, descartado.
- A vida passa a vir do `Damageable` do kit; o Bloco 4 antigo era de outra pessoa, então alinhe antes de reaproveitar algo dele.

## 8. Próximo passo
**Fase 1**, conforme o [Plano](Docs/Plano.md):
- manifesto limpo do template URP 6.3;
- **importação manual do 3D Game Kit** pelo usuário (Window → Package Manager → My Assets, com o Editor aberto por `Tools/unity.sh`);
- revisão do diff de `ProjectSettings`;
- instalação de NGO/UTP/uGUI;
- auditoria dos pontos multiplayer do kit;
- **pronto quando** o Level1 for jogável sozinho, sem erros no Console.

## 9. Pendências e riscos abertos
- A estrutura interna do kit está **a confirmar**: asmdef ou não, sistema de input, versão do Cinemachine, singletons, `Time.timeScale` na pausa, nome da pasta.
- Login da conta no Editor dentro do sandbox, para a My Assets.
- API de `NetworkAnimator` com autoridade do dono no NGO 2.13.
- Desempenho dos inimigos do kit em horda (teto inicial de 40).
