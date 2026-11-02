using GameYunit.Combat;
using GameYunit.Player;
using UnityEngine;
using UnityEngine.UI;

namespace GameYunit.UI
{
    /// <summary>HUD local por Observer, sem polling, RPC ou acesso de escrita à vida.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Slider), typeof(CanvasGroup))]
    public sealed class PlayerHealthUI : MonoBehaviour
    {
        private Slider _healthBar;
        private CanvasGroup _visibility;
        private PlayerNetwork _player;
        private HealthComponent _health;

        private void Awake()
        {
            _healthBar = GetComponent<Slider>();
            _visibility = GetComponent<CanvasGroup>();
            _visibility.interactable = false;
            _visibility.blocksRaycasts = false;
            _healthBar.minValue = 0f;
            _healthBar.wholeNumbers = true;
        }

        private void OnEnable()
        {
            PlayerNetwork.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;
            PlayerNetwork.OnLocalPlayerDespawned += HandleLocalPlayerDespawned;
            HideAndUnbind();
            HandleLocalPlayerSpawned(PlayerNetwork.LocalPlayer);
        }

        private void OnDisable()
        {
            PlayerNetwork.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
            PlayerNetwork.OnLocalPlayerDespawned -= HandleLocalPlayerDespawned;
            HideAndUnbind();
        }

        private void HandleLocalPlayerSpawned(PlayerNetwork player)
        {
            if (player == null || !player.IsSpawned || !player.IsClient || !player.IsOwner)
            {
                return;
            }

            HideAndUnbind();
            if (!player.TryGetComponent(out HealthComponent health))
            {
                Debug.LogWarning("[PlayerHealthUI] Adicione HealthComponent ao prefab do jogador.", player);
                return;
            }

            _player = player;
            _health = health;
            _health.OnHealthChanged += HandleHealthChanged;
            _healthBar.maxValue = _health.MaxHealth;
            _healthBar.SetValueWithoutNotify(_health.CurrentHealth);
            _visibility.alpha = 1f;
        }

        private void HandleLocalPlayerDespawned(PlayerNetwork player)
        {
            if (_player == player)
            {
                HideAndUnbind();
            }
        }

        private void HandleHealthChanged(int previous, int current) => _healthBar.SetValueWithoutNotify(current);

        private void HideAndUnbind()
        {
            if (_health != null)
            {
                _health.OnHealthChanged -= HandleHealthChanged;
            }

            _health = null;
            _player = null;
            // Mantém o GameObject ativo para continuar recebendo eventos de um novo jogador.
            _visibility.alpha = 0f;
            _healthBar.SetValueWithoutNotify(0f);
        }
    }
}
