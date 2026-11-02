using GameYunit.Core.Rendering;
using UnityEngine;
using UnityEngine.UI;

namespace GameYunit.Rendering
{
    /// <summary>
    /// Simula a resolução do PSX: a câmera renderiza numa RenderTexture pequena (320x240, filtro Point)
    /// que é ampliada num Canvas Screen Space Overlay, com letterbox para manter a proporção.
    /// Também publica os parâmetros globais do shader "GameYunit/PSX Lit" (vertex snapping e affine).
    /// </summary>
    /// <remarks>
    /// <para>Adicione na Main Camera (a mesma do CameraController). Tudo é criado em runtime e destruído
    /// no OnDisable; nada é salvo na cena.</para>
    /// <para>O retângulo exibido é registrado em <see cref="CameraViewportMapping"/>, para que a mira do
    /// mouse continue correta com a câmera renderizando em baixa resolução.</para>
    /// <para>Canvases de HUD devem usar Sort Order maior que <c>Canvas Sort Order</c> para ficarem por cima
    /// em resolução nativa (ou desenhe o HUD dentro da RenderTexture com Screen Space - Camera, se quiser o HUD pixelado).</para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class PsxRenderTextureSetup : MonoBehaviour
    {
        private static readonly int SnapResolutionId = Shader.PropertyToID("_PsxSnapResolution");
        private static readonly int AffineStrengthId = Shader.PropertyToID("_PsxAffineStrength");

        [Header("Resolução interna")]
        [SerializeField] private Vector2Int _resolution = new Vector2Int(320, 240);

        [Tooltip("Mantém a altura e calcula a largura pela proporção da janela (sem faixas laterais).")]
        [SerializeField] private bool _matchScreenAspect;

        [Header("Apresentação")]
        [Tooltip("Ordem do Canvas da tela PSX. HUDs com valor maior ficam por cima.")]
        [SerializeField] private int _canvasSortOrder = -100;

        [SerializeField] private Color _letterboxColor = Color.black;

        [Header("Shader PSX (globais)")]
        [Tooltip("Arredonda os vértices para a grade da resolução interna (tremido característico).")]
        [SerializeField] private bool _vertexSnapping = true;

        [Tooltip("0 = UV com perspectiva correta; 1 = mapeamento afim (texturas 'nadando' como no PSX).")]
        [SerializeField, Range(0f, 1f)] private float _affineStrength = 1f;

        private readonly Vector3[] _corners = new Vector3[4];
        private Camera _camera;
        private Camera _clearCamera;
        private RenderTexture _renderTexture;
        private Canvas _canvas;
        private RawImage _screen;
        private AspectRatioFitter _fitter;
        private Vector2Int _lastScreenSize;

        /// <summary>RenderTexture atual (muda se a resolução for recalculada).</summary>
        public RenderTexture RenderTexture => _renderTexture;

        public Vector2Int InternalResolution => _renderTexture != null
            ? new Vector2Int(_renderTexture.width, _renderTexture.height)
            : _resolution;

        public bool VertexSnapping => _vertexSnapping;
        public float AffineStrength => _affineStrength;

        public void ConfigureRuntime(Vector2Int resolution, bool vertexSnapping, float affineStrength)
        {
            _resolution = Vector2Int.Max(resolution, new Vector2Int(16, 16));
            _matchScreenAspect = false;
            _vertexSnapping = vertexSnapping;
            _affineStrength = Mathf.Clamp01(affineStrength);
            if (_camera != null && isActiveAndEnabled)
            {
                RebuildRenderTexture();
            }
        }

        private void OnEnable()
        {
            _camera = GetComponent<Camera>();
            CreatePresentation();
            RebuildRenderTexture();
        }

        private void OnDisable()
        {
            CameraViewportMapping.Clear(_camera);
            if (_camera != null)
            {
                _camera.targetTexture = null;
            }

            DestroyObject(_canvas != null ? _canvas.gameObject : null);
            DestroyObject(_clearCamera != null ? _clearCamera.gameObject : null);
            ReleaseRenderTexture();

            // Sem a tela PSX, o shader volta a se comportar como um lit simples.
            Shader.SetGlobalVector(SnapResolutionId, Vector4.zero);
            Shader.SetGlobalFloat(AffineStrengthId, 0f);
        }

        private void OnValidate()
        {
            _resolution = Vector2Int.Max(_resolution, new Vector2Int(16, 16));
            if (isActiveAndEnabled && Application.isPlaying && _camera != null)
            {
                RebuildRenderTexture();
            }
        }

        private void LateUpdate()
        {
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (_matchScreenAspect && screenSize != _lastScreenSize)
            {
                RebuildRenderTexture();
            }

            _lastScreenSize = screenSize;

            // O RectTransform de um Canvas Overlay está em pixels de tela.
            _screen.rectTransform.GetWorldCorners(_corners);
            CameraViewportMapping.SetScreenRect(_camera,
                Rect.MinMaxRect(_corners[0].x, _corners[0].y, _corners[2].x, _corners[2].y));
        }

        private void RebuildRenderTexture()
        {
            Vector2Int size = _resolution;
            if (_matchScreenAspect && Screen.height > 0)
            {
                size.x = Mathf.Max(16, Mathf.RoundToInt(size.y * (float)Screen.width / Screen.height));
            }

            if (_renderTexture == null || _renderTexture.width != size.x || _renderTexture.height != size.y)
            {
                _camera.targetTexture = null;
                ReleaseRenderTexture();
                _renderTexture = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.Default)
                {
                    name = "PSX Screen",
                    filterMode = FilterMode.Point,
                    antiAliasing = 1,
                    useMipMap = false,
                    hideFlags = HideFlags.DontSave
                };
                _renderTexture.Create();
            }

            _camera.targetTexture = _renderTexture;
            _screen.texture = _renderTexture;
            _fitter.aspectRatio = (float)size.x / size.y;

            Shader.SetGlobalVector(SnapResolutionId, _vertexSnapping ? new Vector4(size.x, size.y, 0f, 0f) : Vector4.zero);
            Shader.SetGlobalFloat(AffineStrengthId, _affineStrength);
        }

        private void CreatePresentation()
        {
            // Câmera vazia que só limpa a tela: sem ela as faixas do letterbox ficam com lixo da janela.
            var clearObject = new GameObject("[PSX Clear Camera]") { hideFlags = HideFlags.DontSave };
            _clearCamera = clearObject.AddComponent<Camera>();
            _clearCamera.clearFlags = CameraClearFlags.SolidColor;
            _clearCamera.backgroundColor = _letterboxColor;
            _clearCamera.cullingMask = 0;
            _clearCamera.depth = _camera.depth - 1f;
            _clearCamera.useOcclusionCulling = false;

            var canvasObject = new GameObject("[PSX Screen]", typeof(RectTransform)) { hideFlags = HideFlags.DontSave };
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = _canvasSortOrder;

            var screenObject = new GameObject("Screen", typeof(RectTransform));
            screenObject.transform.SetParent(canvasObject.transform, worldPositionStays: false);
            _screen = screenObject.AddComponent<RawImage>();
            _screen.raycastTarget = false;
            _fitter = screenObject.AddComponent<AspectRatioFitter>();
            _fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        }

        private void ReleaseRenderTexture()
        {
            if (_renderTexture == null)
            {
                return;
            }

            _renderTexture.Release();
            DestroyObject(_renderTexture);
            _renderTexture = null;
        }

        private static void DestroyObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
