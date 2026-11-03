using System;
using System.Collections.Generic;
using GameYunit.Network;
using Unity.Netcode;
using UnityEngine;

namespace GameYunit.Progression
{
    /// <summary>
    /// Atende os pedidos de <see cref="XpDropper"/> no servidor: retira gemas do NetworkObjectPool e as publica.
    /// Fica no GameObject do NetworkManager, junto do ObjectPoolManager e do NetworkObjectPool.
    /// </summary>
    /// <remarks>
    /// <para>Pedidos são enfileirados e atendidos no LateUpdate: o pedido nasce dentro do despawn do inimigo,
    /// e publicar outro NetworkObject no meio desse callback é frágil.</para>
    /// <para>Acima de <c>Max Active Gems</c>, o XP é somado à gema ativa mais antiga em vez de criar outra.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObjectPool))]
    public sealed class XpDropService : MonoBehaviour
    {
        [Tooltip("Teto de gemas publicadas ao mesmo tempo. Drops extras se fundem na gema mais antiga.")]
        [SerializeField, Min(1)] private int _maxActiveGems = 300;

        private readonly Queue<XpDropRequest> _pending = new Queue<XpDropRequest>();
        private readonly List<XpGem> _activeGems = new List<XpGem>();
        private NetworkObjectPool _networkPool;
        private NetworkManager _networkManager;
        private bool _loggedMissingRegistration;

        /// <summary>[Servidor] Gemas publicadas no momento.</summary>
        public int ActiveGemCount => _activeGems.Count;

        /// <summary>[Servidor] Nova gema publicada.</summary>
        public event Action<XpGem> OnGemSpawned;

        private void Awake()
        {
            _networkPool = GetComponent<NetworkObjectPool>();
            _networkManager = GetComponent<NetworkManager>();
        }

        private void OnEnable()
        {
            XpDropper.OnServerDropRequested += Enqueue;
            if (_networkManager != null)
            {
                _networkManager.OnServerStopped += HandleServerStopped;
            }
        }

        private void OnDisable()
        {
            XpDropper.OnServerDropRequested -= Enqueue;
            if (_networkManager != null)
            {
                _networkManager.OnServerStopped -= HandleServerStopped;
            }

            ForgetActiveGems();
        }

        private void Enqueue(XpDropRequest request)
        {
            if (_networkManager != null && _networkManager.IsServer)
            {
                _pending.Enqueue(request);
            }
        }

        private void LateUpdate()
        {
            if (_pending.Count == 0)
            {
                return;
            }

            if (_networkManager == null || !_networkManager.IsServer || !_networkManager.IsListening ||
                _networkManager.ShutdownInProgress)
            {
                _pending.Clear();
                return;
            }

            while (_pending.Count > 0)
            {
                SpawnOrMerge(_pending.Dequeue());
            }
        }

        private void SpawnOrMerge(in XpDropRequest request)
        {
            NetworkObject prefab = request.Prefab != null ? request.Prefab.GetComponent<NetworkObject>() : null;
            if (!_networkPool.IsRegistered(prefab))
            {
                if (!_loggedMissingRegistration)
                {
                    _loggedMissingRegistration = true;
                    Debug.LogError("[XpDropService] Registre o prefab da gema no NetworkObjectPool e em Network Prefabs.", this);
                }

                return;
            }

            if (_activeGems.Count >= _maxActiveGems)
            {
                _activeGems[0].AddValueServer(request.Value);
                return;
            }

            NetworkObject spawned = _networkPool.Spawn(prefab, request.Position, Quaternion.identity);
            if (spawned == null)
            {
                return;
            }

            XpGem gem = spawned.GetComponent<XpGem>();
            gem.InitializeServer(request.Value);
            gem.OnServerDespawned += HandleGemDespawned;
            _activeGems.Add(gem);
            OnGemSpawned?.Invoke(gem);
        }

        private void HandleGemDespawned(XpGem gem)
        {
            gem.OnServerDespawned -= HandleGemDespawned;
            _activeGems.Remove(gem);
        }

        private void HandleServerStopped(bool wasHost)
        {
            _pending.Clear();
            ForgetActiveGems();
        }

        private void ForgetActiveGems()
        {
            foreach (XpGem gem in _activeGems)
            {
                if (gem != null)
                {
                    gem.OnServerDespawned -= HandleGemDespawned;
                }
            }

            _activeGems.Clear();
        }
    }
}
