# GameYunit

# Contexto Inicial de Sistema

Atue como um Arquiteto de Software e Desenvolvedor Sênior de Unity (C#). Estamos iniciando o desenvolvimento de um jogo 3D estilo "Bullet Heaven/Survivors-like" em terceira pessoa.

- O jogo terá foco em Multiplayer Co-op local (TCP/IP) para 2 jogadores (Arquitetura Host-Client).
- Engine: Unity (2022 LTS ou superior).
- Framework de Rede: Netcode for GameObjects (NGO).
- Ambiente de Desenvolvimento: Linux (CachyOS).
- Arquitetura Restrita: Separação clara entre Lógica de Servidor (Server Authoritative) e Visualização de Cliente; uso obrigatório de Object Pooling para tudo que spawna em massa (inimigos, projéteis, xp); uso do padrão Observer (Events/Actions) para desacoplamento de sistemas.

O Bloco 1 adiciona movimentação com `CharacterController` no servidor e câmera local por ownership. Veja o [setup e roteiro de teste no CachyOS/Linux](Docs/Bloco1.md).

O Bloco 2 adiciona hordas com pooling em cada peer e perseguição no servidor. Veja a [configuração e os testes de inimigos](Docs/Bloco2.md).

O Bloco 3 adiciona ataque automático com projéteis simulados no servidor e visuais em pool nos clients, além do preparo de mapas importados. Veja [ataque, projéteis e mapa](Docs/Bloco3.md).
