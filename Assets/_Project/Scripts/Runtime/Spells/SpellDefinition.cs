using UnityEngine;

namespace GameYunit.Spells
{
    /// <summary>Forma da área atingida por uma magia, usada para montar a hitbox no servidor.</summary>
    public enum SpellShape
    {
        /// <summary>Esfera centrada no ponto de impacto à frente do conjurador.</summary>
        Sphere,
        /// <summary>Cone partindo do conjurador, limitado por <see cref="SpellDefinition.Range"/>.</summary>
        Cone
    }

    /// <summary>
    /// Dados de uma magia conjurável. É só configuração: nenhum comportamento mora aqui, para que o
    /// balanceamento possa ser ajustado sem recompilar e para que servidor e clientes leiam os mesmos números.
    /// </summary>
    /// <remarks>
    /// Dano, custo e recarga já ficam declarados embora a Fase 3 ainda não aplique dano. A hitbox é
    /// montada a partir de <see cref="Range"/>, <see cref="Radius"/> e <see cref="Shape"/>, de modo que
    /// ligar o dano depois não exija mudar a área nem o balanceamento.
    /// </remarks>
    [CreateAssetMenu(menuName = "GameYunit/Magia", fileName = "Magia")]
    public sealed class SpellDefinition : ScriptableObject
    {
        [Header("Identidade")]
        [Tooltip("Identificador estável, usado na replicação. Não renomeie depois de publicar uma build.")]
        [SerializeField] private string _id = "fogo";
        [SerializeField] private string _displayName = "Magia";
        [SerializeField, TextArea(2, 3)] private string _description = "";
        [SerializeField] private Sprite _icon;
        [SerializeField] private Color _tint = Color.white;

        [Header("Área atingida (hitbox)")]
        [SerializeField] private SpellShape _shape = SpellShape.Sphere;
        [Tooltip("Distância à frente do conjurador onde a área nasce. Zero centra a área nele mesmo.")]
        [SerializeField, Min(0f)] private float _range = 6f;
        [Tooltip("Raio da esfera atingida, ou metade da largura do cone na ponta.")]
        [SerializeField, Min(0.1f)] private float _radius = 2.5f;
        [Tooltip("Abertura total do cone em graus. Ignorado quando a forma é esfera.")]
        [SerializeField, Range(10f, 180f)] private float _coneAngle = 60f;
        [Tooltip("Altura da área, para não atingir quem está em outro andar do cenário.")]
        [SerializeField, Min(0.5f)] private float _height = 3f;

        [Header("Combate (dano ainda não aplicado — Fase 3)")]
        [SerializeField, Min(0)] private int _damage = 10;
        [Tooltip("Quantos alvos a magia atinge por conjuração. Zero ou menos significa sem limite.")]
        [SerializeField] private int _maxTargets = 0;

        [Header("Ritmo")]
        [Tooltip("Segundos entre conjurações. É o principal botão de balanceamento.")]
        [SerializeField, Min(0.1f)] private float _cooldown = 2f;
        [Tooltip("Atraso entre o início do gesto e o efeito, casado com a animação do cajado.")]
        [SerializeField, Min(0f)] private float _castDelay = 0.35f;
        [Tooltip("Por quanto tempo o VFX fica na cena antes de ser removido.")]
        [SerializeField, Min(0.1f)] private float _effectLifetime = 3f;

        [Header("Apresentação")]
        [SerializeField] private GameObject _effectPrefab;
        [Tooltip("Deslocamento vertical do VFX no ponto de impacto.")]
        [SerializeField] private float _effectHeightOffset;

        public string Id => _id;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Icon => _icon;
        public Color Tint => _tint;
        public SpellShape Shape => _shape;
        public float Range => _range;
        public float Radius => _radius;
        public float ConeAngle => _coneAngle;
        public float Height => _height;
        public int Damage => _damage;
        public int MaxTargets => _maxTargets;
        public float Cooldown => _cooldown;
        public float CastDelay => _castDelay;
        public float EffectLifetime => _effectLifetime;
        public GameObject EffectPrefab => _effectPrefab;
        public float EffectHeightOffset => _effectHeightOffset;

        /// <summary>Centro da área atingida, dadas a posição e a direção do conjurador.</summary>
        public Vector3 ImpactPoint(Vector3 origin, Vector3 forward)
        {
            Vector3 flat = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
            return origin + flat.normalized * _range;
        }

        /// <summary>
        /// Raio que cobre toda a área, usado como primeiro filtro barato antes do teste exato de forma.
        /// </summary>
        public float BroadRadius => _shape == SpellShape.Sphere ? _radius : _range + _radius;

        /// <summary>Diz se um ponto cai dentro da área, já considerando a forma e a altura.</summary>
        public bool Contains(Vector3 origin, Vector3 forward, Vector3 point)
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
            flatForward.Normalize();

            if (_shape == SpellShape.Sphere)
            {
                Vector3 center = origin + flatForward * _range;
                if (Mathf.Abs(point.y - center.y) > _height * 0.5f) return false;
                return Vector3.ProjectOnPlane(point - center, Vector3.up).sqrMagnitude <= _radius * _radius;
            }

            Vector3 toTarget = point - origin;
            if (Mathf.Abs(toTarget.y) > _height * 0.5f) return false;
            Vector3 flatToTarget = Vector3.ProjectOnPlane(toTarget, Vector3.up);
            float distance = flatToTarget.magnitude;
            if (distance > _range + _radius || distance < 0.0001f) return false;
            return Vector3.Angle(flatForward, flatToTarget.normalized) <= _coneAngle * 0.5f;
        }
    }
}
