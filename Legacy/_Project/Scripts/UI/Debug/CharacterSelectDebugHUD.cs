using GameYunit.Characters;
using GameYunit.Player;
using UnityEngine;

namespace GameYunit.UI
{
    /// <summary>Escolha de personagem antes de conectar e durante o teste do protótipo.</summary>
    public sealed class CharacterSelectDebugHUD : MonoBehaviour
    {
        [SerializeField] private PlayerAppearance _playerPrefab;
        [SerializeField] private Rect _area = new Rect(10f, 380f, 240f, 160f);

        private void OnGUI()
        {
            if (!Debug.isDebugBuild || _playerPrefab == null)
            {
                return;
            }

            PlayerNetwork local = PlayerNetwork.LocalPlayer;
            PlayerAppearance active = local != null ? local.GetComponent<PlayerAppearance>() : null;
            int selected = active != null ? active.SelectedIndex : PlayerAppearance.PreferredCharacterIndex;
            GUILayout.BeginArea(_area, GUI.skin.box);
            GUILayout.Label("Personagem");
            for (int i = 0; i < _playerPrefab.CharacterCount; i++)
            {
                string name = _playerPrefab.GetCharacterName(i);
                if (GUILayout.Button(i == selected ? $"● {name}" : name))
                {
                    PlayerAppearance.PreferredCharacterIndex = i;
                    if (active != null)
                    {
                        active.RequestCharacter(i);
                    }
                }
            }

            GUILayout.EndArea();
        }
    }
}
