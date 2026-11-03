using UnityEngine;

namespace GameYunit.Combat
{
    /// <summary>
    /// Dados de uma arma de ataque automático. Os comportamentos são parâmetros combináveis
    /// (leque, teleguiado, perfuração), não subclasses: as armas iniciais são assets com valores
    /// diferentes, e upgrades futuros podem misturá-los (ex.: leque teleguiado).
    /// </summary>
    /// <remarks>
    /// Somente o servidor lê dano e perfuração. Os parâmetros de trajetória viajam no
    /// <see cref="ProjectileVolley"/>, então alterá-los em runtime no servidor não dessincroniza os clients.
    /// </remarks>
    [CreateAssetMenu(menuName = "GameYunit/Combat/Weapon Definition", fileName = "Weapon_")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        public enum ProjectileStyle { ArcaneLightning, Bullet, PiercingBolt }
        public enum AimMode
        {
            /// <summary>Mira o inimigo mais próximo dentro do raio.</summary>
            NearestEnemy,

            /// <summary>Dispara para a frente do personagem (mira independente do Bloco 1).</summary>
            PlayerFacing
        }

        [Header("Identificação")]
        [SerializeField] private string _displayName = "Arma";

        [SerializeField, TextArea] private string _description;

        [Tooltip("Visual opcional desta arma (sem NetworkObject). Vazio usa o prefab padrão do AutoAttackSystem.")]
        [SerializeField] private Projectile _projectilePrefab;

        [Header("Cadência e alvo")]
        [SerializeField, Min(0.05f)] private float _fireInterval = 0.5f;

        [Tooltip("Distância horizontal máxima para haver alvo. Sem inimigo no raio, a arma não dispara.")]
        [SerializeField, Min(0.5f)] private float _attackRadius = 12f;

        [SerializeField] private AimMode _aim = AimMode.NearestEnemy;
        [SerializeField] private ProjectileStyle _visualStyle = ProjectileStyle.Bullet;

        [Header("Projétil")]
        [SerializeField, Min(0f)] private float _damage = 10f;

        [SerializeField, Min(0.1f)] private float _projectileSpeed = 20f;

        [Tooltip("Alcance efetivo = velocidade x tempo de vida. Mantenha acima do raio de ataque.")]
        [SerializeField, Min(0.05f)] private float _projectileLifetime = 1.2f;

        [Header("Leque")]
        [Tooltip("Projéteis por disparo.")]
        [SerializeField, Range(1, 16)] private int _projectileCount = 1;

        [Tooltip("Abertura total do leque em graus, distribuída igualmente entre os projéteis.")]
        [SerializeField, Range(0f, 180f)] private float _spreadAngle;

        [Header("Teleguiado")]
        [Tooltip("Curva máxima em graus/s em direção ao alvo do disparo. Zero = trajetória reta.")]
        [SerializeField, Min(0f)] private float _homingTurnRate;

        [Header("Perfuração")]
        [Tooltip("Inimigos extras atravessados. Zero = o projétil para no primeiro inimigo.")]
        [SerializeField, Min(0)] private int _pierceCount;

        [Tooltip("Multiplicador de dano aplicado a cada inimigo atravessado (dano decrescente).")]
        [SerializeField, Range(0.05f, 1f)] private float _pierceDamageFalloff = 1f;

        public string DisplayName => _displayName;
        public string Description => _description;
        public Projectile ProjectilePrefab => _projectilePrefab;
        public float FireInterval => _fireInterval;
        public float AttackRadius => _attackRadius;
        public AimMode Aim => _aim;
        public ProjectileStyle VisualStyle => _visualStyle;
        public float Damage => _damage;
        public float ProjectileSpeed => _projectileSpeed;
        public float ProjectileLifetime => _projectileLifetime;
        public int ProjectileCount => _projectileCount;
        public float SpreadAngle => _spreadAngle;
        public float HomingTurnRate => _homingTurnRate;
        public int PierceCount => _pierceCount;
        public float PierceDamageFalloff => _pierceDamageFalloff;
    }
}
