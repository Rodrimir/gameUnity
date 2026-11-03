using System.Collections.Generic;
using Gamekit3D;
using GameYunit.Characters;
using GameYunit.Network;
using GameYunit.Settings;
using TMPro;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

namespace GameYunit.KitBridge
{
    [RequireComponent(typeof(NetworkPlayerIdentity), typeof(PlayerController), typeof(PlayerInput))]
    public sealed class CoopPlayer : NetworkBehaviour
    {
        [Tooltip("Catálogo de personagens; a aparência é aplicada sobre o mesmo corpo e o mesmo animator.")]
        [SerializeField] private CharacterCatalog _characters;
        private NetworkPlayerIdentity _identity;
        private PlayerInput _input;
        private CameraSettings _camera;
        private TextMeshPro _nameLabel;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private readonly List<CinemachineInputAxisController.Controller> _axes = new();
        private readonly List<float> _baseGains = new(), _baseLegacyGains = new();
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
                GameSettings.Applied += ApplyCameraSettings;
                ApplyCameraSettings();
            }
        }

        private void RefreshIdentity()
        {
            if (_nameLabel != null)
            {
                _nameLabel.text = _identity.PlayerName.Value.ToString();
                _nameLabel.color = _identity.PlayerColor;
            }
            ApplyCharacter();
        }

        /// <summary>
        /// Aparência do personagem escolhido. Mexe só em material, cor e porte: o corpo e o animator
        /// continuam os mesmos, porque as animações da Ellen estão presas ao esqueleto dela.
        /// </summary>
        private void ApplyCharacter()
        {
            var character = _characters != null ? _characters[_identity.CharacterIndex.Value] : null;
            Color tint = character != null && !character.UsesPlayerColor
                ? character.Tint
                : _identity.PlayerColor;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", tint);
            block.SetColor("_Color", tint);
            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (character != null) ApplyMaterial(renderer, character);
                renderer.SetPropertyBlock(block);
            }
            if (character != null && !Mathf.Approximately(character.Scale, 1f))
                transform.localScale = Vector3.one * character.Scale;
        }

        /// <summary>Troca o material pelo do personagem, casando pelo nome da parte do corpo.</summary>
        private static void ApplyMaterial(SkinnedMeshRenderer renderer, CharacterDefinition character)
        {
            string name = renderer.name.ToLowerInvariant();
            Material replacement =
                name.Contains("head") || name.Contains("face") ? character.HeadMaterial :
                name.Contains("hair") ? character.HairMaterial :
                name.Contains("body") ? character.BodyMaterial : null;
            if (replacement != null) renderer.sharedMaterial = replacement;
        }

        /// <summary>
        /// Campo de visão e sensibilidade do mouse na câmera do kit.
        /// </summary>
        /// <remarks>
        /// Só na câmera do dono: a réplica não tem câmera local. A sensibilidade multiplica o ganho de cada
        /// eixo do Cinemachine, e o sinal do eixo vertical cobre a opção de inverter.
        /// </remarks>
        private void ApplyCameraSettings()
        {
            if (_camera == null) return;
            foreach (var camera in _camera.GetComponentsInChildren<CinemachineCamera>(true))
            {
                var lens = camera.Lens;
                lens.FieldOfView = GameSettings.FieldOfView;
                camera.Lens = lens;
            }
            CacheAxisGains();
            for (int i = 0; i < _axes.Count; i++)
            {
                var controller = _axes[i].Input;
                // O eixo vertical é o único que a opção de inverter afeta.
                float direction = _axes[i].Name != null && _axes[i].Name.Contains("Vertical")
                    && GameSettings.InvertMouseY ? -1f : 1f;
                controller.Gain = _baseGains[i] * GameSettings.MouseSensitivity * direction;
                controller.LegacyGain = _baseLegacyGains[i] * GameSettings.MouseSensitivity * direction;
            }
        }

        /// <summary>
        /// Guarda o ganho original de cada eixo na primeira passagem.
        /// </summary>
        /// <remarks>
        /// Sem isso, cada aplicação multiplicaria o ganho já multiplicado e a sensibilidade cresceria a
        /// cada vez que as configurações fossem salvas.
        /// </remarks>
        private void CacheAxisGains()
        {
            if (_axes.Count > 0) return;
            foreach (var axis in _camera.GetComponentsInChildren<CinemachineInputAxisController>(true))
                foreach (var controller in axis.Controllers)
                {
                    _axes.Add(controller);
                    _baseGains.Add(controller.Input.Gain);
                    _baseLegacyGains.Add(controller.Input.LegacyGain);
                }
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
            if (IsOwner) GameSettings.Applied -= ApplyCameraSettings;
        }
    }
}
