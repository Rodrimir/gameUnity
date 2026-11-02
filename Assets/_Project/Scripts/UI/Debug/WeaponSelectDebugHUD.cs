using GameYunit.Combat;
using GameYunit.Player;
using UnityEngine;

// Namespace propositalmente sem ".Debug": um namespace com esse nome sombrearia UnityEngine.Debug.
namespace GameYunit.UI
{
    /// <summary>
    /// HUD mínimo de desenvolvimento (IMGUI) para escolher a arma inicial do jogador local.
    /// Visível apenas no Editor e em Development Builds; a tela definitiva de seleção virá depois.
    /// </summary>
    /// <remarks>
    /// Só observa eventos (spawn local e troca de arma) e envia o pedido ao servidor via
    /// <see cref="AutoAttackSystem.RequestWeapon"/>; nunca troca a arma localmente.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class WeaponSelectDebugHUD : MonoBehaviour
    {
        [SerializeField] private Rect _area = new Rect(10f, 150f, 240f, 150f);

        private AutoAttackSystem _attack;
        private string[] _labels = System.Array.Empty<string>();
        private string _current = "Arma: —";

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
            SetAttack(null);
        }

        private void OnGUI()
        {
            if (!Debug.isDebugBuild || _attack == null)
            {
                return;
            }

            GUILayout.BeginArea(_area, GUI.skin.box);
            GUILayout.Label(_current);
            for (int i = 0; i < _labels.Length; i++)
            {
                if (GUILayout.Button(_labels[i]))
                {
                    _attack.RequestWeapon(i);
                }
            }

            GUILayout.EndArea();
        }

        private void HandleLocalPlayerSpawned(PlayerNetwork player)
        {
            if (player != null && player.TryGetComponent(out AutoAttackSystem attack))
            {
                SetAttack(attack);
            }
        }

        private void HandleLocalPlayerDespawned(PlayerNetwork player)
        {
            if (_attack != null && player != null && _attack.gameObject == player.gameObject)
            {
                SetAttack(null);
            }
        }

        private void SetAttack(AutoAttackSystem attack)
        {
            if (_attack != null)
            {
                _attack.OnWeaponChanged -= HandleWeaponChanged;
            }

            _attack = attack;
            _labels = System.Array.Empty<string>();
            if (_attack == null)
            {
                return;
            }

            // Rótulos montados uma vez: OnGUI roda várias vezes por frame.
            _labels = new string[_attack.Weapons.Count];
            for (int i = 0; i < _labels.Length; i++)
            {
                WeaponDefinition weapon = _attack.Weapons[i];
                _labels[i] = weapon != null ? $"{i + 1}. {weapon.DisplayName}" : $"{i + 1}. (vazio)";
            }

            _attack.OnWeaponChanged += HandleWeaponChanged;
            HandleWeaponChanged(_attack.CurrentWeapon);
        }

        private void HandleWeaponChanged(WeaponDefinition weapon) =>
            _current = weapon != null ? $"Arma: {weapon.DisplayName}" : "Arma: —";
    }
}
