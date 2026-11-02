using GameYunit.Player;
using GameYunit.Progression;
using UnityEngine;

// Namespace propositalmente sem ".Debug": um namespace com esse nome sombrearia UnityEngine.Debug.
namespace GameYunit.UI
{
    /// <summary>
    /// HUD mínimo de desenvolvimento (IMGUI) com nível e XP do jogador local.
    /// Visível apenas no Editor e em Development Builds; a barra definitiva de XP virá com a UI do jogo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelDebugHUD : MonoBehaviour
    {
        [SerializeField] private Rect _area = new Rect(10f, 310f, 240f, 60f);

        private PlayerLevel _level;
        private string _text = string.Empty;
        private string _lastLevelUp = string.Empty;

        private void OnEnable()
        {
            PlayerNetwork.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;
            PlayerNetwork.OnLocalPlayerDespawned += HandleLocalPlayerDespawned;
            HandleLocalPlayerSpawned(PlayerNetwork.LocalPlayer);
        }

        private void OnDisable()
        {
            PlayerNetwork.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
            PlayerNetwork.OnLocalPlayerDespawned -= HandleLocalPlayerDespawned;
            SetLevel(null);
        }

        private void OnGUI()
        {
            if (!Debug.isDebugBuild || _level == null)
            {
                return;
            }

            GUILayout.BeginArea(_area, GUI.skin.box);
            GUILayout.Label(_text);
            GUILayout.Label(_lastLevelUp);
            GUILayout.EndArea();
        }

        private void HandleLocalPlayerSpawned(PlayerNetwork player)
        {
            if (player != null && player.TryGetComponent(out PlayerLevel level))
            {
                SetLevel(level);
            }
        }

        private void HandleLocalPlayerDespawned(PlayerNetwork player)
        {
            if (_level != null && player != null && _level.gameObject == player.gameObject)
            {
                SetLevel(null);
            }
        }

        private void SetLevel(PlayerLevel level)
        {
            if (_level != null)
            {
                _level.OnXpChanged -= HandleXpChanged;
                _level.OnLevelUp -= HandleLevelUp;
            }

            _level = level;
            _lastLevelUp = string.Empty;
            if (_level == null)
            {
                return;
            }

            _level.OnXpChanged += HandleXpChanged;
            _level.OnLevelUp += HandleLevelUp;
            HandleXpChanged(_level.Xp, _level.XpToNextLevel);
        }

        // Textos montados só nos eventos: OnGUI roda várias vezes por frame.
        private void HandleXpChanged(int xp, int required) =>
            _text = _level.IsMaxLevel ? $"Nível {_level.Level} (máx.)" : $"Nível {_level.Level} — XP {xp}/{required}";

        private void HandleLevelUp(int level) => _lastLevelUp = $"Level Up! Nível {level}";
    }
}
