# GameYunit

# Contexto Inicial de Sistema

Atue como um Arquiteto de Software e Desenvolvedor Sênior de Unity (C#). Estamos iniciando o desenvolvimento de um jogo 3D estilo "Bullet Heaven/Survivors-like" em terceira pessoa.

- O jogo terá foco em Multiplayer Co-op local (TCP/IP) para 2 jogadores (Arquitetura Host-Client).
- Engine: Unity (2022 LTS ou superior).
- Framework de Rede: Netcode for GameObjects (NGO).
- Ambiente de Desenvolvimento: Linux (CachyOS).
- Arquitetura Restrita: Separação clara entre Lógica de Servidor (Server Authoritative) e Visualização de Cliente; uso obrigatório de Object Pooling para tudo que spawna em massa (inimigos, projéteis, xp); uso do padrão Observer (Events/Actions) para desacoplamento de sistemas.

# Tarefa 1: Fundação

1. Crie a estrutura de diretórios ideal no formato Unity (ex: Scripts/Player, Scripts/Network, Scripts/Core, Scripts/Enemies, Prefabs, etc.).
2. Gere o script `NetworkManagerSetup.cs` para gerenciar a inicialização do NGO, permitindo iniciar como Host ou conectar como Client em '127.0.0.1'.
3. Gere a interface e a classe base do `ObjectPoolManager.cs` que usaremos em todo o jogo para instanciar e reciclar GameObjects sem gargalos de performance.
4. Gere o script inicial `PlayerNetwork.cs` (herdando de NetworkBehaviour) com validação básica de `IsOwner` para garantir que apenas o jogador local envie inputs.

Não crie mecânicas de jogo ainda. Retorne apenas a estrutura de pastas e os scripts limpos, tipados e comentados.
