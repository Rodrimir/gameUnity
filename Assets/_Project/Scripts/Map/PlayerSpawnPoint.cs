using System.Collections.Generic;
using UnityEngine;

namespace GameYunit.Map
{
    /// <summary>
    /// Marcador de cena para o nascimento de um jogador. Não tem NetworkObject: o servidor lê a
    /// posição na aprovação da conexão e o NGO cria o PlayerObject já no lugar certo em todos os peers.
    /// </summary>
    /// <remarks>
    /// Posicione o pivot sobre o chão (o pivot do jogador fica nos pés) e aponte o eixo azul (+Z)
    /// para onde o jogador deve olhar ao nascer.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerSpawnPoint : MonoBehaviour
    {
        private static readonly List<PlayerSpawnPoint> Active = new List<PlayerSpawnPoint>();

        [Tooltip("Ordem de conexão atendida: 0 = Host, 1 = segundo jogador. Slots ausentes usam rodízio.")]
        [SerializeField, Min(0)] private int _slot;

        public int Slot => _slot;

        private void OnEnable()
        {
            if (!Active.Contains(this))
            {
                Active.Add(this);
            }
        }

        private void OnDisable() => Active.Remove(this);

        /// <summary>
        /// Escolhe o ponto do slot pedido; se não existir, faz rodízio pelos pontos ativos.
        /// </summary>
        /// <returns>False quando a cena não tem nenhum ponto ativo.</returns>
        public static bool TryGetSpawn(int playerIndex, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (Active.Count == 0)
            {
                return false;
            }

            PlayerSpawnPoint chosen = null;
            for (int i = 0; i < Active.Count; i++)
            {
                if (Active[i]._slot == playerIndex)
                {
                    chosen = Active[i];
                    break;
                }
            }

            if (chosen == null)
            {
                chosen = Active[Mathf.Abs(playerIndex) % Active.Count];
            }

            Transform t = chosen.transform;
            Vector3 forward = Vector3.ProjectOnPlane(t.forward, Vector3.up);
            position = t.position;
            rotation = forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward, Vector3.up) : Quaternion.identity;
            return true;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
            Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + transform.forward);
        }

        // Com "Enter Play Mode Options" (sem domain reload) estáticos sobrevivem entre sessões de Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();
    }
}
