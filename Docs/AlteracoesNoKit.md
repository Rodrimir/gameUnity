# Alterações sobre o 3D Game Kit

Os arquivos originais do kit **nunca são editados** (ver `AGENTS.md`). Quando um comportamento do kit precisa mudar para o co-op, o script é copiado para `Assets/_Project/Scripts/KitBridge/` com namespace `GameYunit.*`, e a referência é trocada no prefab ou na cena **copiados**. Cada cópia entra nesta tabela.

| Original do kit | Cópia em `Assets/_Project` | Onde a referência foi trocada | O que mudou e por quê | Fase |
|---|---|---|---|---|
| `Scripts/Game/Player/PlayerInput.cs` | `Scripts/KitBridge/Player/PlayerInput.cs` | `Prefabs/EllenCoop.prefab` | Sem singleton; uma instância por Ellen, habilitada apenas no dono; menu local bloqueia o input; injeção opt-in para o ensaio. | 2 |
| `Scripts/Game/Player/PlayerController.cs` | `Scripts/KitBridge/Player/PlayerController.cs` | `Prefabs/EllenCoop.prefab` | Sem singleton; root motion e cálculo de movimento apenas no dono; triggers via NetworkAnimator; sem respawn/fade single-player. | 2 |
| `EllenStaffEffect`, `EllenRespawnEffect`, `EllenSetTargetableSMB` | `Scripts/KitBridge/Player/` (mesmos nomes) | `Prefabs/EllenCoop.controller` (cópia do animator Ellen) | Referências ao controlador cooperativo, preservando os eventos e efeitos originais. | 2 |

## Cenas copiadas

| Cena original | Cópia | O que foi desativado na cópia (puzzles, gatilhos, cutscenes, fluxo de cenas) | Fase |
|---|---|---|---|
| `Scenes/GamePlay/Level1.unity` | `Assets/_Project/Scenes/Level1_Coop.unity` (local, ignorada) | Removidos Ellen, inimigos, puzzles, destrutíveis, informações, cutscenes, morte, UI e fluxo single-player. Ambiente, áudio ambiente, iluminação e rig de câmera preservados. Dois marcadores de nascimento adicionados. | 2 |

A preparação reproduzível está em `KitBridge/Editor/CoopProjectSetup.cs`. Prefab e animator cooperativos são versionados; Level1_Coop é gerada localmente com GUID fixo. Os originais do kit não são gravados. O Damageable do prefab fica desabilitado e as armas ficam sem alvos de dano nesta fase; a ponte de vida pertence à Fase 3.
