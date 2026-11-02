# Bloco 6 — Mapa, NavMesh e estética PSX

## Convivência com o Bloco 4

Como no Bloco 5, quase tudo entrou em **arquivos e assemblies novos**. A UI da tela PSX ficou numa asmdef própria (`GameYunit.Rendering`, que referencia `UnityEngine.UI`), para não alterar a `GameYunit.Runtime.asmdef`, que o Bloco 4 provavelmente vai editar para a barra de HP. As ferramentas de NavMesh também têm asmdef própria (`GameYunit.Editor.Navigation`), que só compila quando o pacote AI Navigation está instalado.

Só dois arquivos existentes foram alterados, com trechos pequenos e isolados:

| Arquivo | Mudança | Motivo |
|---|---|---|
| `Enemies/EnemyAI.cs` | Campo `_steering` (obtido no `Awake`) e cerca de 10 linhas no cálculo de direção do `FixedUpdate` | Consultar a navegação opcional (`IEnemySteering`). A parada continua medida até o alvo. Sem `NavMeshSteering` no prefab, o comportamento é idêntico ao do Bloco 2. |
| `Player/PlayerAimInput.cs` | 1 `using` e 2 linhas na leitura do mouse | Com a RenderTexture, a câmera passa a ter 320x240 px e o mouse precisa ser convertido da tela para os pixels da câmera. Sem a tela PSX, a conversão é a identidade. |

Se o Bloco 4 também mexer no `FixedUpdate` do `EnemyAI`, o conflito fica restrito a esse trecho; o resto do Bloco 4 (assinatura de `OnDeath`, retorno ao pool) não toca nessas linhas.

## Tela PSX (RenderTexture 320x240)

`PsxRenderTextureSetup` (na **Main Camera**):
- cria uma RenderTexture `320x240` com filtro **Point**, sem MSAA e sem mipmaps, e a define como `targetTexture` da câmera;
- exibe a imagem num Canvas **Screen Space Overlay** (`RawImage` + `AspectRatioFitter` em *Fit In Parent*), com letterbox;
- cria uma câmera vazia que só limpa a tela, para as faixas do letterbox não mostrarem lixo;
- destrói tudo isso no `OnDisable`, sem salvar nada na cena.

Opções:
- **Match Screen Aspect** mantém os 240 px de altura e calcula a largura pela janela, sem faixas laterais.
- **Canvas Sort Order** (`-100`): HUDs com Sort Order maior aparecem por cima em resolução nativa. Para um HUD também pixelado, use um Canvas *Screen Space - Camera* apontando para a Main Camera.
- Os HUDs de debug (IMGUI) continuam em resolução nativa.

O retângulo exibido é registrado em `CameraViewportMapping` (no Core). A mira do mouse do Bloco 1 continua correta, inclusive ignorando cliques nas faixas do letterbox.

## Shader PSX e materiais

`Assets/_Project/Art/Shaders/PSXLit.shader` — **GameYunit/PSX Lit** (URP):
- **Vertex snapping:** posições arredondadas para a grade da resolução interna, o que dá o tremido característico. `Snap Multiplier` por material ajusta a força.
- **Affine texture mapping:** UV sem correção de perspectiva.
- **Iluminação Gouraud por vértice:** luz principal + ambiente (SH), sem sombras e sem luzes adicionais, como no PSX. Há a opção *Sem iluminação*, Alpha Cutoff, Cull e fog do URP.
- **Parâmetros globais:** `_PsxSnapResolution` e `_PsxAffineStrength` são publicados pelo `PsxRenderTextureSetup`. Sem ele, o shader se comporta como um lit simples por vértice.

Menus **GameYunit → PSX** (aceitam pastas, assets e objetos da cena):
- **Aplicar importação retrô (texturas e áudio) na seleção** — use na pasta do mapa importado.
- **Converter materiais da seleção para PSX Lit** — copia textura, tiling e cor de materiais URP Lit/Unlit ou legados, e aplica a regra de textura às texturas usadas. Materiais embutidos em FBX são somente leitura: extraia-os em *Model Importer → Materials → Extract Materials* e rode o menu de novo.

