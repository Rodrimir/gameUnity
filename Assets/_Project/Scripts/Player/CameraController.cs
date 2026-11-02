using UnityEngine;

namespace GameYunit.Player
{
    /// <summary>
    /// Câmera local com ângulo top-down fixo e acompanhamento suave em LateUpdate.
    /// Vive na câmera da cena, sem NetworkObject e fora do prefab do jogador.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraController : MonoBehaviour
    {
        [Tooltip("Offset em eixos do mundo; não gira com o personagem.")]
        [SerializeField] private Vector3 _followOffset = new Vector3(0f, 12f, -8f);

        [Tooltip("Ponto observado em relação à posição do jogador.")]
        [SerializeField] private Vector3 _lookOffset = new Vector3(0f, 1f, 0f);

        [SerializeField, Min(0f)] private float _smoothTime = 0.12f;

        private PlayerNetwork _target;
        private Vector3 _followVelocity;
        private Quaternion _viewRotation;

        private void OnEnable()
        {
            PlayerNetwork.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;
            PlayerNetwork.OnLocalPlayerDespawned += HandleLocalPlayerDespawned;

            // Também cobre câmera ativada depois do spawn, sem busca na cena por frame.
            HandleLocalPlayerSpawned(PlayerNetwork.LocalPlayer);
        }

        private void OnDisable()
        {
            PlayerNetwork.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
            PlayerNetwork.OnLocalPlayerDespawned -= HandleLocalPlayerDespawned;
            ClearTarget();
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            if (!_target.IsSpawned || !_target.IsClient || !_target.IsOwner)
            {
                ClearTarget();
                return;
            }

            Vector3 desiredPosition = _target.transform.position + _followOffset;
            Vector3 position = _smoothTime <= 0f
                ? desiredPosition
                : Vector3.SmoothDamp(transform.position, desiredPosition, ref _followVelocity,
                    _smoothTime, Mathf.Infinity, Time.deltaTime);

            // Rotação fixa evita oscilações de enquadramento durante o amortecimento da posição.
            transform.SetPositionAndRotation(position, _viewRotation);
        }

        private void HandleLocalPlayerSpawned(PlayerNetwork player)
        {
            if (player == null || !player.IsSpawned || !player.IsClient || !player.IsOwner)
            {
                return;
            }

            _target = player;
            _followVelocity = Vector3.zero;
            Vector3 lookDirection = _lookOffset - _followOffset;
            _viewRotation = lookDirection.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(lookDirection, Vector3.up)
                : transform.rotation;

            // Primeiro enquadramento imediato, sem deslizar desde a posição de edição da câmera.
            transform.SetPositionAndRotation(player.transform.position + _followOffset, _viewRotation);
        }

        private void HandleLocalPlayerDespawned(PlayerNetwork player)
        {
            if (_target == player)
            {
                ClearTarget();
            }
        }

        private void ClearTarget()
        {
            _target = null;
            _followVelocity = Vector3.zero;
        }
    }
}
