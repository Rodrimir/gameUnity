using System;
using System.Collections.Generic;
using GameYunit.Core.Pooling;
using GameYunit.Enemies;
using Unity.Netcode;
using UnityEngine;

namespace GameYunit.Combat
{
    /// <summary>
    /// Ataque automático do jogador. O servidor escolhe o alvo conforme a arma equipada, dispara em
    /// intervalo fixo, simula os projéteis e aplica o dano. Os clients recebem um RPC por rajada e
    /// reproduzem os projéteis a partir do pool local, adiantados pela latência.
    /// </summary>
    /// <remarks>
    /// <para>Projéteis não são NetworkObjects: um bullet heaven pode ter centenas deles, e replicar
    /// Transform por frame custaria muito mais banda do que um RPC por rajada.</para>
    /// <para>A arma equipada é uma NetworkVariable escrita só pelo servidor; o Owner pede a troca por RPC.</para>
    /// <para>No Host, as instâncias simuladas pelo servidor também são o visual.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class AutoAttackSystem : NetworkBehaviour
    {
        private const string LogPrefix = "[AutoAttackSystem]";

        [Header("Armas iniciais")]
        [Tooltip("Armas que o jogador pode escolher. A ordem define o índice sincronizado; deve ser igual em todos os peers.")]
        [SerializeField] private WeaponDefinition[] _weapons = Array.Empty<WeaponDefinition>();

        [SerializeField, Min(0)] private int _startingWeaponIndex;

        [Header("Projétil")]
        [Tooltip("Visual padrão (SEM NetworkObject) para armas sem prefab próprio. Cada peer usa o próprio pool.")]
        [SerializeField] private Projectile _projectilePrefab;

        [SerializeField, Min(0)] private int _prewarmCount = 32;

        [Header("Ponto de disparo")]
        [Tooltip("Altura do disparo acima do pivot nos pés.")]
        [SerializeField] private float _muzzleHeight = 1f;

        [Tooltip("Distância horizontal do centro do jogador ao ponto de disparo.")]
        [SerializeField, Min(0f)] private float _muzzleDistance = 0.7f;

        private readonly NetworkVariable<int> _weaponIndex = new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly HashSet<Projectile> _serverProjectiles = new HashSet<Projectile>();
        private readonly Dictionary<uint, Projectile> _clientVisuals = new Dictionary<uint, Projectile>();
        private IObjectPoolManager _pool;
        private float _nextFireTime;
        private uint _nextShotId;
        private bool _ready;

        /// <summary>Arma escolhida no menu local antes de entrar na sessão.</summary>
        public static int PreferredStartingWeaponIndex { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPreferredStartingWeapon() => PreferredStartingWeaponIndex = 0;

        #region Eventos (Observer)

        /// <summary>Arma equipada mudou (todos os peers). Use para HUD/ícone da arma.</summary>
        public event Action<WeaponDefinition> OnWeaponChanged;

        /// <summary>Rajada criada. No servidor ao disparar; nos clients ao criar os visuais. Use para SFX/muzzle flash.</summary>
        public event Action<ProjectileVolley> OnVolleyFired;

        /// <summary>Projétil terminou contra algo. Em todos os peers; use para VFX/SFX de impacto.</summary>
        public event Action<Vector3> OnProjectileImpact;

        /// <summary>[Servidor] Um inimigo/alvo foi atingido: objeto, ponto e dano aplicado.</summary>
        public event Action<GameObject, Vector3, float> OnServerProjectileHit;

        #endregion

        public IReadOnlyList<WeaponDefinition> Weapons => _weapons;

        public int CurrentWeaponIndex => _weaponIndex.Value;

        public WeaponDefinition CurrentWeapon => GetWeapon(_weaponIndex.Value);

        /// <summary>[Servidor] Projéteis autoritativos em voo deste jogador.</summary>
        public int ActiveServerProjectileCount => _serverProjectiles.Count;

        #region Ciclo de Rede

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                int preferred = IsOwner ? PreferredStartingWeaponIndex : _startingWeaponIndex;
                _weaponIndex.Value = Mathf.Clamp(preferred, 0, Mathf.Max(0, _weapons.Length - 1));
                // Dispara assim que houver alvo; a cadência conta a partir do primeiro disparo.
                _nextFireTime = Time.time;
                _nextShotId = 0;
            }

            // Assinado depois da arma inicial: o evento abaixo cobre o estado inicial uma única vez.
            _weaponIndex.OnValueChanged += HandleWeaponIndexChanged;

            _pool = ObjectPoolManager.Instance;
            _ready = _pool != null && _weapons.Length > 0 && GetWeapon(0) != null;
            if (!_ready)
            {
                Debug.LogError($"{LogPrefix} Requer um ObjectPoolManager na cena e ao menos uma WeaponDefinition.", this);
                return;
            }

            // Servidor precisa das instâncias simuladas; clients, dos visuais.
            for (int i = 0; i < _weapons.Length; i++)
            {
                Projectile prefab = GetProjectilePrefab(_weapons[i]);
                if (prefab != null)
                {
                    _pool.Prewarm(prefab.gameObject, _prewarmCount);
                }
            }

            OnWeaponChanged?.Invoke(CurrentWeapon);

            if (IsClient && IsOwner && PreferredStartingWeaponIndex >= 0 &&
                PreferredStartingWeaponIndex < _weapons.Length && PreferredStartingWeaponIndex != CurrentWeaponIndex)
            {
                RequestWeapon(PreferredStartingWeaponIndex);
            }
        }

