using GameYunit.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameYunit.Tests
{
    public sealed class PlayerAimInputTests : InputTestFixture
    {
        private GameObject _player;
        private Camera _camera;
        private PlayerAimInput _aim;

        public override void Setup()
        {
            base.Setup();
            _player = new GameObject("Aim input test player");
            _aim = _player.AddComponent<PlayerAimInput>();
            _camera = new GameObject("Aim input test camera").AddComponent<Camera>();
            _camera.pixelRect = new Rect(0f, 0f, 800f, 600f);
            _camera.transform.SetPositionAndRotation(new Vector3(0f, 10f, -10f), Quaternion.Euler(45f, 0f, 0f));

            // Configura o campo do Inspector sem depender das câmeras de outras cenas.
            var serialized = new SerializedObject(_aim);
            serialized.FindProperty("_aimCamera").objectReferenceValue = _camera;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _aim.EnableInput();
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(_player);
            Object.DestroyImmediate(_camera.gameObject);
            base.TearDown();
        }

        [Test]
        public void RightStickAimsRelativeToCamera()
        {
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            _camera.transform.rotation = Quaternion.Euler(45f, 90f, 0f);
            Set(pad.rightStick, Vector2.up);

            AssertDirection(_aim.ReadAim(), Vector2.right);
        }

        [Test]
        public void LeftStickAndRightStickDriftDoNotAim()
        {
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            Set(pad.leftStick, Vector2.one);
            Set(pad.rightStick, new Vector2(0.01f, 0f));

            Assert.That(_aim.ReadAim(), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void MouseProjectsOntoPlaneAtCharacterHeight()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Vector3 point = new Vector3(3f, 1f, 0f);
            Set(mouse.position, (Vector2)_camera.WorldToScreenPoint(point));

            AssertDirection(_aim.ReadAim(), Vector2.right);
        }

        [Test]
        public void StationaryMouseDoesNotOverrideReleasedStick()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            Set(mouse.position, (Vector2)_camera.WorldToScreenPoint(new Vector3(-3f, 1f, 0f)));
            Set(pad.rightStick, Vector2.up);
            AssertDirection(_aim.ReadAim(), Vector2.up);

            Set(pad.rightStick, Vector2.zero);
            Assert.That(_aim.ReadAim(), Is.EqualTo(Vector2.zero));

            Set(mouse.position, (Vector2)_camera.WorldToScreenPoint(new Vector3(-4f, 1f, 0f)));
            AssertDirection(_aim.ReadAim(), Vector2.left);
        }

        [Test]
        public void MouseOutsideViewportDoesNotAim()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Set(mouse.position, new Vector2(-10f, -10f));

            Assert.That(_aim.ReadAim(), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void DisabledInputDoesNotReadDevices()
        {
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            Set(pad.rightStick, Vector2.right);
            _aim.DisableInput();

            Assert.That(_aim.ReadAim(), Is.EqualTo(Vector2.zero));
        }

        private static void AssertDirection(Vector2 actual, Vector2 expected) =>
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(0.001f));
    }
}
