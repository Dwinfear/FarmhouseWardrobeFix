using System;

namespace FarmhouseWardrobeFix
{
    internal static class SceneNames
    {
        internal const string Farmhouse = "FarmHouseA";

        internal static bool IsFarmhouse(string sceneName)
            => Matches(Farmhouse, sceneName);

        internal static bool Matches(string savedScene, string activeScene)
            => !string.IsNullOrWhiteSpace(savedScene) &&
               !string.IsNullOrEmpty(activeScene) &&
               activeScene.StartsWith(savedScene, StringComparison.OrdinalIgnoreCase);

        internal static string Normalize(string sceneName)
            => IsFarmhouse(sceneName) ? Farmhouse : sceneName;
    }
}
