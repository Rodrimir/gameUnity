# GameYunit

Survivors-like 3D em co-op para 2 jogadores (Host–Client por IP direto), sendo refeito sobre o **3D Game Kit** da Unity. Estado atual e o que já foi verificado: [CONTEXTO.md](CONTEXTO.md). Plano aprovado: [Docs/Plano.md](Docs/Plano.md).

## Rodar localmente (leia primeiro)

O clone **não traz tudo** o que o projeto precisa. Alguns itens são grandes, gerados pela Unity, presos à sua conta ou proibidos de redistribuir.

### O que falta no clone

| Item | Por que não está no Git | Como obter |
|---|---|---|
| **3D Game Kit** (Asset Store, ~2,2 GB) | A EULA da Asset Store proíbe redistribuir; ignorado em `/Assets/3DGamekit/` | Importar pela sua conta (passo 5). |
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

   O download fica em `.unity/asset-store/`. Os detalhes (pasta criada, configurações que o kit altera) são confirmados na Fase 1 e registrados em [Docs/Fase1.md](Docs/Fase1.md).
6. **Verificar:** `Tools/unity.sh --verify` compila em batch e roda os testes EditMode; os resultados ficam em `Logs/TestResults.xml` e `Logs/Tests.log`.

### Teste em rede no Linux
O Multiplayer Play Mode da Unity não existe no Linux. Para testar o co-op, rode o Editor como Host e uma build Linux de Development (em `Builds/`) como Client, em `127.0.0.1:7777`, ou duas builds.

## Organização

| Caminho | Conteúdo |
|---|---|
| `Assets/_Project/` | Código, prefabs, cenas e dados do jogo novo |
| `Assets/3DGamekit/` | 3D Game Kit (ignorado; nunca editar) |
| `Docs/` | Plano, documentação por fase e alterações em cópias de scripts do kit |
| `Tools/unity.sh` | Launcher da Unity em sandbox (`--print-paths`, `--status`, `--verify`, `--reopen*`) |
| `Legacy/` | Projeto anterior ao 3D Game Kit, só para consulta (fora de `Assets/`, não compila) |

O projeto anterior também está preservado na tag `legacy/v1-pre-gamekit` e na branch `legacy/v1`.
