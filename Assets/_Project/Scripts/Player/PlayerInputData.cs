using System;
using Unity.Netcode;
using UnityEngine;

namespace GameYunit.Player
{
    /// <summary>
    /// Snapshot do input de um jogador, enviado do Owner para o servidor.
    /// Contém apenas intenção (o que o jogador quer fazer), nunca estado (posição, vida etc.):
    /// o servidor é a autoridade e decide o resultado.
    /// </summary>
    public struct PlayerInputData : INetworkSerializable, IEquatable<PlayerInputData>
    {
        /// <summary>Direção de movimento no plano (x = lateral, y = frente), magnitude 0..1.</summary>
        public Vector2 Move;

        public PlayerInputData(Vector2 move)
        {
            Move = move;
        }

        /// <summary>
        /// Versão segura para uso no servidor: descarta NaN/Infinity e limita a magnitude a 1.
        /// Nunca confie em dados vindos do client sem passar por aqui.
        /// </summary>
        public readonly PlayerInputData Sanitized()
        {
            if (!IsFinite(Move.x) || !IsFinite(Move.y))
            {
                return default;
            }

            return new PlayerInputData(Vector2.ClampMagnitude(Move, 1f));
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Move);
        }

        public readonly bool Equals(PlayerInputData other) => Move.Equals(other.Move);

        public override readonly bool Equals(object obj) => obj is PlayerInputData other && Equals(other);

        public override readonly int GetHashCode() => Move.GetHashCode();

        public override readonly string ToString() => $"Move={Move}";

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
