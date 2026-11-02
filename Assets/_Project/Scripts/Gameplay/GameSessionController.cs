using System;
using System.Collections.Generic;
using GameYunit.Combat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameYunit.Gameplay
{
    /// <summary>Estado replicado de pausa e marcos de abates. A pausa não interrompe o transporte NGO.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class GameSessionController : NetworkBehaviour
    {
        private readonly NetworkVariable<int> _pauseMode = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _killCount = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly HashSet<ulong> _pendingUpgrade = new();
        private bool _escapeHeld;
        private int _nextMilestone = 30;

        public static GameSessionController Instance { get; private set; }
        public static event Action<int, int> OnPauseChanged;
        public int PauseMode => _pauseMode.Value;
        public int KillCount => _killCount.Value;
        public static bool IsPaused => Instance != null && Instance.PauseMode != 0;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            Instance = this;
            _pauseMode.OnValueChanged += HandleStateChanged;
            _killCount.OnValueChanged += HandleKillCountChanged;
            if (IsServer) { _pauseMode.Value = 0; _killCount.Value = 0; _nextMilestone = 30; }
            OnPauseChanged?.Invoke(PauseMode, KillCount);
        }

        public override void OnNetworkDespawn()
        {
            _pauseMode.OnValueChanged -= HandleStateChanged;
            _killCount.OnValueChanged -= HandleKillCountChanged;
            if (Instance == this) Instance = null;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsSpawned || !IsClient || Keyboard.current == null) return;
            bool pressed = Keyboard.current.escapeKey.isPressed;
            if (pressed && !_escapeHeld) TogglePause();
            _escapeHeld = pressed;
            if (IsServer && PauseMode == 2)
            {
                _pendingUpgrade.RemoveWhere(id => NetworkManager == null || !NetworkManager.ConnectedClients.ContainsKey(id));
                if (_pendingUpgrade.Count == 0) _pauseMode.Value = 0;
            }
        }

        public void TogglePause()
        {
            if (IsSpawned && IsClient && PauseMode != 2) TogglePauseRpc();
        }

        public void RegisterKillServer()
        {
            if (!IsServer || !IsSpawned) return;
            _killCount.Value++;
            if (_killCount.Value < _nextMilestone) return;
            _nextMilestone += 30;
            _pendingUpgrade.Clear();
            foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
                if (client.PlayerObject != null) _pendingUpgrade.Add(client.ClientId);
            _pauseMode.Value = _pendingUpgrade.Count > 0 ? 2 : 0;
        }

        [Rpc(SendTo.Server)]
        private void TogglePauseRpc(RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (!NetworkManager.ConnectedClients.ContainsKey(sender) || PauseMode == 2) return;
            _pauseMode.Value = PauseMode == 1 ? 0 : 1;
        }

        public void ChooseUpgrade(int upgrade)
        {
            if (IsSpawned && IsClient && upgrade >= 0 && upgrade <= 2) ChooseUpgradeRpc(upgrade);
        }

        [Rpc(SendTo.Server)]
        private void ChooseUpgradeRpc(int upgrade, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (PauseMode != 2 || upgrade < 0 || upgrade > 2 || !_pendingUpgrade.Remove(sender) ||
                !NetworkManager.ConnectedClients.TryGetValue(sender, out NetworkClient client) || client.PlayerObject == null) return;
            client.PlayerObject.GetComponent<HealthComponent>()?.ApplyUpgradeServer(upgrade);
            if (_pendingUpgrade.Count == 0) _pauseMode.Value = 0;
        }

        private void HandleStateChanged(int previous, int current) => OnPauseChanged?.Invoke(current, KillCount);
        private void HandleKillCountChanged(int previous, int current) => OnPauseChanged?.Invoke(PauseMode, current);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; OnPauseChanged = null; }
    }
}