        public override void OnNetworkDespawn()
        {
            _weaponIndex.OnValueChanged -= HandleWeaponIndexChanged;
            _ready = false;
            ReleaseAllProjectiles();
            base.OnNetworkDespawn();
        }

        #endregion

        #region Seleção de arma

        /// <summary>Pede ao servidor a troca de arma. Só o Owner pode chamar; o servidor valida o índice.</summary>
        public void RequestWeapon(int index)
        {
            if (IsSpawned && IsOwner && index != _weaponIndex.Value)
            {
                RequestWeaponRpc(index);
            }
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void RequestWeaponRpc(int index, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || GetWeapon(index) == null)
            {
                Debug.LogWarning($"{LogPrefix} Troca de arma recusada (client {rpcParams.Receive.SenderClientId}, índice {index}).", this);
                return;
            }

            _weaponIndex.Value = index;
        }

        private void HandleWeaponIndexChanged(int previous, int current) => OnWeaponChanged?.Invoke(GetWeapon(current));

        private WeaponDefinition GetWeapon(int index) => index >= 0 && index < _weapons.Length ? _weapons[index] : null;

        private Projectile GetProjectilePrefab(WeaponDefinition weapon) =>
            weapon != null && weapon.ProjectilePrefab != null ? weapon.ProjectilePrefab : _projectilePrefab;

        #endregion

        #region Servidor (alvo, disparo e impacto)

        private void Update()
        {
            if (!_ready || !IsSpawned || !IsServer || Time.time < _nextFireTime)
            {
                return;
            }

            WeaponDefinition weapon = CurrentWeapon;
            if (weapon == null || GetProjectilePrefab(weapon) == null)
            {
                return;
            }

            EnemyAI target = FindNearestEnemy(weapon.AttackRadius);
            if (target == null)
            {
                // Sem alvo, a arma fica "engatilhada" e dispara assim que um inimigo entrar no raio.
                return;
            }

            // Não acumula rajadas de compensação após um frame lento.
            _nextFireTime = Time.time + weapon.FireInterval;
            FireServer(weapon, target);
        }

        private EnemyAI FindNearestEnemy(float radius)
        {
            Vector3 origin = transform.position;
            float nearestSqr = radius * radius;
            EnemyAI nearest = null;

            IReadOnlyList<EnemyAI> enemies = EnemyAI.ActiveOnServer;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyAI enemy = enemies[i];
                if (enemy == null || !enemy.IsSpawned)
                {
                    continue;
                }

                Vector3 offset = enemy.transform.position - origin;
                offset.y = 0f;
                float distanceSqr = offset.sqrMagnitude;
                if (distanceSqr <= nearestSqr)
                {
                    nearestSqr = distanceSqr;
                    nearest = enemy;
                }
            }

