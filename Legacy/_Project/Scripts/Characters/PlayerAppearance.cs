using System;
using Unity.Netcode;
using UnityEngine;

namespace GameYunit.Characters
{
    /// <summary>Seleção visual validada no servidor; todos os peers mostram o mesmo personagem.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerAppearance : NetworkBehaviour
    {
        [Serializable]
        public struct Character
        {
            public string Name;
            public GameObject Visual;
        }

        [SerializeField] private Character[] _characters = Array.Empty<Character>();
        private readonly NetworkVariable<int> _selected = new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public static int PreferredCharacterIndex { get; set; }
        public int SelectedIndex => _selected.Value;
        public int CharacterCount => _characters.Length;
        public event Action<int> OnCharacterChanged;

        public string GetCharacterName(int index) => IsValid(index) ? _characters[index].Name : string.Empty;

        public override void OnNetworkSpawn()
        {
            _selected.OnValueChanged += HandleChanged;
            Apply(_selected.Value);
            if (IsClient && IsOwner)
            {
                RequestCharacter(PreferredCharacterIndex);
            }
        }

        public override void OnNetworkDespawn() => _selected.OnValueChanged -= HandleChanged;

        public void RequestCharacter(int index)
        {
            if (!IsSpawned || !IsClient || !IsOwner || !IsValid(index))
            {
                return;
            }

            PreferredCharacterIndex = index;
            SelectCharacterRpc(index);
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void SelectCharacterRpc(int index)
        {
            if (IsValid(index))
            {
                _selected.Value = index;
            }
        }

        private bool IsValid(int index) => index >= 0 && index < _characters.Length;

        private void HandleChanged(int previous, int current) => Apply(current);

        private void Apply(int index)
        {
            for (int i = 0; i < _characters.Length; i++)
            {
                if (_characters[i].Visual != null)
                {
                    _characters[i].Visual.SetActive(i == index);
                }
            }

            OnCharacterChanged?.Invoke(index);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => PreferredCharacterIndex = 0;
    }
}
