Atue como um Arquiteto de Software e Desenvolvedor Sênior de Unity (C#). Estamos desenvolvendo um jogo 3D estilo "Bullet Heaven/Survivors-like" em terceira pessoa com estética retrô PSX.

Contexto Técnico e Arquitetura:

    Engine & Rede: Unity (2022 LTS ou superior) com Netcode for GameObjects (NGO).

    Multiplayer: Co-op local (TCP/IP) para 2 jogadores (Host-Client).

    Padrões Obrigatórios: Arquitetura Server Authoritative (Lógica no Servidor, Visual no Cliente), Object Pooling para tudo que spawna em massa (inimigos, projéteis, XP), e padrão Observer (Eventos C#) para desacoplamento.

    Estética PSX: Uso de Render Textures (320x240), texturas em Point (no filter), áudio em Mono/11025Hz e shaders URP-PSX (Vertex Snapping/Jitter e Affine Texture Mapping).

Regra de Execução:
Não gere o código do jogo inteiro agora. Nós vamos programar em blocos. Ao final da execução de cada bloco, você não deve prosseguir automaticamente. Em vez disso, forneça um breve resumo do que foi feito e me dê 2 a 3 sugestões/escolhas de como abordar o próximo bloco (ex: decisões de design, formas de resolver um problema mecânico). Aguarde minha confirmação para iniciar o próximo bloco.

Abaixo está o nosso roteiro. Diga apenas "Entendido! O ambiente foi configurado. Envie o Bloco 1 quando estiver pronto para começarmos a programar a movimentação."

(Nota para o usuário: Envie os blocos abaixo um de cada vez conforme o avanço do projeto)
Bloco 1: Movimentação, Câmera e Sincronização

Agora vamos implementar a movimentação. Crie o PlayerController.cs. A movimentação deve ocorrer através do CharacterController da Unity, mas os cálculos de input devem ser executados localmente (IsOwner) e a posição sincronizada com o Servidor usando NetworkTransform ou variáveis de rede. Crie também o CameraController.cs para seguir dinamicamente o jogador ativo em terceira pessoa (top-down angular), garantindo que a câmera só siga o objeto onde IsOwner for verdadeiro. Ao final, me dê opções de como lidar com a rotação do personagem (ex: virar para o mouse vs virar para a direção do movimento).
Bloco 2: Sistema de Spawn e IA Básica dos Inimigos

Implemente o sistema de hordas. Crie o EnemyAI.cs e o EnemySpawner.cs. O Spawner deve ser estritamente Server-Side (IsServer), usando o nosso ObjectPoolManager para ativar inimigos em um raio ao redor dos jogadores conectados. O EnemyAI deve localizar o jogador mais próximo usando Vector3.Distance (calculado no servidor) e se mover em direção a ele. Sincronize a posição dos inimigos para os clientes. Ao final, me dê opções sobre o padrão de spawn (ondas temporizadas vs fluxo contínuo).
Bloco 3: Ataque Automático e Projéteis de Rede

Vamos criar o sistema de ataque automático do jogador. Crie o AutoAttackSystem.cs e o Projectile.cs. O jogador deve verificar num raio qual o inimigo mais próximo e 'atirar' em intervalos de tempo regulares. Como os projéteis serão muitos, o input de atirar pode ser previsto no Cliente (Client-side prediction visual), mas a ativação real do projétil a partir do Pool e a verificação de colisão devem ocorrer no Servidor. Ao final, me pergunte que tipo de comportamento queremos para a primeira arma.
Bloco 4: Sistema de Vida, Dano e Desacoplamento (Observer)

Implemente o sistema de atributos. Crie HealthComponent.cs (Server-Side com NetworkVariable para a vida atual). Quando o projétil colidir com o inimigo no Servidor, reduza a vida. Quando a vida chegar a zero, dispare um evento C# OnDeath. O EnemyAI deve escutar OnDeath para se devolver ao Object Pool. Crie um script de UI local que escute eventos de alteração da NetworkVariable de vida do jogador para atualizar a barra de HP na tela.
Bloco 5: Drop de XP e Sistema de Níveis

Implemente o sistema de progressão. Quando um inimigo morrer (no Servidor), ele deve solicitar ao ObjectPoolManager uma Gema de XP, ativando-a na sua posição. Crie o XpGem.cs e o PlayerLevel.cs. Quando o jogador colidir fisicamente com a gema, a colisão é processada no servidor, a gema volta para o Pool, a NetworkVariable de XP do jogador aumenta e um evento OnLevelUp é disparado caso atinja o limite. Ao final, me dê as opções de como lidar com a tela de Level Up no multiplayer (pausar o tempo global vs UI sobreposta em tempo real).
Bloco 6: Mapa, NavMesh e Estética PSX

Vamos configurar o cenário e a estética. Forneça o script para inicializar a Render Texture de 320x240 no Canvas para simular a baixa resolução do PSX e instruções rápidas via script para forçar os materiais e sons no estilo retrô. Além disso, crie o setup para o NavMesh, permitindo que a IA inimiga evite obstáculos estáticos no cenário (que serão marcados como Navigation Static). Ao final, me pergunte se prefiro um mapa fechado tipo arena ou um mapa com geração infinita/treadmill.
