# Regras de contexto inical

1.  Fundação e Rede (Onde você está)
    precisa garantir que um cliente consegue se conectar a um host, instanciar um objeto vazio na cena e que o tráfego de rede esteja funcional. A infraestrutura de Object Pooling entra aqui porque jogos Survivors-like lidam com milhares de entidades (inimigos, XP, projéteis), e o Garbage Collector da Unity destruiria a performance sem um Pool desde o dia zero.

2.  O Personagem (Player Controller Networked)
    O Personagem (Player Controller Networked)
    Crie um bloco ou cápsula (placeholder) para representar o jogador.

        Movimentação: Implemente a lógica de input (WASD/Gamepad) apenas para o cliente local (IsOwner).

        Sincronização: Use o NetworkTransform do NGO para replicar a posição do Host para o Client e vice-versa.

        Câmera: Configure uma câmera em terceira pessoa que siga apenas o jogador local, ignorando os clones de rede dos outros jogadores.
