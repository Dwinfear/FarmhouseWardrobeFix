using System;
using ModSettings;
using UnityEngine;

namespace FarmhouseWardrobeFix
{
    internal sealed class WardrobeFixSettings : ModSettingsBase
    {
        private readonly Action<KeyCode> onConfirmed;

        [Section("Controls")]
        [Name("Remove aimed object")]
        [Description("Permanently hides the aimed object and saves it to FarmhouseWardrobeFix.json.")]
        public KeyCode RemovalKey = KeyCode.F4;

        internal WardrobeFixSettings(KeyCode removalKey, Action<KeyCode> onConfirmed)
        {
            RemovalKey = removalKey;
            this.onConfirmed = onConfirmed ?? throw new ArgumentNullException(nameof(onConfirmed));
        }

        protected override void OnConfirm()
        {
            onConfirmed(RemovalKey);
        }
    }
}
