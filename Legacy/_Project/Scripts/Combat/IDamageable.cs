using UnityEngine;

namespace GameYunit.Combat
{
    /// <summary>
    /// Contrato para qualquer coisa que recebe dano. Chamado somente no servidor.
    /// O HealthComponent do Bloco 4 implementará esta interface; armas não conhecem a implementação.
    /// </summary>
    public interface IDamageable
    {
        /// <param name="amount">Dano bruto, já calculado pela arma.</param>
        /// <param name="sourceClientId">Client dono do disparo (para créditos de kill/estatísticas).</param>
        /// <param name="hitPoint">Ponto de impacto em world space.</param>
        void ApplyDamageServer(float amount, ulong sourceClientId, Vector3 hitPoint);
    }
}
