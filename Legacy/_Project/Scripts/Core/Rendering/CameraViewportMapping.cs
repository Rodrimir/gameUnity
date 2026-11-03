using System.Collections.Generic;
using UnityEngine;

namespace GameYunit.Core.Rendering
{
    /// <summary>
    /// Converte posições de tela (mouse) em pixels de uma câmera que renderiza para uma RenderTexture
    /// exibida em parte da tela (ex.: tela PSX 320x240 com letterbox).
    /// </summary>
    /// <remarks>
    /// Quem exibe a RenderTexture registra o retângulo ocupado na tela. Câmeras sem RenderTexture usam a
    /// conversão direta, então o código que consulta esta classe funciona com ou sem o efeito PSX.
    /// </remarks>
    public static class CameraViewportMapping
    {
        private static readonly Dictionary<Camera, Rect> ScreenRects = new Dictionary<Camera, Rect>();

        /// <summary>Registra o retângulo, em pixels de tela, onde a imagem da câmera está sendo exibida.</summary>
        public static void SetScreenRect(Camera camera, Rect screenRect)
        {
            if (camera != null)
            {
                ScreenRects[camera] = screenRect;
            }
        }

        public static void Clear(Camera camera)
        {
            if (!ReferenceEquals(camera, null))
            {
                ScreenRects.Remove(camera);
            }
        }

        /// <summary>
        /// Converte <paramref name="screenPosition"/> para pixels da câmera (o espaço de ScreenPointToRay).
        /// </summary>
        /// <returns>False se o ponto estiver fora da área exibida pela câmera.</returns>
        public static bool TryScreenToCameraPixel(Camera camera, Vector2 screenPosition, out Vector2 cameraPixel)
        {
            cameraPixel = default;
            if (camera == null)
            {
                return false;
            }

            if (camera.targetTexture == null)
            {
                cameraPixel = screenPosition;
                return camera.pixelRect.Contains(screenPosition);
            }

            // Sem registro, assume a RenderTexture esticada na tela inteira.
            Rect displayed = ScreenRects.TryGetValue(camera, out Rect rect)
                ? rect
                : new Rect(0f, 0f, Screen.width, Screen.height);
            if (displayed.width <= 0f || displayed.height <= 0f || !displayed.Contains(screenPosition))
            {
                return false;
            }

            Vector2 normalized = (screenPosition - displayed.position) / displayed.size;
            Rect pixels = camera.pixelRect;
            cameraPixel = pixels.position + Vector2.Scale(normalized, pixels.size);
            return true;
        }

        public static bool TryCameraPixelToScreen(Camera camera, Vector2 cameraPixel, out Vector2 screenPosition)
        {
            screenPosition = default;
            if (camera == null) return false;
            if (camera.targetTexture == null)
            {
                screenPosition = cameraPixel;
                return camera.pixelRect.Contains(cameraPixel);
            }

            if (!ScreenRects.TryGetValue(camera, out Rect displayed))
                displayed = new Rect(0f, 0f, Screen.width, Screen.height);
            Rect pixels = camera.pixelRect;
            if (displayed.width <= 0f || displayed.height <= 0f || pixels.width <= 0f || pixels.height <= 0f)
                return false;

            Vector2 normalized = (cameraPixel - pixels.position) / pixels.size;
            screenPosition = displayed.position + Vector2.Scale(normalized, displayed.size);
            return displayed.Contains(screenPosition);
        }

        public static bool TryGetScreenRect(Camera camera, out Rect screenRect)
        {
            screenRect = default;
            if (camera == null) return false;
            if (camera.targetTexture == null)
            {
                screenRect = camera.pixelRect;
                return true;
            }

            return ScreenRects.TryGetValue(camera, out screenRect);
        }

        // Com "Enter Play Mode Options" (sem domain reload) estáticos sobrevivem entre sessões de Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ScreenRects.Clear();
    }
}
