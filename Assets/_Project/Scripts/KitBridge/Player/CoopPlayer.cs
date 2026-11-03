using Gamekit3D;
using GameYunit.Network;
using TMPro;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

namespace GameYunit.KitBridge
{
    [RequireComponent(typeof(NetworkPlayerIdentity), typeof(PlayerController), typeof(PlayerInput))]
    public sealed class CoopPlayer : NetworkBehaviour
    {
        private NetworkPlayerIdentity _identity;
        private PlayerInput _input;
        private CameraSettings _camera;
        private TextMeshPro _nameLabel;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        public PlayerInput Input => _input;
        public CameraSettings LocalCamera => _camera;

        public override void OnNetworkSpawn()
        {
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            _input = GetComponent<PlayerInput>();
            _identity = GetComponent<NetworkPlayerIdentity>();
            _input.enabled = IsOwner;
            GetComponent<CharacterController>().enabled = IsOwner;
            GetComponent<Animator>().applyRootMotion = IsOwner;
            _nameLabel = GetComponentInChildren<TextMeshPro>(true);
            _identity.Changed += RefreshIdentity;
            RefreshIdentity();
            if (IsOwner)
            {
                _camera = FindFirstObjectByType<CameraSettings>();
                if (_camera != null)
                {
                    _camera.follow = transform;
                    _camera.lookAt = transform.Find("HeadTarget");
                    _camera.allowRuntimeCameraSettingsChanges = true;
                    GetComponent<PlayerController>().cameraSettings = _camera;
                }
                CoopSession.Instance.MenuChanged += UpdateCameraInput;
                CoopSession.Instance.PlayerReady();
                UpdateCameraInput();
            }
        }

        private void RefreshIdentity()
        {
            if (_nameLabel != null)
            {
                _nameLabel.text = _identity.PlayerName.Value.ToString();
                _nameLabel.color = _identity.PlayerColor;
            }
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", _identity.PlayerColor);
            block.SetColor("_Color", _identity.PlayerColor);
            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.SetPropertyBlock(block);
        }

        private void UpdateCameraInput()
        {
            if (_camera == null) return;
            bool allow = !CoopSession.Instance.LocalMenuOpen;
            foreach (var axis in _camera.GetComponentsInChildren<CinemachineInputAxisController>(true)) axis.enabled = allow;
        }

        private void LateUpdate()
        {
            if (_nameLabel != null && Camera.main != null) _nameLabel.transform.rotation = Camera.main.transform.rotation;
            // Rede de vida e revive só na Fase 3; aqui uma queda devolve o dono ao nascimento.
            if (IsSpawned && IsOwner && transform.position.y < -20f)
            {
                var controller = GetComponent<CharacterController>();
                controller.enabled = false;
                transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
                controller.enabled = true;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_identity != null) _identity.Changed -= RefreshIdentity;
            if (IsOwner && CoopSession.Instance != null) CoopSession.Instance.MenuChanged -= UpdateCameraInput;
        }
    }
}
