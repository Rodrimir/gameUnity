using System.Collections.Generic;
using GameYunit.Core.Rendering;
using GameYunit.Enemies;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace GameYunit.Tests
{
    public sealed class MapAndRenderingTests
    {
        private readonly List<Object> _created = new List<Object>();
        private NavMeshDataInstance _navMesh;

        [TearDown]
        public void TearDown()
        {
            if (_navMesh.valid)
            {
                NavMesh.RemoveNavMeshData(_navMesh);
            }

            foreach (Object created in _created)
            {
                if (created is Camera camera)
                {
                    CameraViewportMapping.Clear(camera);
                }

                if (created != null)
                {
                    Object.DestroyImmediate(created is Component component ? component.gameObject : created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void MouseMapsFromLetterboxedScreenToRenderTexturePixels()
        {
            var renderTexture = new RenderTexture(320, 240, 24);
            _created.Add(renderTexture);
            Camera camera = CreateCamera();
            camera.targetTexture = renderTexture;

            // Tela 1000x600 com a imagem 4:3 centralizada: 800x600 a partir de x = 100.
            CameraViewportMapping.SetScreenRect(camera, new Rect(100f, 0f, 800f, 600f));

            Assert.That(CameraViewportMapping.TryScreenToCameraPixel(camera, new Vector2(500f, 300f), out Vector2 center), Is.True);
            Assert.That(center.x, Is.EqualTo(160f).Within(0.01f));
            Assert.That(center.y, Is.EqualTo(120f).Within(0.01f));

            Assert.That(CameraViewportMapping.TryScreenToCameraPixel(camera, new Vector2(100f, 0f), out Vector2 corner), Is.True);
            Assert.That(corner, Is.EqualTo(Vector2.zero));

            // Faixa do letterbox: fora da imagem da câmera.
            Assert.That(CameraViewportMapping.TryScreenToCameraPixel(camera, new Vector2(50f, 300f), out _), Is.False);
        }

        [Test]
        public void CameraWithoutRenderTextureUsesScreenCoordinates()
        {
            Camera camera = CreateCamera();
            camera.pixelRect = new Rect(0f, 0f, 800f, 600f);

            Assert.That(CameraViewportMapping.TryScreenToCameraPixel(camera, new Vector2(10f, 20f), out Vector2 pixel), Is.True);
            Assert.That(pixel, Is.EqualTo(new Vector2(10f, 20f)));
            Assert.That(CameraViewportMapping.TryScreenToCameraPixel(null, Vector2.zero, out _), Is.False);
        }

        [Test]
        public void NavMeshSteeringGoesAroundStaticWall()
        {
            CreateBox("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(30f, 1f, 30f));
            CreateBox("Wall", new Vector3(0f, 1f, 0f), new Vector3(1f, 2f, 6f));
            BuildNavMesh();

            NavMeshSteering steering = new GameObject("Steering").AddComponent<NavMeshSteering>();
            _created.Add(steering);
            var from = new Vector3(-5f, 0f, 0f);
            var to = new Vector3(5f, 0f, 0f);

            Vector3 point = steering.GetSteeringPoint(from, to);
            Assert.That(steering.HasPath, Is.True);

            // A parede vai de z = -3 a 3; o primeiro canto tem que contorná-la, não atravessá-la.
            Assert.That(Mathf.Abs(point.z), Is.GreaterThan(3f));
            Assert.That(point.x, Is.LessThan(1f));
        }

        [Test]
        public void NavMeshSteeringFallsBackToStraightLineWithoutNavMesh()
        {
            NavMeshSteering steering = new GameObject("Steering").AddComponent<NavMeshSteering>();
            _created.Add(steering);
            var destination = new Vector3(5f, 0f, 3f);

            Assert.That(steering.GetSteeringPoint(Vector3.zero, destination), Is.EqualTo(destination));
            Assert.That(steering.HasPath, Is.False);
        }

        private Camera CreateCamera()
        {
            Camera camera = new GameObject("Mapping camera").AddComponent<Camera>();
            _created.Add(camera);
            return camera;
        }

        private void CreateBox(string name, Vector3 position, Vector3 scale)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
            _created.Add(box);
        }

        private void BuildNavMesh()
        {
            Physics.SyncTransforms();
            var bounds = new Bounds(Vector3.zero, new Vector3(40f, 10f, 40f));
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);
            NavMeshData data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources, bounds,
                Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null);
            _created.Add(data);
            _navMesh = NavMesh.AddNavMeshData(data);
        }
    }
}
