using UnityEngine;

namespace GameYunit.Player
{
    /// <summary>
    /// Câmera local em terceira pessoa, acompanhando o jogador por trás em LateUpdate.
    /// Vive na câmera da cena, sem NetworkObject e fora do prefab do jogador.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraController : MonoBehaviour
    {
        [Tooltip("Distância atrás do personagem, altura e deslocamento lateral da câmera.")]
        [SerializeField] private Vector3 _followOffset = new Vector3(0.45f, 2.7f, -5.2f);

        [Tooltip("Ponto observado em relação à posição do jogador.")]
        [SerializeField] private Vector3 _lookOffset = new Vector3(0f, 1.35f, 0f);

        [SerializeField, Min(0f)] private float _smoothTime = 0.12f;
        [SerializeField, Min(0f)] private float _rotationSharpness = 12f;

        private PlayerNetwork _target;
        private Vector3 _followVelocity;

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

            Quaternion yaw = Quaternion.Euler(0f, _target.transform.eulerAngles.y, 0f);
            Vector3 desiredPosition = _target.transform.position + yaw * _followOffset;
            Vector3 position = _smoothTime <= 0f
                ? desiredPosition
                : Vector3.SmoothDamp(transform.position, desiredPosition, ref _followVelocity,
                    _smoothTime, Mathf.Infinity, Time.deltaTime);

            Vector3 lookPoint = _target.transform.position + Vector3.up * _lookOffset.y;
            Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - position, Vector3.up);
            float rotationBlend = 1f - Mathf.Exp(-_rotationSharpness * Time.deltaTime);
            transform.SetPositionAndRotation(position,
                Quaternion.Slerp(transform.rotation, desiredRotation, rotationBlend));
        }

        private void HandleLocalPlayerSpawned(PlayerNetwork player)
        {
            if (player == null || !player.IsSpawned || !player.IsClient || !player.IsOwner)
            {
                return;
            }

            _target = player;
            _followVelocity = Vector3.zero;
            Quaternion yaw = Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f);
            Vector3 position = player.transform.position + yaw * _followOffset;
            Vector3 lookPoint = player.transform.position + Vector3.up * _lookOffset.y;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookPoint - position, Vector3.up));
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
