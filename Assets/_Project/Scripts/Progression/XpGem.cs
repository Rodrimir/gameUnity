using System;
using GameYunit.Core.Pooling;
using Unity.Netcode;
using UnityEngine;

namespace GameYunit.Progression
{
    /// <summary>
    /// Gema de XP. É um NetworkObject reciclado pelo NetworkObjectPool: o servidor decide o spawn,
    /// e cada peer retira a representação do próprio pool. Clients que entram depois também a recebem.
    /// A coleta é processada só no servidor, pelo trigger contra o CharacterController do jogador.
    /// </summary>
    /// <remarks>
    /// Não usa NetworkTransform: a gema é estática e a posição viaja no spawn.
    /// O valor de XP existe apenas no servidor.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(SphereCollider), typeof(Rigidbody))]
    public sealed class XpGem : NetworkBehaviour, IPoolable
    {
        [Tooltip("Valor usado quando o spawn não informa outro.")]
        [SerializeField, Min(1)] private int _defaultValue = 1;

        [Tooltip("Raio do trigger de coleta.")]
        [SerializeField, Min(0.05f)] private float _pickupRadius = 0.6f;

        [Tooltip("Giro puramente visual, em graus/s (local em cada peer).")]
        [SerializeField] private float _spinSpeed = 90f;

        private int _value;
        private bool _collected;

        /// <summary>[Servidor] XP concedido na coleta.</summary>
        public int Value => _value;

        /// <summary>[Servidor] Gema saiu da rede (coletada ou limpeza). Usado pelo XpDropService para contar a população.</summary>
        public event Action<XpGem> OnServerDespawned;

        /// <summary>[Servidor] Gema coletada pelo jogador informado, antes do despawn.</summary>
        public event Action<XpGem, PlayerLevel> OnServerCollected;

        private void Awake()
        {
            // Trigger + Rigidbody cinemático: eventos de trigger confiáveis com CharacterController, sem física simulada.
            SphereCollider trigger = GetComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = _pickupRadius;

            Rigidbody body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _collected = false;
            if (IsServer && _value <= 0)
            {
                _value = _defaultValue;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                OnServerDespawned?.Invoke(this);
            }

            ResetState();
            base.OnNetworkDespawn();
        }

        /// <summary>[Servidor] Define o valor logo após o spawn; a coleta só ocorre no próximo passo de física.</summary>
        internal void InitializeServer(int value) => _value = Mathf.Max(1, value);

        /// <summary>[Servidor] Funde o XP de outro drop nesta gema (limite de gemas ativas).</summary>
        internal void AddValueServer(int amount) => _value += Mathf.Max(0, amount);

        private void OnTriggerEnter(Collider other)
        {
            if (!IsSpawned || !IsServer || _collected || GameYunit.Gameplay.GameSessionController.IsPaused)
            {
                return;
            }

            PlayerLevel player = other.GetComponentInParent<PlayerLevel>();
            if (player == null || !player.IsSpawned)
            {
                return;
            }

            _collected = true;
            player.AddXpServer(_value);
            OnServerCollected?.Invoke(this, player);

            // O handler do NetworkObjectPool devolve a gema ao pool em cada peer.
            NetworkObject.Despawn(destroy: true);
        }

        private void Update()
        {
            if (IsSpawned && _spinSpeed != 0f)
            {
                transform.Rotate(0f, _spinSpeed * Time.deltaTime, 0f, Space.World);
            }
        }

        public void OnSpawnedFromPool() => ResetState();

        public void OnReturnedToPool() => ResetState();

        private void ResetState()
        {
            _value = 0;
            _collected = false;
        }
    }
}
