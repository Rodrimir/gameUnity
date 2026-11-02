using UnityEngine;

namespace GameYunit.Enemies
{
    /// <summary>
    /// Estratégia opcional de navegação consultada pelo EnemyAI no servidor. Sem implementação no prefab,
    /// o inimigo anda em linha reta até o alvo (comportamento do Bloco 2).
    /// </summary>
    public interface IEnemySteering
    {
        /// <summary>Ponto intermediário para onde andar agora, a caminho de <paramref name="destination"/>.</summary>
        Vector3 GetSteeringPoint(Vector3 position, Vector3 destination);
    }
}