Importação automática (`PsxAssetPostprocessor`):
- **Texturas** em `Assets/_Project/Art` (exceto subpastas `/UI/`): filtro Point, sem mipmaps, sem compressão, no máximo **256 px** (limite da VRAM do PSX).
- **Áudios** em `Assets/_Project/Audio`: **mono, 11025 Hz**. Efeitos ficam em ADPCM *Decompress On Load*; `/Music/` é transmitido em Vorbis.
- Para isentar um asset, escreva `psx-ignore` em *User Data* do importer (Inspector em modo Debug).

## NavMesh

1. Instale **AI Navigation** (`com.unity.ai.navigation`) pelo Package Manager. Isso habilita a assembly `GameYunit.Editor.Navigation`.
2. Prepare o mapa com **GameYunit → Mapa → Preparar mapa selecionado** (Bloco 3): colliders e flags estáticas.
3. Com a raiz do mapa selecionada, use **GameYunit → Mapa → Configurar e assar NavMesh do mapa selecionado**:
   - adiciona um `NavMeshSurface` na raiz, coletando **só os filhos** e **só colliders** (jogadores e inimigos ficam fora do bake);
   - objetos **não estáticos** com collider recebem `NavMeshModifier` *Ignore From Build*;
   - assa e salva o `NavMeshData` como asset na pasta da cena.
4. **Navigation Static** é uma flag legada que o `NavMeshSurface` não lê. Aqui, qualquer flag Static marca o objeto como piso ou obstáculo do NavMesh, e isso inclui a Navigation Static e as flags aplicadas pelo preparo do mapa. Os parâmetros do agente padrão (*Humanoid*: raio 0,5, altura 2) coincidem com a cápsula do inimigo.
5. No `Enemy.prefab`, adicione **NavMeshSteering**. Ele calcula o caminho com `NavMesh.CalculatePath` a cada 0,5 s (±25%, para espalhar o custo) e entrega o próximo canto ao `EnemyAI`. O movimento continua no `CharacterController`, com a mesma gravidade, colisão e replicação por `NetworkTransform`. Não há `NavMeshAgent`, que disputaria a posição com o `CharacterController`. Sem NavMesh na cena, ou com o inimigo fora dele, a navegação volta à linha reta.

## Verificação

`MapAndRenderingTests` (EditMode, sem sessão de rede):
- **Mapeamento do mouse:** com a imagem 4:3 centralizada numa tela 1000x600, o centro da tela vira o pixel (160, 120) da RenderTexture, e a faixa do letterbox é rejeitada. Uma câmera sem RenderTexture mantém as coordenadas da tela.
- **NavMesh:** com um NavMesh assado em runtime sobre um chão com uma parede de 6 m, o primeiro canto contorna a parede em vez de atravessá-la.
- **Sem NavMesh:** a navegação devolve o próprio destino.

**Estado:** estes scripts e o shader **ainda não foram compilados nem executados**. A máquina em que foram escritos não tem Unity. Abra o projeto na 6000.3.27f2, confira o Console (shader incluso) e rode o Test Runner.

Teste manual:
- **Tela:** com `PsxRenderTextureSetup` na câmera, a imagem fica pixelada (Point), em 4:3 com letterbox. Redimensione a janela; com *Match Screen Aspect*, as faixas somem.
- **Mira:** o mouse continua mirando exatamente sob o cursor, e clicar nas faixas não altera a mira.
- **Shader:** objetos com PSX Lit tremem levemente ao mover a câmera (snap), e texturas em chão grande "nadam" (afim). Desligue *Vertex Snapping* e zere *Affine Strength* para comparar.
- **NavMesh:** coloque um obstáculo estático entre o jogador e uma horda: os inimigos o contornam. Os dois peers veem o mesmo trajeto, porque só o Host calcula.

## Próxima decisão

Formato do mapa: arena fechada ou geração infinita/treadmill. Veja as opções ao final do bloco.
