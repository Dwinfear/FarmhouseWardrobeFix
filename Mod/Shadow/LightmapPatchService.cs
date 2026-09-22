using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;

namespace FarmhouseWardrobeFix
{
    internal sealed class LightmapPatchService
    {
        private const string FloorPath =
            "Root/Art/Structure/STR_FarmHouseUpStairsFloorWood_Prefab";
        private const string ExpectedLightmapName = "Lightmap-1_comp_light";
        private const string EmbeddedPatchName =
            "FarmhouseWardrobeFix.Resources.farmhouse_lightmap_regions.fwbc7";
        private const string PatchSha256 =
            "03A6B71465084881E3F4DB23B6BA0D7D4FAEF3DC929E9FB7165C4DDDF8055E7C";
        private const int ExpectedPatchByteCount = 30808;
        private const int ExpectedLightmapSize = 2048;
        private const int ExpectedMipmapCount = 12;
        private const int StartupRetryFrames = 300;
        private const int RetryIntervalFrames = 30;

        private readonly Action<string> error;

        private NativeBc7Patch patchData;
        private Texture2D farmhouseLightmap;
        private bool farmhouseSceneActive;
        private bool startupMonitoring;
        private bool patchPrepared;
        private bool fatalError;
        private int sceneFrame;
        private int lastResidentWidth = -1;

        internal LightmapPatchService(Action<string> error)
        {
            this.error = error ?? throw new ArgumentNullException(nameof(error));
        }

        internal bool RequiresUpdate => startupMonitoring;

        internal void Initialize()
        {
            try
            {
                using Stream resource = typeof(WardrobeFixMod).Assembly
                    .GetManifestResourceStream(EmbeddedPatchName);
                if (resource == null)
                    throw new FileNotFoundException(
                        $"Embedded resource was not found: {EmbeddedPatchName}");

                using var buffer = new MemoryStream();
                resource.CopyTo(buffer);
                byte[] bytes = buffer.ToArray();

                if (bytes.Length != ExpectedPatchByteCount)
                    throw new InvalidDataException(
                        $"Unexpected embedded patch size: {bytes.Length}; " +
                        $"expected {ExpectedPatchByteCount}.");

                string sha256 = Convert.ToHexString(SHA256.HashData(bytes));
                if (!string.Equals(sha256, PatchSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Embedded patch SHA-256 mismatch: {sha256}.");

                patchData = NativeBc7Patch.Parse(bytes);
            }
            catch (Exception ex)
            {
                patchData = null;
                error($"Could not load embedded BC7 shadow patch: {ex}");
            }
        }

        internal void OnSceneInitialized(string sceneName)
        {
            bool continuingFarmhouseLoad =
                farmhouseSceneActive && SceneNames.IsFarmhouse(sceneName);

            sceneFrame = 0;
            farmhouseSceneActive = SceneNames.IsFarmhouse(sceneName);
            startupMonitoring = farmhouseSceneActive;

            if (!continuingFarmhouseLoad)
                ResetSceneState();

            if (farmhouseSceneActive && !patchPrepared)
                TryPreparePatch();
        }

        internal void OnUpdate()
        {
            if (!startupMonitoring || fatalError)
                return;

            sceneFrame++;

            if (!patchPrepared)
            {
                if (ShouldRetry())
                    TryPreparePatch();
                if (sceneFrame >= StartupRetryFrames)
                    startupMonitoring = false;
                return;
            }

            if (lastResidentWidth < ExpectedLightmapSize && ShouldRetry())
                TryApplyPatch();

            if (lastResidentWidth >= ExpectedLightmapSize || sceneFrame >= StartupRetryFrames)
                startupMonitoring = false;
        }

        private bool ShouldRetry()
            => sceneFrame <= StartupRetryFrames && sceneFrame % RetryIntervalFrames == 0;

        private void ResetSceneState()
        {
            farmhouseLightmap = null;
            patchPrepared = false;
            fatalError = false;
            lastResidentWidth = -1;
        }

        private void TryPreparePatch()
        {
            if (patchPrepared || fatalError)
                return;

            if (patchData == null)
            {
                Stop("Wardrobe shadow fix disabled because the embedded BC7 patch is unavailable.");
                return;
            }

            GameObject floor = GameObject.Find(FloorPath);
            if (floor == null)
                return;

            Renderer renderer = floor.GetComponent<Renderer>();
            if (renderer == null)
            {
                Stop("Floor Renderer was not found.");
                return;
            }

            int lightmapIndex = renderer.lightmapIndex;
            var lightmaps = LightmapSettings.lightmaps;
            if (lightmaps == null || lightmapIndex < 0 || lightmapIndex >= lightmaps.Length)
                return;

            Texture2D source = lightmaps[lightmapIndex].lightmapColor;
            if (source == null)
                return;

            if (!IsExpectedLightmap(lightmapIndex, source))
            {
                Stop(
                    $"Lightmap safety check failed: index={lightmapIndex}, name={source.name}, " +
                    $"size={source.width}x{source.height}, format={source.format}, " +
                    $"mips={source.mipmapCount}, sRGB={source.isDataSRGB}.");
                return;
            }

            farmhouseLightmap = source;
            patchPrepared = true;
            TryApplyPatch();
        }

        private static bool IsExpectedLightmap(int lightmapIndex, Texture2D source)
            => lightmapIndex == 0 &&
               source.width == ExpectedLightmapSize &&
               source.height == ExpectedLightmapSize &&
               source.format == TextureFormat.BC7 &&
               source.mipmapCount == ExpectedMipmapCount &&
               source.isDataSRGB &&
               string.Equals(source.name, ExpectedLightmapName, StringComparison.Ordinal);

        private void TryApplyPatch()
        {
            if (!patchPrepared || farmhouseLightmap == null || patchData == null)
                return;

            try
            {
                lastResidentWidth = D3D11Bc7RegionWriter.WritePatched(
                    farmhouseLightmap,
                    patchData.Regions);
            }
            catch (Exception ex)
            {
                Stop($"D3D11 region update failed: {ex}");
            }
        }

        private void Stop(string reason)
        {
            fatalError = true;
            startupMonitoring = false;
            error($"Wardrobe shadow fix stopped: {reason}");
        }
    }
}
