using System;
using GameYunit.Enemies;
using Unity.Netcode;
using UnityEngine;

namespace GameYunit.Progression
{
    /// <summary>Pedido de drop publicado pelo inimigo e atendido pelo <see cref="XpDropService"/>.</summary>
    public readonly struct XpDropRequest
    {
        public readonly XpGem Prefab;
        public readonly Vector3 Position;
        public readonly int Value;

        public XpDropRequest(XpGem prefab, Vector3 position, int value)
        {
            Prefab = prefab;
            Position = position;
            Value = value;
        }
    }

    /// <summary>
    /// Componente adicional do prefab de inimigo: ao morrer no servidor, publica um pedido de gema de XP.
    /// Não altera EnemyAI nem o sistema de vida; integra-se apenas pela API pública existente.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Modo padrão: qualquer despawn do inimigo no servidor durante a partida conta como morte. Pelo roteiro
    /// do Bloco 4, a morte chama <c>EnemyAI.ReturnToPoolServer()</c>, que gera esse despawn; nada precisa ser editado.
    /// </para>
    /// <para>
    /// Se no futuro houver despawns que não são morte (ex.: inimigo removido por ficar longe demais),
    /// desligue <c>Drop On Server Despawn</c> e chame <see cref="NotifyKilledServer"/> a partir do evento de morte.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyAI))]
    public sealed class XpDropper : MonoBehaviour
    {
        [SerializeField] private XpGem _gemPrefab;

        [SerializeField, Min(1)] private int _xpValue = 1;

        [Tooltip("Altura da gema acima do pivot do inimigo.")]
        [SerializeField] private float _dropHeight = 0.5f;

        [Tooltip("Trata qualquer despawn no servidor como morte. Desligue para usar só NotifyKilledServer().")]
        [SerializeField] private bool _dropOnServerDespawn = true;

        private EnemyAI _enemy;

        /// <summary>[Servidor] Observer global de drops; o XpDropService assina.</summary>
        public static event Action<XpDropRequest> OnServerDropRequested;

        private void Awake() => _enemy = GetComponent<EnemyAI>();

        // Instâncias pooled são reativadas a cada uso, então a assinatura acompanha o ciclo do pool.
        private void OnEnable() => _enemy.OnServerDespawned += HandleEnemyDespawned;

        private void OnDisable() => _enemy.OnServerDespawned -= HandleEnemyDespawned;

        /// <summary>[Servidor] Solicita o drop explicitamente (para integração direta com o evento de morte).</summary>
        public void NotifyKilledServer()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || !manager.IsListening || manager.ShutdownInProgress ||
                _gemPrefab == null)
            {
                return;
            }

            OnServerDropRequested?.Invoke(new XpDropRequest(_gemPrefab, transform.position + Vector3.up * _dropHeight,
                _xpValue));
        }

        private void HandleEnemyDespawned(EnemyAI enemy)
        {
            if (_dropOnServerDespawn)
            {
                NotifyKilledServer();
            }
        }

        // Com "Enter Play Mode Options" (sem domain reload) eventos estáticos sobrevivem entre sessões de Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => OnServerDropRequested = null;
    }
}
