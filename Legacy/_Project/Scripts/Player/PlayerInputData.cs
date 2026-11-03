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

        /// <summary>Direção de mira no mundo (x = X, y = Z). Zero preserva a orientação atual.</summary>
        public Vector2 Aim;

        public PlayerInputData(Vector2 move, Vector2 aim = default)
        {
            Move = move;
            Aim = aim;
        }

        /// <summary>
        /// Versão segura para uso no servidor: descarta NaN/Infinity e limita a magnitude a 1.
        /// Nunca confie em dados vindos do client sem passar por aqui.
        /// </summary>
        public readonly PlayerInputData Sanitized()
        {
            // Validação independente: uma mira inválida não cancela um movimento válido.
            return new PlayerInputData(SanitizeVector(Move), SanitizeVector(Aim));
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Move);
            serializer.SerializeValue(ref Aim);
        }

        public readonly bool Equals(PlayerInputData other) => Move.Equals(other.Move) && Aim.Equals(other.Aim);

        public override readonly bool Equals(object obj) => obj is PlayerInputData other && Equals(other);

        public override readonly int GetHashCode() => unchecked((Move.GetHashCode() * 397) ^ Aim.GetHashCode());

        public override readonly string ToString() => $"Move={Move}, Aim={Aim}";

        private static Vector2 SanitizeVector(Vector2 value) => IsFinite(value.x) && IsFinite(value.y)
            ? Vector2.ClampMagnitude(value, 1f)
            : Vector2.zero;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
