using UnityEngine;

namespace GameYunit.Characters
{
    /// <summary>
    /// Um personagem jogável: aparência aplicada sobre o mesmo corpo e o mesmo animator.
    /// </summary>
    /// <remarks>
    /// Todos os personagens do 3D Game Kit usam rig <c>Generic</c>, cada um com seu próprio esqueleto, e as
    /// animações da Ellen estão presas aos ossos dela por nome. Não há retargeting entre eles, então trocar
    /// o modelo quebraria a animação. Por isso um personagem aqui é uma variação de aparência — materiais,
    /// cor e porte — sobre o corpo da Ellen, o que mantém intactas as animações pedidas.
    ///
    /// <see cref="BodyPrefab"/> existe para quando houver um modelo com esqueleto compatível: nesse caso o
    /// corpo é substituído e o animator continua o mesmo. Um modelo de rig diferente não serve.
    /// </remarks>
    [CreateAssetMenu(menuName = "GameYunit/Personagem", fileName = "Personagem")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [Header("Identidade")]
        [Tooltip("Identificador estável, usado na replicação. Não renomeie depois de publicar uma build.")]
        [SerializeField] private string _id = "ellen";
        [SerializeField] private string _displayName = "Ellen";
        [SerializeField, TextArea(2, 3)] private string _description = "";
        [SerializeField] private Sprite _portrait;

        [Header("Aparência")]
        [Tooltip("Cor aplicada ao corpo. Some com a cor do jogador quando esta está ligada.")]
        [SerializeField] private Color _tint = Color.white;
        [Tooltip("Deixe vazio para manter o material original do corpo.")]
        [SerializeField] private Material _bodyMaterial;
        [Tooltip("Deixe vazio para manter o material original da cabeça.")]
        [SerializeField] private Material _headMaterial;
        [Tooltip("Deixe vazio para manter o material original do cabelo.")]
        [SerializeField] private Material _hairMaterial;
        [Tooltip("Porte relativo. Valores longe de 1 desencaixam a câmera e as colisões.")]
        [SerializeField, Range(0.85f, 1.15f)] private float _scale = 1f;

        [Header("Modelo alternativo (opcional)")]
        [Tooltip("Só use com um modelo de esqueleto compatível com o da Ellen; outro rig quebra a animação.")]
        [SerializeField] private GameObject _bodyPrefab;

        public string Id => _id;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Portrait => _portrait;
        public Color Tint => _tint;
        public Material BodyMaterial => _bodyMaterial;
        public Material HeadMaterial => _headMaterial;
        public Material HairMaterial => _hairMaterial;
        public float Scale => _scale;
        public GameObject BodyPrefab => _bodyPrefab;

        /// <summary>Diz se a cor do jogador ainda deve pintar o corpo por cima da aparência.</summary>
        public bool UsesPlayerColor => _bodyMaterial == null && _bodyPrefab == null;
    }
}
