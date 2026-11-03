using UnityEngine;

namespace GameYunit.Characters
{
    /// <summary>
    /// Lista ordenada dos personagens jogáveis. Como no catálogo de magias, a posição é o que viaja pela
    /// rede, então a ordem precisa ser idêntica nas duas máquinas — por isso é um asset versionado.
    /// </summary>
    [CreateAssetMenu(menuName = "GameYunit/Catálogo de personagens", fileName = "CatalogoDePersonagens")]
    public sealed class CharacterCatalog : ScriptableObject
    {
        [SerializeField] private CharacterDefinition[] _characters = new CharacterDefinition[0];

        public int Count => _characters?.Length ?? 0;

        public CharacterDefinition this[int index] =>
            _characters != null && index >= 0 && index < _characters.Length ? _characters[index] : null;

        public int IndexOf(string id)
        {
            if (_characters == null) return -1;
            for (int i = 0; i < _characters.Length; i++)
                if (_characters[i] != null && _characters[i].Id == id) return i;
            return -1;
        }
    }
}
