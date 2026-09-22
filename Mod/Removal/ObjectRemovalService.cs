using System;
using UnityEngine;

namespace FarmhouseWardrobeFix
{
    internal sealed class ObjectRemovalService
    {
        private const string WardrobePath =
            "RootSandbox/Art/Props_Sandbox_Ver/OBJ_DresserTallDrawerD";
        private const string WardrobeName = "OBJ_DresserTallDrawerD";
        private const float WardrobePositionX = 5.232f;
        private const float WardrobePositionY = 3.614f;
        private const float WardrobePositionZ = -2.255f;
        private const float PositionTolerance = 0.25f;

        private readonly FixConfig config;
        private readonly Action saveConfig;
        private readonly Action<string> warning;
        private readonly AimedObjectResolver aimedObjectResolver;

        internal ObjectRemovalService(
            FixConfig config,
            Action saveConfig,
            Action<string> warning)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.saveConfig = saveConfig ?? throw new ArgumentNullException(nameof(saveConfig));
            this.warning = warning ?? throw new ArgumentNullException(nameof(warning));
            aimedObjectResolver = new AimedObjectResolver(warning);
        }

        internal void ApplyForScene(string sceneName)
        {
            bool farmhouseScene = SceneNames.IsFarmhouse(sceneName);
            if (farmhouseScene)
                HideBuiltInWardrobe();

            if (!HasConfiguredRemovals(sceneName))
                return;

            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            if (transforms == null || transforms.Length == 0)
                return;

            ApplyConfiguredRemovals(transforms, sceneName);
        }

        internal void RemoveAimedObject(string sceneName, KeyCode removalKey)
        {
            string keyLabel = AimedObjectResolver.GetKeyLabel(removalKey);
            if (string.IsNullOrEmpty(sceneName))
            {
                warning($"{keyLabel} ignored: no gameplay scene is active.");
                return;
            }

            if (!aimedObjectResolver.TryResolve(removalKey, out Transform selected, out string path))
                return;

            string sceneKey = SceneNames.Normalize(sceneName);
            Vector3 position = selected.position;
            if (FindRemoval(sceneKey, path, position) == null)
            {
                config.RemovedObjects.Add(new RemovedObjectEntry
                {
                    Scene = sceneKey,
                    Path = path,
                    Name = selected.name,
                    PositionX = position.x,
                    PositionY = position.y,
                    PositionZ = position.z
                });
                saveConfig();
            }

            selected.gameObject.SetActive(false);
        }

        private bool HasConfiguredRemovals(string sceneName)
        {
            foreach (RemovedObjectEntry entry in config.RemovedObjects)
            {
                if (entry != null && !string.IsNullOrWhiteSpace(entry.Scene) &&
                    SceneNames.Matches(entry.Scene, sceneName))
                    return true;
            }
            return false;
        }

        private void ApplyConfiguredRemovals(Transform[] transforms, string sceneName)
        {
            foreach (RemovedObjectEntry entry in config.RemovedObjects)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Scene) ||
                    string.IsNullOrWhiteSpace(entry.Path) ||
                    !SceneNames.Matches(entry.Scene, sceneName))
                    continue;

                HideMatchingActiveInstances(
                    transforms,
                    entry.Path,
                    entry.Name,
                    entry.PositionX,
                    entry.PositionY,
                    entry.PositionZ);
            }
        }

        private static void HideBuiltInWardrobe()
        {
            GameObject wardrobe = GameObject.Find(WardrobePath);
            if (wardrobe == null || !wardrobe.activeInHierarchy ||
                !string.Equals(wardrobe.name, WardrobeName, StringComparison.Ordinal))
                return;

            Vector3 position = wardrobe.transform.position;
            float dx = position.x - WardrobePositionX;
            float dy = position.y - WardrobePositionY;
            float dz = position.z - WardrobePositionZ;
            float toleranceSquared = PositionTolerance * PositionTolerance;
            if (dx * dx + dy * dy + dz * dz <= toleranceSquared)
                wardrobe.SetActive(false);
        }

        private static void HideMatchingActiveInstances(
            Transform[] transforms,
            string path,
            string expectedName,
            float positionX,
            float positionY,
            float positionZ)
        {
            float toleranceSquared = PositionTolerance * PositionTolerance;

            foreach (Transform transform in transforms)
            {
                if (transform == null || transform.gameObject == null ||
                    !transform.gameObject.activeInHierarchy)
                    continue;

                if (!string.IsNullOrWhiteSpace(expectedName) &&
                    !string.Equals(transform.name, expectedName, StringComparison.Ordinal))
                    continue;

                Vector3 position = transform.position;
                float dx = position.x - positionX;
                float dy = position.y - positionY;
                float dz = position.z - positionZ;
                if (dx * dx + dy * dy + dz * dz > toleranceSquared)
                    continue;

                if (!string.Equals(TransformPath.Get(transform), path, StringComparison.Ordinal))
                    continue;

                transform.gameObject.SetActive(false);
            }
        }

        private RemovedObjectEntry FindRemoval(string scene, string path, Vector3 position)
        {
            float toleranceSquared = PositionTolerance * PositionTolerance;
            foreach (RemovedObjectEntry entry in config.RemovedObjects)
            {
                if (entry == null ||
                    !string.Equals(entry.Scene, scene, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase))
                    continue;

                float dx = entry.PositionX - position.x;
                float dy = entry.PositionY - position.y;
                float dz = entry.PositionZ - position.z;
                if (dx * dx + dy * dy + dz * dz <= toleranceSquared)
                    return entry;
            }
            return null;
        }

    }
}
