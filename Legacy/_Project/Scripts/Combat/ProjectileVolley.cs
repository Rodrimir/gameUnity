using Unity.Netcode;
using UnityEngine;

namespace GameYunit.Combat
{
    /// <summary>
    /// Um disparo completo (1 a N projéteis em leque), enviado do servidor aos clients em um único RPC.
    /// Com estes dados cada client reconstrói as mesmas trajetórias sem replicação por frame.
    /// </summary>
    public struct ProjectileVolley : INetworkSerializable
    {
        /// <summary>Id do primeiro projétil; os demais usam ids consecutivos (FirstId + i).</summary>
        public uint FirstId;

        /// <summary>Índice da arma no AutoAttackSystem; usado pelos clients para escolher o visual.</summary>
        public byte WeaponIndex;

        public byte Count;

        /// <summary>Abertura total do leque, em graus.</summary>
        public float SpreadAngle;

        public Vector3 Origin;

        /// <summary>Direção unitária central do leque, em world space.</summary>
        public Vector3 Direction;

        public float Speed;

        public float Lifetime;

        /// <summary>Curva do teleguiado em graus/s; zero = reto.</summary>
        public float HomingTurnRate;

        /// <summary>NetworkObjectId do alvo; válido apenas se <see cref="HasTarget"/>.</summary>
        public ulong TargetId;

        public bool HasTarget;

        /// <summary>NetworkManager.ServerTime.Time no momento do disparo.</summary>
        public double FireTime;

        /// <summary>Direção do projétil <paramref name="index"/>, distribuída igualmente no leque.</summary>
        public readonly Vector3 GetDirection(int index)
        {
            if (Count <= 1 || SpreadAngle <= 0f)
            {
                return Direction;
            }

            float angle = -SpreadAngle * 0.5f + SpreadAngle * index / (Count - 1);
            return Quaternion.AngleAxis(angle, Vector3.up) * Direction;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref FirstId);
            serializer.SerializeValue(ref WeaponIndex);
            serializer.SerializeValue(ref Count);
            serializer.SerializeValue(ref SpreadAngle);
            serializer.SerializeValue(ref Origin);
            serializer.SerializeValue(ref Direction);
            serializer.SerializeValue(ref Speed);
            serializer.SerializeValue(ref Lifetime);
            serializer.SerializeValue(ref HomingTurnRate);
            serializer.SerializeValue(ref HasTarget);
            if (HasTarget)
            {
                serializer.SerializeValue(ref TargetId);
            }

            serializer.SerializeValue(ref FireTime);
        }
    }
}
