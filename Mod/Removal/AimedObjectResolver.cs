using System;
using Il2Cpp;
using UnityEngine;

namespace FarmhouseWardrobeFix
{
    internal sealed class AimedObjectResolver
    {
        private const float RaycastDistance = 20f;

        private readonly Action<string> warning;

        internal AimedObjectResolver(Action<string> warning)
        {
            this.warning = warning ?? throw new ArgumentNullException(nameof(warning));
        }

        internal bool TryResolve(
            KeyCode removalKey,
            out Transform selected,
            out string path)
        {
            selected = null;
            path = string.Empty;
            string keyLabel = GetKeyLabel(removalKey);

            Camera camera = FindGameplayCamera();
            if (camera == null)
            {
                warning($"{keyLabel}: no active gameplay camera found.");
                return false;
            }

            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    RaycastDistance,
                    ~0,
                    QueryTriggerInteraction.Collide) ||
                hit.collider == null || hit.collider.gameObject == null)
            {
                warning($"{keyLabel}: nothing removable was hit within {RaycastDistance:F0} metres.");
                return false;
            }

            selected = SelectRemovalRoot(hit.collider.transform);
            if (selected == null || selected.gameObject == null)
            {
                warning($"{keyLabel} could not resolve a removable object from the raycast hit.");
                return false;
            }

            path = TransformPath.Get(selected);
            if (!IsProtectedTarget(selected, path))
                return true;

            warning(
                $"{keyLabel} safety check refused to hide structural/system object: {path}. " +
                "Aim at a separate prop or furniture object.");
            selected = null;
            path = string.Empty;
            return false;
        }

        internal static string GetKeyLabel(KeyCode key)
            => key == KeyCode.None ? "Removal hotkey" : key.ToString();

        private static Transform SelectRemovalRoot(Transform hit)
        {
            Transform candidate = hit;
            for (int i = 0; i < 3 && candidate != null && candidate.parent != null; i++)
            {
                string currentName = candidate.name ?? string.Empty;
                string parentName = candidate.parent.name ?? string.Empty;
                if (LooksLikeRenderLeaf(currentName) || LooksLikeObjectContainer(parentName))
                {
                    candidate = candidate.parent;
                    continue;
                }
                break;
            }
            return candidate;
        }

        private static bool LooksLikeRenderLeaf(string name)
            => name.IndexOf("_LOD", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.Equals("PickupHelper", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("Collider", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("_COL", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("_Mesh", StringComparison.OrdinalIgnoreCase);

        private static bool LooksLikeObjectContainer(string name)
            => name.IndexOf("_Prefab", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("(PLACED)", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.EndsWith("(Clone)", StringComparison.OrdinalIgnoreCase);

        private static bool IsProtectedTarget(Transform target, string path)
        {
            string name = target.name ?? string.Empty;
            if (target.parent == null ||
                string.Equals(name, "Root", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "RootSandbox", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Art", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Structure", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "DesignPlaceable", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("STR_", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("SCRIPT_", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("CHARACTER_", StringComparison.OrdinalIgnoreCase))
                return true;

            return path.IndexOf("CHARACTER_FPSPlayer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("SCRIPT_EngineSystems", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("SCRIPT_EnvironmentSystems", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Camera FindGameplayCamera()
        {
            Camera gameCamera = GameManager.GetMainCamera();
            if (gameCamera != null && gameCamera.gameObject != null &&
                gameCamera.gameObject.activeInHierarchy)
                return gameCamera;

            Camera best = null;
            long bestScore = long.MinValue;
            Camera[] cameras = Camera.allCameras;
            if (cameras == null)
                return null;

            foreach (Camera camera in cameras)
            {
                long score = ScoreCamera(camera);
                if (score > bestScore)
                {
                    best = camera;
                    bestScore = score;
                }
            }
            return best;
        }

        private static long ScoreCamera(Camera camera)
        {
            if (camera == null || !camera.enabled || camera.gameObject == null ||
                !camera.gameObject.activeInHierarchy || camera.targetTexture != null)
                return long.MinValue;

            long score = (long)Math.Max(1, camera.pixelWidth) * Math.Max(1, camera.pixelHeight);
            if (camera.cameraType == CameraType.Game)
                score += 10_000_000_000L;
            if (camera.cullingMask != 0)
                score += 1_000_000_000L;
            return score;
        }
    }
}
