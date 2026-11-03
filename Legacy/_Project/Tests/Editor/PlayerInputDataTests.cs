using GameYunit.Player;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace GameYunit.Tests
{
    public sealed class PlayerInputDataTests
    {
        [Test]
        public void InvalidAimPreservesValidMovement()
        {
            var input = new PlayerInputData(new Vector2(0.25f, 0.5f), new Vector2(float.NaN, 0f));
            PlayerInputData sanitized = input.Sanitized();

            Assert.That(sanitized.Move, Is.EqualTo(input.Move));
            Assert.That(sanitized.Aim, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void InvalidMovementPreservesValidAim()
        {
            var input = new PlayerInputData(new Vector2(float.PositiveInfinity, 0f), Vector2.left);
            PlayerInputData sanitized = input.Sanitized();

            Assert.That(sanitized.Move, Is.EqualTo(Vector2.zero));
            Assert.That(sanitized.Aim, Is.EqualTo(Vector2.left));
        }

        [Test]
        public void BothIntentionsHaveBoundedMagnitude()
        {
            var input = new PlayerInputData(new Vector2(3f, 4f), new Vector2(4f, -3f)).Sanitized();

            Assert.That(input.Move.magnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(input.Aim.magnitude, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void ChangingOnlyAimChangesSnapshotEquality()
        {
            var first = new PlayerInputData(Vector2.up, Vector2.left);
            var second = new PlayerInputData(Vector2.up, Vector2.right);

            Assert.That(first.Equals(second), Is.False);
        }

        [Test]
        public void NetworkSerializationPreservesMovementAndAim()
        {
            var original = new PlayerInputData(new Vector2(0.25f, -0.5f), Vector2.right);
            using (var writer = new FastBufferWriter(64, Allocator.Temp))
            {
                writer.WriteNetworkSerializable(original);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadNetworkSerializable(out PlayerInputData received);
                    Assert.That(received, Is.EqualTo(original));
                }
            }
        }
    }
}
