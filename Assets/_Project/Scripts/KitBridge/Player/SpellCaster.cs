using System;
using System.Collections.Generic;
using GameYunit.Combat;
using GameYunit.Network;
using GameYunit.Spells;
using Unity.Netcode;
using UnityEngine;

namespace GameYunit.KitBridge
{
    /// <summary>
    /// Conjura a magia escolhida pelo dono. O gesto reaproveita o golpe de cajado do kit, que o
    /// <see cref="PlayerController"/> já dispara por <c>NetworkAnimator</c> no clique esquerdo — então a
    /// animação replica sozinha e aqui só entram a recarga, o efeito visual e a área atingida.
    /// </summary>
    /// <remarks>
    /// O servidor é quem decide: valida a recarga, monta a hitbox a partir da própria cópia do jogador
    /// (nunca de coordenadas enviadas pelo cliente) e avisa os peers para tocar o VFX. O dano ainda não é
    /// aplicado — pertence à Fase 3 —, mas <see cref="TargetsHitServer"/> já entrega os alvos da área,
    /// que é o ponto onde vida, XP e upgrades vão se encaixar.
    /// </remarks>
    [RequireComponent(typeof(NetworkPlayerIdentity), typeof(PlayerController))]
    public sealed class SpellCaster : NetworkBehaviour, ISpellCaster
    {
        [Tooltip("Catálogo compartilhado; a posição na lista é o que viaja pela rede.")]
        [SerializeField] private SpellCatalog _catalog;
        [Tooltip("Camadas que a área da magia considera alvo. Na Fase 3 recebe a camada dos inimigos.")]
        [SerializeField] private LayerMask _targetLayers = ~0;
        [Tooltip("Altura do ombro, de onde a direção da conjuração é medida.")]
        [SerializeField] private float _castOriginHeight = 1.2f;

        private NetworkPlayerIdentity _identity;
        private PlayerController _controller;
        private readonly List<PendingCast> _pendingServer = new();
        private readonly Collider[] _overlap = new Collider[64];
        private readonly HashSet<NetworkHealth> _hitRoots = new();
        // Recargas separadas: no Host o mesmo objeto é dono e servidor, e um campo só faria a previsão
        // local do dono ser lida como recarga pelo servidor, que recusaria a própria conjuração.
        private float _ownerReadyAt, _serverReadyAt;

        private struct PendingCast
        {
            public float ApplyAt;
            public Vector3 Origin;
            public Vector3 Forward;
            public int SpellIndex;
        }

        /// <summary>Alvos encontrados na área, no servidor. Ponte para o dano da Fase 3.</summary>
        public event Action<SpellDefinition, IReadOnlyList<Collider>> TargetsHitServer;

        /// <summary>Fração da recarga já cumprida, de 0 (acabou de conjurar) a 1 (pronto).</summary>
        public float CooldownProgress
        {
            get
            {
                var spell = CurrentSpell;
                if (spell == null) return 1f;
                float remaining = _ownerReadyAt - Time.time;
                return remaining <= 0f ? 1f : Mathf.Clamp01(1f - remaining / spell.Cooldown);
            }
        }

        public SpellDefinition CurrentSpell =>
            _catalog == null ? null : _catalog[_identity != null ? _identity.SpellIndex.Value : 0];

        public override void OnNetworkSpawn()
        {
            _identity = GetComponent<NetworkPlayerIdentity>();
            _controller = GetComponent<PlayerController>();
            if (_catalog == null)
                Debug.LogWarning("[SpellCaster] Sem catálogo de magias; a conjuração fica desligada.");
        }

        private void Update()
        {
            if (IsOwner) UpdateOwner();
            if (IsServer) UpdateServer();
        }

        private void UpdateOwner()
        {
            var spell = CurrentSpell;
            if (spell == null) return;
            bool ready = Time.time >= _ownerReadyAt;
            // Trava o gesto do kit enquanto recarrega, para o clique não animar um golpe sem efeito.
            if (_controller.canAttack != ready) _controller.SetCanAttack(ready);
            var session = CoopSession.Instance;
            if (!ready || session == null || session.LocalMenuOpen) return;
            if (!UnityEngine.Input.GetButtonDown("Fire1")) return;
            // Previsão local: a recarga começa já, sem esperar a volta do servidor.
            _ownerReadyAt = Time.time + spell.Cooldown;
            CastServerRpc();
        }

