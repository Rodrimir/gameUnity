using GameYunit.Combat;
using UnityEngine;

namespace GameYunit.KitBridge
{
    /// <summary>
    /// Barra de vida flutuante sobre o personagem, visível para os dois jogadores.
    /// </summary>
    /// <remarks>
    /// É feita de quads com <c>Sprites/Default</c> em vez de um Canvas em world space: um Canvas por
    /// personagem custa um rebuild de layout a cada quadro em que a vida muda, e aqui basta escalar
    /// um quad. Fica junto da etiqueta de nome, que já encara a câmera.
    /// </remarks>
    [RequireComponent(typeof(NetworkHealth))]
    public sealed class HealthBar : MonoBehaviour
    {
        [SerializeField] private float _height = 2.15f;
        [SerializeField] private float _width = 1.1f;

        private NetworkHealth _health;
        private Transform _root, _fill;

        private void Awake()
        {
            _health = GetComponent<NetworkHealth>();
            Build();
            _health.Changed += Refresh;
        }

        private void OnDestroy()
        {
            if (_health != null) _health.Changed -= Refresh;
        }

        private void Build()
        {
            _root = new GameObject("Barra de vida").transform;
            _root.SetParent(transform, false);
            _root.localPosition = new Vector3(0f, _height, 0f);
            var back = Quad("Fundo", _root, new Color(0.05f, 0.05f, 0.06f, 0.85f), 0f);
            back.localScale = new Vector3(_width, 0.12f, 1f);
            _fill = Quad("Preenchimento", _root, new Color(0.35f, 0.85f, 0.45f), -0.001f);
            _fill.localScale = new Vector3(_width, 0.1f, 1f);
            Refresh();
        }

        private static Transform Quad(string name, Transform parent, Color color, float z)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = new Vector3(0f, 0f, z);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = color };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return quad.transform;
        }

        private void Refresh()
        {
            if (_fill == null) return;
            float fraction = _health.Fraction;
            // Encolhe a partir da esquerda: escala no X e desloca metade do que foi perdido.
            _fill.localScale = new Vector3(_width * fraction, 0.1f, 1f);
            _fill.localPosition = new Vector3(-_width * (1f - fraction) * 0.5f, 0f, -0.001f);
            var renderer = _fill.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial.color = fraction > 0.5f ? new Color(0.35f, 0.85f, 0.45f)
                    : fraction > 0.25f ? new Color(0.95f, 0.75f, 0.25f)
                    : new Color(0.9f, 0.3f, 0.25f);
            _root.gameObject.SetActive(_health.IsAlive);
        }

        private void LateUpdate()
        {
            if (_root != null && Camera.main != null) _root.rotation = Camera.main.transform.rotation;
        }
    }
}
