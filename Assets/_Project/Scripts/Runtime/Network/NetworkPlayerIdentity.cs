using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace GameYunit.Network
{
    public sealed class NetworkPlayerIdentity : NetworkBehaviour
    {
        public readonly NetworkVariable<FixedString64Bytes> PlayerName = new();
        public readonly NetworkVariable<int> ColorIndex = new();
        public readonly NetworkVariable<int> Slot = new();
        /// <summary>Posição da magia escolhida no catálogo compartilhado.</summary>
        public readonly NetworkVariable<int> SpellIndex = new();
        /// <summary>Posição do personagem escolhido no catálogo compartilhado.</summary>
        public readonly NetworkVariable<int> CharacterIndex = new();
        private string _initialName;
        private int _initialColor, _initialSlot, _initialSpell, _initialCharacter;
        public event Action Changed;
        public Color PlayerColor => ColorIndex.Value == 0
            ? new Color(0.25f, 0.73f, 1f) : new Color(1f, 0.55f, 0.22f);

        public void InitializeServer(string playerName, int color, int slot, int spell = 0, int character = 0)
        {
            _initialName = ConnectionRules.CleanName(playerName);
            _initialColor = color;
            _initialSlot = slot;
            _initialSpell = spell;
            _initialCharacter = character;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                PlayerName.Value = new FixedString64Bytes(_initialName ?? "Explorador");
                ColorIndex.Value = _initialColor;
                Slot.Value = _initialSlot;
                SpellIndex.Value = _initialSpell;
                CharacterIndex.Value = _initialCharacter;
            }
            PlayerName.OnValueChanged += NameChanged;
            ColorIndex.OnValueChanged += ColorChanged;
            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            PlayerName.OnValueChanged -= NameChanged;
            ColorIndex.OnValueChanged -= ColorChanged;
        }

        private void NameChanged(FixedString64Bytes previous, FixedString64Bytes current) => Changed?.Invoke();
        private void ColorChanged(int previous, int current) => Changed?.Invoke();
    }
}
