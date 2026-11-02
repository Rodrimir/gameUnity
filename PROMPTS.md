Prompts Subsequentes por Blocos de Desenvolvimento
Use a lista abaixo em ordem no Claude Code assim que a etapa anterior for concluída e os scripts estiverem implementados na Unity.

# Bloco 1: Movimentação, Câmera e Sincronização

    "Agora vamos implementar a movimentação. Crie o PlayerController.cs. A movimentação deve ocorrer através do CharacterController da Unity, mas os cálculos de input devem ser executados localmente (IsOwner) e a posição sincronizada com o Servidor usando NetworkTransform ou variáveis de rede. Crie também o CameraController.cs para seguir dinamicamente o jogador ativo em terceira pessoa (top-down angular), garantindo que a câmera só siga o objeto onde IsOwner for verdadeiro."

# Bloco 2: Sistema de Spawn e IA Básica dos Inimigos

    "Implemente o sistema de hordas. Crie o EnemyAI.cs e o EnemySpawner.cs. O Spawner deve ser estritamente Server-Side (IsServer), usando o nosso ObjectPoolManager para ativar inimigos em um raio ao redor dos jogadores conectados. O EnemyAI deve localizar o jogador mais próximo usando Vector3.Distance (calculado no servidor) e se mover em direção a ele. Sincronize a posição dos inimigos para os clientes."

# Bloco 3: Ataque Automático e Projéteis de Rede

    "Vamos criar o sistema de ataque automático do jogador. Crie o AutoAttackSystem.cs e o Projectile.cs. O jogador deve verificar num raio qual o inimigo mais próximo e 'atirar' em intervalos de tempo regulares. Como os projéteis serão muitos, o input de atirar pode ser previsto no Cliente (Client-side prediction visual), mas a ativação real do projétil a partir do Pool e a verificação de colisão devem ocorrer no Servidor."

# Bloco 4: Sistema de Vida, Dano e Desacoplamento (Observer)

    "Implemente o sistema de atributos. Crie HealthComponent.cs (Server-Side com NetworkVariable para a vida atual). Quando o projétil colidir com o inimigo no Servidor, reduza a vida. Quando a vida chegar a zero, dispare um evento C# OnDeath. O EnemyAI deve escutar OnDeath para se devolver ao Object Pool. Crie um script de UI local que escute eventos de alteração da NetworkVariable de vida do jogador para atualizar a barra de HP na tela."

# Bloco 5: Drop de XP e Sistema de Níveis

    "Implemente o sistema de progressão. Quando um inimigo morrer (no Servidor), ele deve solicitar ao ObjectPoolManager uma Gema de XP, ativando-a na sua posição. Crie o XpGem.cs e o PlayerLevel.cs. Quando o jogador colidir fisicamente com a gema, a colisão é processada no servidor, a gema volta para o Pool, a NetworkVariable de XP do jogador aumenta e um evento OnLevelUp é disparado caso atinja o limite."
