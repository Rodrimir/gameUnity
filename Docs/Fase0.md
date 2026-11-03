# Fase 0 — Preservar e diagnosticar

*Concluída em 02/11/2026, na branch `rebuild/gamekit`. Plano: [Plano.md](Plano.md).*

## O que foi feito

### Legado preservado
- **Tag `legacy/v1-pre-gamekit`** e **branch `legacy/v1`** no commit `65b167c` (último estado de `main` antes da reconstrução).
- **Branch de trabalho `rebuild/gamekit`**, criada a partir de `main`.
- Movidos com `git mv` (histórico preservado) para **`Legacy/`**, fora de `Assets/`, onde a Unity não compila:

| Antes | Depois |
|---|---|
| `Assets/_Project/` (código, cenas, prefabs, arte, testes) | `Legacy/_Project/` |
| `Assets/DefaultNetworkPrefabs.asset` (listava prefabs antigos) | `Legacy/Assets/` |
| `Docs/` (Blocos 1–6, Menu, ProjetoUnity) | `Legacy/Docs/` |
| `CONTEXTO.md`, `README.md`, `PROMPTS.md` | `Legacy/` |
| `Tools/import_models.py` | `Legacy/Tools/` |

- `modelo-jogo/` continua na raiz, como combinado.
- Esta máquina não tinha arquivos ignorados do projeto antigo (ZIPs, FBX, Medieval Kit). Eles existem só na máquina do Labubu.
- O novo **`Tools/move_legacy_ignored.sh`** move esses arquivos para `Legacy/` depois do checkout. O Git só move arquivos versionados, então sem o script a Unity voltaria a importá-los.

### Esqueleto novo
`Assets/_Project/{Scripts,Prefabs,Scenes,ScriptableObjects}` (com `.gitkeep` e os `.meta` gerados pela Unity). Em `Assets/` ficam também os assets de projeto do URP (`UniversalRenderPipelineGlobalSettings`, `DefaultVolumeProfile`) e um `DefaultNetworkPrefabs.asset` vazio, regenerado pelo NGO.

### `.gitignore`
- Pasta do kit reservada (`/Assets/3DGamekit/` e `.meta`; nome a confirmar na Fase 1).
- Arquivos grandes do legado cobertos nos caminhos novos (`Legacy/...`) e nos antigos, até as outras máquinas migrarem.
- Capturas de tela na raiz (`Captura_de_tela_*.png`) ignoradas.

### Launcher (`Tools/unity.sh`)
- **Editor:** resolvido por `GAMEYUNIT_UNITY_EDITOR` → `/mnt/SSD/Unity/editors/<versão>` → `~/Unity/Hub/Editor/<versão>`, com a versão lida de `ProjectVersion.txt`.
- **Licença:** copiada para `.unity/` a partir de `/mnt/SSD/...` ou de `~/.config/unity3d/Unity` (onde o Hub a grava).
- **Asset Store:** a variável errada `UNITY_ASSET_STORE_CACHE_PATH` virou **`ASSETSTORE_CACHE_PATH`** → `.unity/asset-store`.
- **Binds do home:** `~/.cache`, `~/.config/unity3d` e `~/.local/share/unity3d` são redirecionados para `.unity/`. Se o destino não existe, a pasta-mãe é redirecionada, porque a raiz do sandbox é somente leitura.
- **Opções novas:**
  - `--print-paths`;
  - `--verify` (compila e roda todos os testes EditMode);
  - `--reopen` (reabre na última cena).
  - `--reopen-game`/`--reopen-play` chamam `GameYunit.Editor.EditorEntryPoints.*` quando esses métodos existirem; até lá reabrem sem `-executeMethod`. `--reopen-preview` foi removido (a cena de personagens está no legado).
- **Sem `wmctrl`:** os comandos de janela param com instrução de instalação; o `--status` funciona sem ele.

### Documentação
`README.md` (guia "Rodar localmente"), `CONTEXTO.md`, `AGENTS.md`, [Plano.md](Plano.md), esta página e [AlteracoesNoKit.md](AlteracoesNoKit.md) foram reescritos para o plano novo. Os documentos antigos estão em `Legacy/`.

## Verificação (executada nesta máquina)

| Verificação | Como | Resultado |
|---|---|---|
| Legado acessível | `git tag -n1`, `git branch` | `legacy/v1-pre-gamekit` e `legacy/v1` em `65b167c` |
| Sintaxe dos scripts | `bash -n` | ok |
| Caminhos | `Tools/unity.sh --print-paths` | Editor em `~/Unity/Hub/Editor/6000.3.27f2` (ok); licença de `~/.config/unity3d/Unity/licenses`; caches em `.unity/` |
| Sandbox | "Editor" falso (script) gravando nos destinos da Unity | `~/.local/share/unity3d`, `~/.config/unity3d`, `~/.cache`, `ASSETSTORE_CACHE_PATH` e `/tmp` caíram em `.unity/`; gravar em `~/` **foi bloqueado**; nada criado no home |
| Unity real + licença | `Tools/unity.sh -batchmode -quit -logFile Logs/Fase0-licenca.log` | Saída 0 em 68 s; licença resolvida pelo LicensingClient; 29 pacotes resolvidos no cache local; sem erros de compilação (não há scripts ainda) |
| Script do legado | `move_legacy_ignored.sh` num checkout simulado em `.unity/tmp` | FBX, PNG e o pacote do Medieval Kit movidos para `Legacy/`; pastas vazias removidas; nesta máquina: "nada a mover" |
| `.gitignore` | `git check-ignore` | Kit, FBX do legado, Medieval Kit e capturas ignorados; `.meta` do esqueleto versionáveis |

**Não verificado nesta fase:** importação e execução do 3D Game Kit, que é a Fase 1.

**Observação:** o log da Unity registra `Access token is unavailable`. O Editor no sandbox não recebe o login do Hub, o que pode exigir login dentro do Editor para baixar o kit pela My Assets.

## Roteiro manual (opcional)
1. `Tools/unity.sh --print-paths`: o Editor deve aparecer com `(ok)` e a licença com uma origem.
2. `Tools/unity.sh`: o Editor abre num projeto vazio (pasta `Assets/_Project` com 4 subpastas), sem erros no Console.

## Próxima fase
Fase 1: manifesto limpo do template URP 6.3, importação do 3D Game Kit (passo manual pela My Assets), revisão do diff de `ProjectSettings`, instalação de NGO/UTP/uGUI e auditoria dos pontos multiplayer do kit (`Docs/Fase1.md`).
