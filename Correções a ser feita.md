# Correções a ser feita

Lista do que está pendente em 03/11/2026. Estado verificado fica em [CONTEXTO.md](CONTEXTO.md);
esta lista é só o que **falta**, com o que já se sabe sobre cada item.

> **Aviso geral:** magias, personagens, configurações, vida e dano foram escritos e compilam, mas
> **nada disso foi verificado em partida**. Os únicos itens confirmados em execução são a correção da
> tela preta e a conexão co-op em UDP.

---

## 1. Bloqueando o teste

### 1.1 Arte das magias não importada
Os botões de magia e personagem mostram **nomes em texto** no lugar de ícones, e a conjuração não tem
**nenhum efeito visual** — `PlayEffectRpc` retorna cedo quando `EffectPrefab` é nulo.

Os dois pacotes já estão na conta do Rodrigo. Passo a passo no [README](README.md), seção
"Jogar com outra pessoa" e em [Docs/Magias.md](Docs/Magias.md).

### 1.2 Retratos dos personagens
Os pacotes importados são **ícones de magia**; não trazem retrato de personagem. Os quatro personagens
vão continuar em texto até alguém apontar sprites à mão no campo `Portrait` de
`Assets/_Project/Characters/*.asset`.

### 1.3 Conjuração no Host — corrigido, falta confirmar
O clique esquerdo não disparava a magia no Host. Causa: um único campo `_readyAt` servia ao dono e ao
servidor; no Host, que é os dois, o servidor lia a previsão local do dono e recusava a própria
conjuração (`Conjuração ignorada: ainda recarregando`, 4 ocorrências em `Logs/Teste-menu.log`).
Corrigido com `_ownerReadyAt` e `_serverReadyAt` separados. **Falta confirmar em partida.**

---

## 2. Fase 2 — não fechada

### 2.1 Replicação de movimento sem prova
O ensaio automatizado nunca chegou a medir movimento e animação. O processo cliente **encerra de forma
ordenada** no instante em que o roteiro ativa `UseTestInput`, sem exceção no log; o Host registra
`Jogador desapareceu durante o ensaio`. **Causa não identificada** — é o item mais importante da lista.
Enquanto não fechar, `localDistance`, `remoteDistance`, `jumpHeight`, `remoteWalkAnimation` e
`remoteJumpAnimation` seguem sem medição válida.

### 2.2 WebSocket (TCP) nunca ensaiado entre processos
Só o UDP foi verificado. O caminho WebSocket existe no código e no menu, mas nunca rodou.

### 2.3 Partida entre duas máquinas
Tudo foi testado em `127.0.0.1`. LAN real nunca foi exercitada.

---

## 3. Combate — escrito, não verificado

### 3.1 Dano entre jogadores está ligado
`SpellCaster` aplica dano a **qualquer** `NetworkHealth` na área menos o próprio conjurador — ou seja,
hoje há **fogo amigo**. Foi assim de propósito, para haver um alvo enquanto não existem inimigos, mas
**precisa ser desligado** quando os inimigos entrarem (filtrar por camada em `_targetLayers`, que hoje
está em "tudo").

### 3.2 Sem inimigos na cena
`Level1_Coop` é gerada com `Enemies`, `Destructibles` e `Level01Gameplay` removidos. Não há nada para
atacar além do parceiro.

### 3.3 Morte sem consequência
`NetworkHealth.DiedServer` dispara, mas **ninguém escuta**. Não há caído, nem revive, nem game over —
é a Fase 3 do [Plano](Docs/Plano.md). Hoje a vida chega a zero e o jogador continua andando.

### 3.4 `Damageable` do kit segue desabilitado
`NetworkHealth` foi escrito do zero em vez de pontear o `Damageable`, que está desligado no prefab.
A decisão nº 1 do CONTEXTO previa a ponte; convém decidir se ela ainda vale. O Bloco 4 de vida do
legado é de outra pessoa — **alinhar antes de reaproveitar**.

---

## 4. Não existe ainda

- **XP e nível** — `Legacy/_Project/Scripts/Progression/` tem `PlayerLevel`, `XpDropService` e `XpGem`
  para reaproveitar (Fase 4).
- **Upgrades periódicos** com pausa global só na escolha (Fase 4).
- **Spawner de horda**, teto inicial de 40 (Fase 4).
- **Fim de partida e reinício** (Fase 4).
- **Mixer de áudio** — os sliders de Música e Efeitos existem no menu mas só `Volume geral` tem efeito
  real (`AudioListener.volume`); os outros dois guardam o valor e não roteiam nada.

---

## 5. Riscos e dívidas técnicas

### 5.1 Versão de protocolo é constante, não hash
`ConnectionRules.Version` é um número fixo. **Builds diferentes conectam sem reclamar** e podem divergir
em silêncio. Antes de distribuir cópias, convém derivar a versão de algo que mude a cada build.

### 5.2 Licença do sandbox não se atualiza
O launcher só copia a licença quando `.unity/config/unity3d/Unity/licenses` ainda **não existe**. A cópia
envelhece e todo build em `-batchmode` passa a falhar com `No valid Unity Editor license found`
(erro 404 de entitlements). Hoje a correção é manual:
```bash
cp -a ~/.config/unity3d/Unity/licenses/. .unity/config/unity3d/Unity/licenses/
```
**Corrigir no `Tools/unity.sh`:** copiar sempre que a origem for mais nova.

### 5.3 Só há build Linux
`CoopProjectSetup.BuildLinux` é o único alvo. Não há método para Windows.

### 5.4 Escala de renderização e MSAA sujam o asset do URP
`GameSettings.ApplyPipeline` escreve em `UniversalRenderPipelineAsset`, porque a Unity não expõe API de
instância. Numa build a mudança é só em memória, mas **dentro do Editor o arquivo fica sujo no disco** e
pode entrar num commit sem querer. Conferir `git status` depois de mexer nas configurações.

### 5.5 Personagens são variações da Ellen
Todos os modelos do kit usam rig `Generic` com esqueletos próprios: **não há retargeting**, e as
animações da Ellen estão presas aos ossos dela por nome. Pôr um Chomper ou Grenadier como jogável
exigiria converter os FBX para Humanoid — arquivos originais do kit, que o projeto proíbe editar — e nem
assim funcionaria para o Chomper, que é quadrúpede. `CharacterDefinition.BodyPrefab` existe para um
modelo externo **de esqueleto compatível**; outro rig quebra a animação.

### 5.6 `Assets.tar.gz` já entrou no histórico uma vez
Dois commits do GitHub Desktop levaram 2,5 GB para o histórico e inflaram o `.git` para 2,8 GB. Foi
desfeito e o `.gitignore` passou a ignorar `/*.tar.gz` na raiz. **Não versionar arquivos grandes de
transporte.**

### 5.7 Warning de serialização do Cinemachine
`CompositionModifier` do Cinemachine 3.1.3. Nenhum arquivo original foi alterado para suprimir.

---

## 6. Nada disso está commitado

Magias, personagens, configurações, vida e dano estão **só no diretório de trabalho**. O último commit é
`fb23f35` (correção da tela preta + co-op UDP verificado). Convém testar antes de fechar num commit —
o projeto registra em `CONTEXTO.md` só o que foi verificado.
