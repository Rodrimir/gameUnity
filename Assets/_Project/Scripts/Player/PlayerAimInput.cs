using GameYunit.Core.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameYunit.Player
{
    /// <summary>
    /// Converte mouse/analógico direito em intenção de mira no plano XZ.
    /// PlayerNetwork chama esta classe somente no Owner; ela nunca altera a rotação.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAimInput : MonoBehaviour
    {
        [Tooltip("Action Vector2 opcional. Se vazia, usa <Gamepad>/rightStick.")]
        [SerializeField] private InputActionReference _aimAction;

        [Tooltip("Opcional; se vazia, procura a câmera ativa com tag MainCamera.")]
        [SerializeField] private Camera _aimCamera;

        [Tooltip("Zona morta aplicada ao valor processado da action de mira.")]
        [SerializeField, Range(0.05f, 0.9f)] private float _stickDeadzone = 0.2f;

        [Tooltip("Altura do plano de mira em relação ao pivot nos pés do jogador.")]
        [SerializeField] private float _aimHeight = 1f;

        private InputAction _localAimAction;
        private Vector2 _lastMousePosition;
        private bool _hasMousePosition;
        private bool _usingGamepad;

        /// <summary>Adquire a action local; chamado somente pelo Owner.</summary>
        public void EnableInput()
        {
            if (_localAimAction != null)
            {
                return;
            }

            _localAimAction = _aimAction != null && _aimAction.action != null
                ? _aimAction.action.Clone()
                : new InputAction("Aim", InputActionType.Value, "<Gamepad>/rightStick",
                    expectedControlType: "Vector2");
            _localAimAction.Enable();
        }

        public void DisableInput()
        {
            if (_localAimAction != null)
            {
                _localAimAction.Disable();
                _localAimAction.Dispose();
                _localAimAction = null;
            }

            _hasMousePosition = false;
            _usingGamepad = false;
        }

        private void OnDestroy() => DisableInput();

        /// <summary>Retorna uma direção unitária no mundo, ou zero para manter a orientação.</summary>
        public Vector2 ReadAim()
        {
            if (_localAimAction == null || !isActiveAndEnabled)
            {
                return Vector2.zero;
            }

            if (_aimCamera == null || !_aimCamera.isActiveAndEnabled)
            {
                _aimCamera = Camera.main;
            }

            Vector2 stick = _localAimAction.ReadValue<Vector2>();
            Mouse mouse = Mouse.current;
            Vector2 mousePosition = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            // Com a tela PSX a câmera renderiza em RenderTexture; converte a tela para os pixels da câmera.
            Vector2 cameraPixel = default;
            bool mouseInView = mouse != null &&
                CameraViewportMapping.TryScreenToCameraPixel(_aimCamera, mousePosition, out cameraPixel);
            bool mouseMoved = mouseInView && _hasMousePosition &&
                (mousePosition - _lastMousePosition).sqrMagnitude > 0.25f;

            _lastMousePosition = mousePosition;
            _hasMousePosition = mouse != null;

            if (stick.sqrMagnitude > _stickDeadzone * _stickDeadzone)
            {
                _usingGamepad = true;
                return ReadStickAim(stick);
            }

            if (mouseMoved)
            {
                _usingGamepad = false;
            }

            // Ao soltar o analógico, um mouse parado não pode roubar a mira.
            return _usingGamepad || !mouseInView ? Vector2.zero : ReadMouseAim(cameraPixel);
        }

        private Vector2 ReadStickAim(Vector2 stick)
        {
            Vector3 forward = _aimCamera != null ? _aimCamera.transform.forward : Vector3.forward;
            Vector3 right = _aimCamera != null ? _aimCamera.transform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            Vector3 direction = forward.normalized * stick.y + right.normalized * stick.x;
            return new Vector2(direction.x, direction.z).normalized;
        }

        private Vector2 ReadMouseAim(Vector2 mousePosition)
        {
            // Plano na altura do personagem: não depende de Colliders ou raycasts de física.
            Plane aimPlane = new Plane(Vector3.up, transform.position + Vector3.up * _aimHeight);
            Ray ray = _aimCamera.ScreenPointToRay(mousePosition);
            if (!aimPlane.Raycast(ray, out float distance))
            {
                return Vector2.zero;
            }

            Vector3 direction = ray.GetPoint(distance) - transform.position;
            Vector2 planarDirection = new Vector2(direction.x, direction.z);
            return planarDirection.sqrMagnitude > 0.0025f ? planarDirection.normalized : Vector2.zero;
        }
    }
}