            return nearest;
        }

        private void FireServer(WeaponDefinition weapon, EnemyAI target)
        {
            Vector3 direction;
            if (weapon.Aim == WeaponDefinition.AimMode.PlayerFacing)
            {
                direction = transform.forward;
            }
            else
            {
                direction = target.transform.position - transform.position;
            }

            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;

            bool homing = weapon.HomingTurnRate > 0f;
            var volley = new ProjectileVolley
            {
                FirstId = _nextShotId,
                WeaponIndex = (byte)_weaponIndex.Value,
                Count = (byte)Mathf.Clamp(weapon.ProjectileCount, 1, byte.MaxValue),
                SpreadAngle = weapon.SpreadAngle,
                Origin = transform.position + Vector3.up * _muzzleHeight + direction * _muzzleDistance,
                Direction = direction,
                Speed = weapon.ProjectileSpeed,
                Lifetime = weapon.ProjectileLifetime,
                HomingTurnRate = weapon.HomingTurnRate,
                HasTarget = homing,
                TargetId = homing ? target.NetworkObjectId : 0,
                FireTime = NetworkManager.ServerTime.Time
            };
            _nextShotId += volley.Count;

            NetworkObject homingTarget = homing ? target.NetworkObject : null;
            SpawnVolley(volley, GetProjectilePrefab(weapon), homingTarget, weapon, elapsed: 0f, serverSimulated: true);
            FireVolleyRpc(volley);
            OnVolleyFired?.Invoke(volley);
        }

        /// <summary>Chamado pelo projétil autoritativo a cada inimigo atingido (inclusive ao perfurar).</summary>
        internal void HandleServerHit(Collider hitCollider, Vector3 hitPoint, float damage)
        {
            IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
            damageable?.ApplyDamageServer(damage, OwnerClientId, hitPoint);
            OnServerProjectileHit?.Invoke(hitCollider.gameObject, hitPoint, damage);
        }

        /// <summary>Chamado pelo projétil autoritativo ao parar (inimigo final ou cenário). Encerra o visual nos clients.</summary>
        internal void HandleServerProjectileEnded(Projectile projectile, Vector3 hitPoint)
        {
            if (!_serverProjectiles.Remove(projectile))
            {
                return;
            }

            if (IsSpawned)
            {
                EndShotRpc(projectile.ShotId, hitPoint);
            }

            OnProjectileImpact?.Invoke(hitPoint);
            _pool.Release(projectile.gameObject);
        }

        #endregion

        #region Clients (visual)

        /// <summary>
        /// [Server -> Clients] Cria os visuais da rajada. Confiável e ordenado com <see cref="EndShotRpc"/>,
        /// para que um fim de projétil nunca chegue antes do disparo correspondente.
        /// </summary>
        [Rpc(SendTo.NotServer)]
        private void FireVolleyRpc(ProjectileVolley volley)
        {
            if (!_ready || volley.Direction.sqrMagnitude < 0.5f || volley.Count == 0)
            {
                return;
            }

            // Adianta o visual pelo tempo de trânsito: ele aparece onde o projétil está no servidor.
            float elapsed = Mathf.Max(0f, (float)(NetworkManager.ServerTime.Time - volley.FireTime));
            if (elapsed >= volley.Lifetime)
            {
                return;
            }

            NetworkObject target = null;
            if (volley.HasTarget)
            {
                NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(volley.TargetId, out target);
            }

            WeaponDefinition weapon = GetWeapon(volley.WeaponIndex);
            Projectile prefab = GetProjectilePrefab(weapon);
            if (prefab != null)
            {
                SpawnVolley(volley, prefab, target, weapon: null, elapsed, serverSimulated: false);
                OnVolleyFired?.Invoke(volley);
            }
        }

        /// <summary>[Server -> Clients] O projétil parou no servidor; remove o visual correspondente.</summary>
        [Rpc(SendTo.NotServer)]
        private void EndShotRpc(uint shotId, Vector3 hitPoint)
        {
            if (_clientVisuals.TryGetValue(shotId, out Projectile projectile))
            {
                _clientVisuals.Remove(shotId);
                _pool.Release(projectile.gameObject);
            }

            OnProjectileImpact?.Invoke(hitPoint);
        }

        #endregion

        /// <param name="weapon">Somente no servidor: fornece dano e perfuração. Null nos visuais.</param>
        private void SpawnVolley(in ProjectileVolley volley, Projectile prefab, NetworkObject target,
            WeaponDefinition weapon, float elapsed, bool serverSimulated)
        {
            for (int i = 0; i < volley.Count; i++)
            {
                var data = new Projectile.LaunchData
                {
                    Origin = volley.Origin,
                    Direction = volley.GetDirection(i),
                    Speed = volley.Speed,
                    Lifetime = volley.Lifetime,
                    HomingTurnRate = volley.HomingTurnRate,
                    Target = target,
                    Damage = weapon != null ? weapon.Damage : 0f,
                    PierceCount = weapon != null ? weapon.PierceCount : 0,
                    PierceDamageFalloff = weapon != null ? weapon.PierceDamageFalloff : 1f
                };

                uint shotId = volley.FirstId + (uint)i;
                Projectile projectile = _pool.Get(prefab, volley.Origin, Quaternion.identity);
                projectile.Launch(this, shotId, data, elapsed, serverSimulated);

                if (serverSimulated)
                {
                    _serverProjectiles.Add(projectile);
                    continue;
                }

                if (_clientVisuals.TryGetValue(shotId, out Projectile stale))
                {
                    _pool.Release(stale.gameObject);
                }

                _clientVisuals[shotId] = projectile;
            }
        }

        /// <summary>
        /// Fim do tempo de vida sem colisão final. Servidor e clients expiram sozinhos no mesmo instante,
        /// então nenhum RPC é necessário.
        /// </summary>
        internal void HandleProjectileExpired(Projectile projectile)
        {
            if (projectile.IsServerSimulated)
            {
                if (!_serverProjectiles.Remove(projectile))
                {
                    return;
                }
            }
            else if (_clientVisuals.TryGetValue(projectile.ShotId, out Projectile current) && current == projectile)
            {
                _clientVisuals.Remove(projectile.ShotId);
            }

            _pool.Release(projectile.gameObject);
        }

        private void ReleaseAllProjectiles()
        {
            if (_pool == null)
            {
                return;
            }

            foreach (Projectile projectile in _serverProjectiles)
            {
                if (projectile != null)
                {
                    _pool.Release(projectile.gameObject);
                }
            }

            foreach (Projectile projectile in _clientVisuals.Values)
            {
                if (projectile != null)
                {
                    _pool.Release(projectile.gameObject);
                }
            }

            _serverProjectiles.Clear();
            _clientVisuals.Clear();
        }
    }
}