        [Rpc(SendTo.Server)]
        private void CastServerRpc()
        {
            var spell = CurrentSpell;
            if (spell == null) return;
            // Margem de tolerância: a previsão do dono pode chegar pouco antes por causa da latência.
            if (Time.time < _serverReadyAt - 0.15f)
            {
                Debug.Log($"[SpellCaster] Conjuração de {OwnerClientId} ignorada: ainda recarregando.");
                return;
            }
            _serverReadyAt = Time.time + spell.Cooldown;
            Vector3 origin = transform.position + Vector3.up * _castOriginHeight;
            Vector3 forward = transform.forward;
            _pendingServer.Add(new PendingCast
            {
                ApplyAt = Time.time + spell.CastDelay,
                Origin = origin,
                Forward = forward,
                SpellIndex = _identity.SpellIndex.Value
            });
            PlayEffectRpc(spell.ImpactPoint(origin, forward), forward, _identity.SpellIndex.Value);
        }

        private void UpdateServer()
        {
            for (int i = _pendingServer.Count - 1; i >= 0; i--)
            {
                if (Time.time < _pendingServer[i].ApplyAt) continue;
                ApplyAreaServer(_pendingServer[i]);
                _pendingServer.RemoveAt(i);
            }
        }

        /// <summary>Monta a hitbox da magia e entrega os alvos; ainda não aplica dano.</summary>
        private void ApplyAreaServer(PendingCast cast)
        {
            var spell = _catalog[cast.SpellIndex];
            if (spell == null) return;
            Vector3 center = spell.Shape == SpellShape.Sphere
                ? spell.ImpactPoint(cast.Origin, cast.Forward)
                : cast.Origin;
            int found = Physics.OverlapSphereNonAlloc(center, spell.BroadRadius, _overlap, _targetLayers,
                QueryTriggerInteraction.Ignore);
            var hits = new List<Collider>();
            _hitRoots.Clear();
            for (int i = 0; i < found; i++)
            {
                var collider = _overlap[i];
                if (collider == null || collider.transform.IsChildOf(transform)) continue;
                if (!spell.Contains(cast.Origin, cast.Forward, collider.bounds.center)) continue;
                // Um alvo com vários colliders entraria na lista mais de uma vez e tomaria dano repetido.
                var root = collider.GetComponentInParent<NetworkHealth>();
                if (root != null && !_hitRoots.Add(root)) continue;
                hits.Add(collider);
                if (spell.MaxTargets > 0 && hits.Count >= spell.MaxTargets) break;
            }
            foreach (var hit in hits)
            {
                var health = hit.GetComponentInParent<NetworkHealth>();
                if (health != null) health.ApplyDamageServer(spell.Damage);
            }
            Debug.Log($"[SpellCaster] {spell.DisplayName} de {OwnerClientId}: {hits.Count} alvo(s) na área.");
            TargetsHitServer?.Invoke(spell, hits);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PlayEffectRpc(Vector3 impact, Vector3 forward, int spellIndex)
        {
            var spell = _catalog == null ? null : _catalog[spellIndex];
            if (spell == null || spell.EffectPrefab == null) return;
            Vector3 position = impact + Vector3.up * spell.EffectHeightOffset;
            Quaternion rotation = Quaternion.LookRotation(
                Vector3.ProjectOnPlane(forward, Vector3.up).sqrMagnitude > 0.0001f
                    ? Vector3.ProjectOnPlane(forward, Vector3.up)
                    : Vector3.forward, Vector3.up);
            // VFX é puramente visual: cada peer instancia o seu, sem NetworkObject.
            var effect = Instantiate(spell.EffectPrefab, position, rotation);
            Destroy(effect, spell.EffectLifetime);
        }

        private void OnDrawGizmosSelected()
        {
            var spell = Application.isPlaying ? CurrentSpell : null;
            if (spell == null) return;
            Vector3 origin = transform.position + Vector3.up * _castOriginHeight;
            Gizmos.color = new Color(spell.Tint.r, spell.Tint.g, spell.Tint.b, 0.35f);
            if (spell.Shape == SpellShape.Sphere)
                Gizmos.DrawWireSphere(spell.ImpactPoint(origin, transform.forward), spell.Radius);
            else
                Gizmos.DrawWireSphere(origin, spell.Range);
        }
    }
}
