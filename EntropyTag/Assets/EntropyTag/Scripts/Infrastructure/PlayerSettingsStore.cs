using System;
using EntropyTag.UnityAdapters;
using UnityEngine;

namespace EntropyTag.Infrastructure
{
    public sealed class PlayerSettingsStore
    {
        public const string PreferencesKey = "EntropyTag.PlayerInputSettings.v1";

        public PlayerInputSettings Load()
        {
            string json = PlayerPrefs.GetString(PreferencesKey, string.Empty);

            if (string.IsNullOrWhiteSpace(json))
            {
                return PlayerInputSettings.CreateDefault();
            }

            try
            {
                PlayerInputSettings settings = JsonUtility.FromJson<PlayerInputSettings>(json);

                if (settings == null)
                {
                    throw new InvalidOperationException("Saved player input settings were empty.");
                }

                settings.Normalize();
                return settings;
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException("Saved player input settings are invalid JSON.", exception);
            }
        }

        public void Save(PlayerInputSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.Normalize();
            PlayerPrefs.SetString(PreferencesKey, JsonUtility.ToJson(settings));
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(PreferencesKey);
            PlayerPrefs.Save();
        }
    }
}
