using System;
using Unity.Netcode;
using UnityEngine;

namespace GameYunit.Progression
{
    /// <summary>
    /// XP e nível do jogador. Só o servidor escreve; as NetworkVariables replicam para todos os peers
    /// e os eventos C# disparam em cada peer a partir de OnValueChanged.
    /// </summary>
    /// <remarks>
    /// XP é individual por jogador. A curva é linear: para sair do nível N são necessários
    /// <c>Base + Incremento × (N − 1)</c> pontos; a sobra passa para o nível seguinte.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerLevel : NetworkBehaviour
    {
        [Header("Curva de XP (lida no servidor)")]
        [Tooltip("XP necessário para sair do nível 1.")]
        [SerializeField, Min(1)] private int _baseXpToLevel = 5;

        [Tooltip("XP adicional exigido a cada nível.")]
        [SerializeField, Min(0)] private int _xpIncreasePerLevel = 5;

        [SerializeField, Min(1)] private int _maxLevel = 99;

        private readonly NetworkVariable<int> _level = new NetworkVariable<int>(1,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _xp = new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        #region Eventos (Observer)

        /// <summary>XP mudou (todos os peers): XP atual no nível e XP necessário para o próximo. Use para a barra de XP.</summary>
        public event Action<int, int> OnXpChanged;

        /// <summary>Nível alcançado (todos os peers). Dispara uma vez por nível, mesmo ao subir vários de uma vez.</summary>
        public event Action<int> OnLevelUp;

        /// <summary>Qualquer jogador subiu de nível (todos os peers). Ponto de entrada da futura tela de Level Up.</summary>
        public static event Action<PlayerLevel, int> OnAnyPlayerLevelUp;

        #endregion

        public int Level => _level.Value;

        /// <summary>XP acumulado dentro do nível atual.</summary>
        public int Xp => _xp.Value;

        public int XpToNextLevel => GetXpRequired(_level.Value);

        public bool IsMaxLevel => _level.Value >= _maxLevel;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _level.OnValueChanged += HandleLevelChanged;
            _xp.OnValueChanged += HandleXpChanged;
            OnXpChanged?.Invoke(Xp, XpToNextLevel);
        }

        public override void OnNetworkDespawn()
        {
            _level.OnValueChanged -= HandleLevelChanged;
            _xp.OnValueChanged -= HandleXpChanged;
            base.OnNetworkDespawn();
        }

        public int GetXpRequired(int level) => _baseXpToLevel + _xpIncreasePerLevel * Mathf.Max(0, level - 1);

        /// <summary>
        /// [Servidor] Soma XP e resolve quantos níveis forem alcançados. Chamadas fora do servidor são ignoradas.
        /// </summary>
        public void AddXpServer(int amount)
        {
            if (!IsServer || !IsSpawned || amount <= 0)
            {
                return;
            }

            int level = _level.Value;
            long xp = (long)_xp.Value + amount;
            while (level < _maxLevel && xp >= GetXpRequired(level))
            {
                xp -= GetXpRequired(level);
                level++;
            }

            if (level >= _maxLevel)
            {
                xp = Math.Min(xp, GetXpRequired(level));
            }

            _xp.Value = (int)xp;
            _level.Value = level;
        }

        private void HandleLevelChanged(int previous, int current)
        {
            for (int level = previous + 1; level <= current; level++)
            {
                OnLevelUp?.Invoke(level);
                OnAnyPlayerLevelUp?.Invoke(this, level);
            }

            OnXpChanged?.Invoke(Xp, XpToNextLevel);
        }

        private void HandleXpChanged(int previous, int current) => OnXpChanged?.Invoke(current, XpToNextLevel);

        // Com "Enter Play Mode Options" (sem domain reload) eventos estáticos sobrevivem entre sessões de Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => OnAnyPlayerLevelUp = null;
    }
}
