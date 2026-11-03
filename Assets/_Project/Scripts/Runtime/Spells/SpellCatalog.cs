using UnityEngine;

namespace GameYunit.Spells
{
    /// <summary>
    /// Lista ordenada das magias disponíveis. A posição na lista é o que viaja pela rede, então a ordem
    /// precisa ser idêntica nas duas máquinas — por isso o catálogo é um asset único e versionado,
    /// e não uma varredura da pasta, que dependeria da ordem de importação.
    /// </summary>
    [CreateAssetMenu(menuName = "GameYunit/Catálogo de magias", fileName = "CatalogoDeMagias")]
    public sealed class SpellCatalog : ScriptableObject
    {
        [SerializeField] private SpellDefinition[] _spells = new SpellDefinition[0];

        public int Count => _spells?.Length ?? 0;

        public SpellDefinition this[int index] =>
            _spells != null && index >= 0 && index < _spells.Length ? _spells[index] : null;

        /// <summary>Índice válido mais próximo; devolve 0 quando o pedido está fora da lista.</summary>
        public int Clamp(int index) => Count == 0 ? 0 : Mathf.Clamp(index, 0, Count - 1);

        public int IndexOf(string id)
        {
            if (_spells == null) return -1;
            for (int i = 0; i < _spells.Length; i++)
                if (_spells[i] != null && _spells[i].Id == id) return i;
            return -1;
        }
    }
}
