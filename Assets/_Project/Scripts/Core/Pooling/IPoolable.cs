namespace GameYunit.Core.Pooling
{
    /// <summary>
    /// Contrato opcional para componentes de objetos reciclados pelo <see cref="IObjectPoolManager"/>.
    /// Substitui a lógica que normalmente ficaria em Awake/Start/OnDestroy, já que uma instância
    /// pooled é criada uma única vez e reaproveitada muitas vezes.
    /// </summary>
    /// <remarks>
    /// Os componentes que implementam esta interface são descobertos uma única vez, na criação da
    /// instância (incluindo filhos inativos). Não há custo de GetComponent por spawn.
    /// </remarks>
    public interface IPoolable
    {
        /// <summary>
        /// Chamado logo após a instância sair do pool, já posicionada e ativa (depois do OnEnable).
        /// Use para resetar estado: vida, timers, velocidade, efeitos visuais etc.
        /// </summary>
        void OnSpawnedFromPool();

        /// <summary>
        /// Chamado imediatamente antes de a instância voltar ao pool (antes do OnDisable).
        /// Use para cancelar timers, soltar referências e parar efeitos.
        /// </summary>
        void OnReturnedToPool();
    }
}
