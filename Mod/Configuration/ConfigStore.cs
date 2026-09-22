using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UnityEngine;

namespace FarmhouseWardrobeFix
{
    internal sealed class ConfigStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private readonly string path;
        private readonly Action<string> warning;

        internal ConfigStore(string path, Action<string> warning)
        {
            this.path = path ?? throw new ArgumentNullException(nameof(path));
            this.warning = warning ?? throw new ArgumentNullException(nameof(warning));
        }

        internal FixConfig Load(out KeyCode removalKey)
        {
            bool mustSave = false;
            FixConfig config;

            try
            {
                if (File.Exists(path))
                {
                    config = JsonSerializer.Deserialize<FixConfig>(
                        File.ReadAllText(path),
                        JsonOptions);
                    if (config == null)
                    {
                        config = new FixConfig();
                        mustSave = true;
                    }
                }
                else
                {
                    config = new FixConfig();
                    mustSave = true;
                }
            }
            catch (Exception ex)
            {
                config = new FixConfig();
                mustSave = true;
                warning($"Could not read config; using defaults: {ex.Message}");
            }

            if (config.RemovedObjects == null)
            {
                config.RemovedObjects = new List<RemovedObjectEntry>();
                mustSave = true;
            }

            if (!TryParseKey(config.RemovalKey, out removalKey))
            {
                removalKey = KeyCode.F4;
                config.RemovalKey = removalKey.ToString();
                mustSave = true;
                warning("Invalid RemovalKey in config; reset to F4.");
            }
            else
            {
                string normalizedKey = removalKey.ToString();
                if (!string.Equals(config.RemovalKey, normalizedKey, StringComparison.Ordinal))
                {
                    config.RemovalKey = normalizedKey;
                    mustSave = true;
                }
            }

            if (mustSave)
                Save(config);

            return config;
        }

        internal void Save(FixConfig config)
        {
            try
            {
                string json = JsonSerializer.Serialize(config, JsonOptions);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                warning($"Could not save config: {ex.Message}");
            }
        }

        private static bool TryParseKey(string value, out KeyCode key)
        {
            key = KeyCode.F4;
            return !string.IsNullOrWhiteSpace(value) &&
                   Enum.TryParse(value, true, out key) &&
                   Enum.IsDefined(typeof(KeyCode), key);
        }
    }
}
