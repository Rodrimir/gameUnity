# GameYunit

Survivors-like 3D em co-op para 2 jogadores (Host–Client por IP direto), sendo refeito sobre o **3D Game Kit** da Unity. Estado atual e o que já foi verificado: [CONTEXTO.md](CONTEXTO.md). Plano aprovado: [Docs/Plano.md](Docs/Plano.md).

## Rodar localmente (leia primeiro)

O clone **não traz tudo** o que o projeto precisa. Alguns itens são grandes, gerados pela Unity, presos à sua conta ou proibidos de redistribuir.

### O que falta no clone

| Item | Por que não está no Git | Como obter |
|---|---|---|
| **[3D Game Kit 1.9.5](https://assetstore.unity.com/packages/templates/packs/3d-game-kit-3d-sample-project-115747)** (Asset Store, ~2,2 GB) | A EULA da Asset Store proíbe redistribuir; ignorado em `/Assets/3DGamekit/` | Adicionar o kit gratuito à sua conta Unity e importar por My Assets (passo 5). |
| **Cópia `Assets/_Project/Scenes/Level1.unity` e seu `.meta`** (~38,5 MiB) | Retirada deste commit por tamanho; permanece apenas local | Não há download separado: depois de importar o kit, **GameYunit → Preparação → Preparar single-player** recria a cópia, com GUID estável. |
| **`Assets/_Project/Scenes/Level1_Coop.unity` e seu `.meta`** (~34 MiB) | Cópia local grande do cenário, gerada a partir do kit | Fora de Play, **GameYunit → Preparação → Preparar cooperativo**. Não exige outro download e mantém GUID estável. |
| **`Assets/TextMesh Pro/` e seu `.meta`** (fonte principal ~2,15 MiB) | Recursos de terceiros recriáveis, ignorados para manter o commit pequeno | Fora de Play, **Window → TextMeshPro → Import TMP Essential Resources**. O pacote vem com o uGUI instalado pelo manifesto; não precisa de download separado. |
| **Unity Editor 6000.3.27f2** | Instalação do sistema | Unity Hub → Instalações (a versão está em `ProjectSettings/ProjectVersion.txt`). |
| **Licença da Unity** | Credencial pessoal | Unity Hub → Preferências → Licenças → Adicionar → Personal. |
| `Library/` | Gerada pela Unity na primeira abertura | Automático (alguns minutos). |
| `.unity/` (caches, licença copiada, temporários) | Local de cada máquina | Criado pelo `Tools/unity.sh`. |
| `Builds/`, `Logs/` | Saídas locais | Criados ao compilar e testar. |
| Arquivos grandes do projeto antigo (ZIPs em `modelo-jogo/`, FBX de personagens, Medieval Kit) | Só servem para consultar o legado | Opcional; só existem na máquina de quem os importou. |

### Passo a passo

1. **Dependências do sistema (Linux):** Unity Hub, `bwrap` (bubblewrap) e, opcionalmente, `wmctrl` (necessário para `--reopen*`, `--close-editor` e capturas). No CachyOS/Arch: `paru -S unityhub` e `sudo pacman -S bubblewrap wmctrl`.
2. **Editor e licença:** no Unity Hub, instale a **6000.3.27f2** e ative a licença Personal. O launcher encontra o Editor sozinho em `~/Unity/Hub/Editor/<versão>` ou em `/mnt/SSD/Unity/editors/<versão>`; para outro caminho, use `export GAMEYUNIT_UNITY_EDITOR=/caminho/Editor/Unity`.
3. **Branch:** `git switch rebuild/gamekit`. Se esta máquina já tinha o projeto antigo, rode `Tools/move_legacy_ignored.sh`. Ele move para `Legacy/` os arquivos ignorados que o Git deixa nos caminhos antigos; sem isso, a Unity importaria de novo os modelos e o Medieval Kit.
4. **Conferir o ambiente:** `Tools/unity.sh --print-paths` deve mostrar o Editor com `(ok)` e a origem da licença. Depois, `Tools/unity.sh` abre o Editor. **Sempre** abra a Unity por esse script, nunca pelo Hub: ele mantém caches, downloads e temporários dentro do repositório.
5. **Importar o 3D Game Kit** (uma vez por máquina), com o Editor aberto por `Tools/unity.sh`:
   1. **Window → Package Manager → My Assets** (faça login na conta Unity, se pedir);
   2. **3D Game Kit**, versão 1.9.5 → **Download** → **Import**, importando tudo.

   O download fica em `.unity/asset-store/` e os assets em `Assets/3DGamekit/`. O pacote também importa configurações de projeto e dependências; veja os resultados em [Docs/Fase1.md](Docs/Fase1.md).

   Se você já adicionou o kit à conta no navegador, deixe o **Unity Hub aberto e autenticado** e rode `Tools/unity.sh --reopen-kit-import`. O launcher mantém os arquivos locais no sandbox e conecta o Editor ao canal IPC do Hub para receber a sessão. Também há o menu **GameYunit → Preparação → Importar 3D Game Kit** no Editor. O botão “Open in Unity” do navegador não é necessário.
6. **Preparar o single-player:** após a importação, use **GameYunit → Preparação → Configurar URP do kit** e **GameYunit → Preparação → Preparar single-player**. O primeiro completa os assets versionados `PC_RPAsset`/`PC_Renderer` com os efeitos do kit e um perfil de volume válido; o segundo gera as cópias ausentes de Start e Level1 em `_Project/Scenes` e configura essas cópias no build. **Rodrigo: Level1 não vem no clone; esse passo é obrigatório após baixar/importar o kit.** A geração mantém o GUID esperado pelo projeto e preserva uma cópia local já existente. Em **Project Settings → Player**, mantenha **Active Input Handling = Both**. Os originais do kit permanecem na pasta ignorada.
7. **Recursos de texto (obrigatório na máquina do Rodrigo):** fora de Play, use **Window → TextMeshPro → Import TMP Essential Resources**. O pacote já vem com o uGUI; não precisa de outro download. Esses recursos ficam em `Assets/TextMesh Pro/`, **fora do Git**, e precisam ser importados uma vez por máquina.
8. **Preparar o cooperativo:** fora de Play, use **GameYunit → Preparação → Preparar cooperativo**. Isso gera Level1_Coop ausente e coloca CoopMenu como primeira cena do build. O kit original permanece intocado.
9. **Verificar:** feche o Editor graciosamente com `Tools/unity.sh --close-editor` e rode `Tools/unity.sh --verify`. Os testes conferem integridade do single-player, regras de aprovação, prefab/cena cooperativos e ciclo de abertura/encerramento do Host. Resultados em `Logs/TestResults.xml` e `Logs/Tests.log`.
10. **Inspecionar:** `Tools/unity.sh --reopen-play` abre a primeira cena habilitada no build (CoopMenu após preparar o cooperativo) em Play, com prévia Full HD e escala ajustada à janela. Para ir direto à fase, use **GameYunit → Jogar Level1**, com o Editor fora de Play. **GameYunit → Jogar pelo menu do kit** abre explicitamente Start. Controles: WASD para mover, mouse para câmera, Espaço para pular e clique esquerdo para o cajado quando equipado.

### Teste em rede no Linux

1. No Editor, **GameYunit → Build → Cliente Linux Development** gera `Builds/Linux/GameYunit.x86_64` em OpenGL Core, com a mesma configuração visual do Editor desta máquina. O módulo **Linux Build Support (Mono)** deve estar instalado junto ao Editor.
2. **GameYunit → Jogar cooperativo** abre a tela de conexão. Escolha nome/cor e **Criar sala**. O Host escuta em `0.0.0.0:7777`.
3. Em outro terminal na raiz, rode `Tools/unity.sh --client -screen-fullscreen 0 -screen-width 1280 -screen-height 720`. Na janela do cliente, escolha outro nome/cor, mantenha `127.0.0.1:7777` e clique **Entrar na sala**.
4. Os dois jogadores devem se ver e se mover de forma independente. WASD move, Espaço pula e o mouse controla a câmera. **Esc** abre apenas o menu local; a partida continua. **Sair da sala** retorna ao menu; o Host continua aberto quando o cliente sai.
5. Repita escolhendo **WebSocket nos dois lados**. Para testar entre máquinas da LAN, use o IPv4 da máquina do Host no cliente e permita a porta 7777 UDP ou TCP conforme o protocolo.

Para gerar o mesmo build pelo terminal, com o Editor fechado:

```bash
Tools/unity.sh -batchmode -quit \
  -executeMethod GameYunit.KitBridge.Editor.CoopProjectSetup.BuildLinux \
  -logFile Logs/BuildLinux.log
```

O Multiplayer Play Mode não está disponível no Linux; o fluxo usa um Editor e um executável. Inimigos e dano compartilhado pertencem à Fase 3. O single-player continua acessível por **GameYunit → Jogar pelo menu do kit**.

O ensaio automatizado usa `--coop-role host|client --coop-protocol udp|ws --coop-smoke` e grava evidências em `Logs/Network/`. Não se inicia sem esses argumentos. Procedimento e resultados em [Docs/Fase2.md](Docs/Fase2.md).

## Organização

| Caminho | Conteúdo |
|---|---|
| `Assets/_Project/` | Código, prefabs, cenas e dados do jogo novo |
| `Assets/3DGamekit/` | 3D Game Kit (ignorado; nunca editar) |
| `Docs/` | Plano, documentação por fase e alterações em cópias de scripts do kit |
| `Tools/unity.sh` | Launcher da Unity em sandbox (`--print-paths`, `--status`, `--verify`, `--reopen*`) |
| `Legacy/` | Projeto anterior ao 3D Game Kit, só para consulta (fora de `Assets/`, não compila) |

O projeto anterior também está preservado na tag `legacy/v1-pre-gamekit` e na branch `legacy/v1`.
