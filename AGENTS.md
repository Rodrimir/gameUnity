# Preferências do projeto

## Comunicação
- Responda em português brasileiro e acompanhe respostas finais e explicações substantivas com um resumo falado pelo MCP `voz_tts`, ferramenta `falar_texto`, personalidade `tecnico`. Consulte `listar_personalidades` uma vez no início da conversa. Preserve a voz padrão e as vozes removidas do servidor. Se o áudio falhar ou o MCP não estiver disponível, informe e continue por texto.
- Se o player do MCP falhar, o cliente local já validado (máquina do Labubu) é `/home/labubu/mcp-tts/venv/bin/python /home/labubu/.codex/scripts/voice_mcp.py speak --text 'resumo'`; ele aguarda a reprodução e usa a mesma personalidade. Não afirme que houve áudio sem confirmação de sucesso.

## Ambiente
- Nunca faça downloads, extrações, instalações de dependências, builds, caches, logs ou arquivos temporários fora desta raiz. Use as pastas locais ignoradas pelo Git (`.unity/`, `Library/`, `Temp/`, `Logs/`, `Builds/`). Não use `/tmp` nem um projeto Unity externo para trabalhar neste jogo.
- Execute a Unity só por `Tools/unity.sh`. O launcher roda o Editor num sandbox (`bwrap`) que só grava na raiz; caches do Package Manager e da Asset Store, licença copiada, dados e temporários ficam em `.unity/`. A instalação do Editor é somente lida. `Tools/unity.sh --print-paths` mostra o Editor, a licença e os caches em uso.
- A cada atualização visível na Unity (cena, prefab, material, UI ou configuração visual), feche a instância anterior graciosamente, espere a janela fechar e o `Temp/UnityLockfile` ser liberado, e só então abra uma única instância nova. Use `Tools/unity.sh --reopen`, `--reopen-game` ou `--reopen-play` (exigem `wmctrl`); nunca abra outra instância enquanto o projeto estiver bloqueado. Mudanças apenas de código/documentação sem efeito visual não exigem reiniciar o Editor.

## Projeto
- Consulte `CONTEXTO.md` (estado real e o que foi verificado) e `Docs/Plano.md` (plano aprovado) antes de continuar. Trabalhe uma fase por vez; ao fim de cada uma, teste, atualize `CONTEXTO.md` e `Docs/FaseN.md` só com o que foi verificado, faça o commit e pare para confirmação.
- O jogo está sendo refeito sobre o **3D Game Kit** (Asset Store). **Nunca edite os arquivos originais do kit.** Cenas do kit usadas no jogo são copiadas para `Assets/_Project/Scenes/`; mudanças de comportamento vão em scripts novos em `Assets/_Project/`. Se um script do kit precisar mudar, copie-o com namespace `GameYunit.*`, troque a referência no prefab copiado e registre em `Docs/AlteracoesNoKit.md`.
- Código que referencia tipos do kit fica em `Assets/_Project/Scripts/KitBridge/`, sem asmdef (o kit compila no Assembly-CSharp, que asmdefs não enxergam). As asmdefs `GameYunit.*` nunca dependem do kit.
- O projeto anterior (Blocos 1–6, inclusive o Bloco 4 de vida feito por outra pessoa) está em `Legacy/` e na tag `legacy/v1-pre-gamekit`. A vida agora vem do `Damageable` do kit; antes de reaproveitar algo do Bloco 4, alinhe com quem o fez.
- Mantenha o progresso disponível em uma cena Unity; abra o Editor quando houver uma versão para inspecionar.
