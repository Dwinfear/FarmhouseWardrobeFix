using System;
using System.IO;
using MelonLoader;
using ModSettings;
using UnityEngine;

namespace FarmhouseWardrobeFix
{
    public sealed class WardrobeFixMod : MelonMod
    {
        private string currentScene = string.Empty;
        private KeyCode removalKey = KeyCode.F4;

        private FixConfig config;
        private ConfigStore configStore;
        private ObjectRemovalService removalService;
        private LightmapPatchService lightmapPatchService;
        private WardrobeFixSettings settings;

        public override void OnInitializeMelon()
        {
            string configPath = Path.Combine(
                AppContext.BaseDirectory,
                "Mods",
                "FarmhouseWardrobeFix.json");

            configStore = new ConfigStore(
                configPath,
                message => LoggerInstance.Warning(message));
            config = configStore.Load(out removalKey);

            removalService = new ObjectRemovalService(
                config,
                SaveConfig,
                message => LoggerInstance.Warning(message));

            lightmapPatchService = new LightmapPatchService(
                message => LoggerInstance.Error(message));
            lightmapPatchService.Initialize();

            settings = new WardrobeFixSettings(removalKey, ApplyRemovalKeySetting);
            settings.AddToModSettings("Farmhouse Wardrobe Fix", MenuType.Both);
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            currentScene = sceneName ?? string.Empty;
            removalService.ApplyForScene(currentScene);
            lightmapPatchService.OnSceneInitialized(currentScene);
        }

        public override void OnUpdate()
        {
            if (removalKey != KeyCode.None && Input.GetKeyDown(removalKey))
                removalService.RemoveAimedObject(currentScene, removalKey);

            if (lightmapPatchService.RequiresUpdate)
                lightmapPatchService.OnUpdate();
        }

        private void ApplyRemovalKeySetting(KeyCode selectedKey)
        {
            removalKey = selectedKey;
            config.RemovalKey = selectedKey.ToString();
            SaveConfig();
        }

        private void SaveConfig()
        {
            configStore.Save(config);
        }
    }
}
