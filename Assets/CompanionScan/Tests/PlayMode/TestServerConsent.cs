using System;
using CompanionScan.Core;
using UnityEngine;

namespace CompanionScan.Tests.PlayMode
{
    /// <summary>
    /// <see cref="CVServerClient"/> sends no image without the camera agreement
    /// (<see cref="CaptureConsent"/>). The live-server tests send fixture images, so they record the
    /// agreement while they run, then put back whatever this machine had recorded before.
    /// </summary>
    internal sealed class TestServerConsent : IDisposable
    {
        private static readonly string[] StringKeys =
        {
            CaptureConsent.PlayerPrefsAgreedAtKey,
            CaptureConsent.PlayerPrefsServerUrlKey
        };

        private readonly bool _hadVersion;
        private readonly int _version;
        private readonly (bool had, string value)[] _strings = new (bool, string)[StringKeys.Length];
        private bool _restored;

        private TestServerConsent()
        {
            _hadVersion = PlayerPrefs.HasKey(CaptureConsent.PlayerPrefsVersionKey);
            _version = PlayerPrefs.GetInt(CaptureConsent.PlayerPrefsVersionKey, 0);
            for (int i = 0; i < StringKeys.Length; i++)
            {
                _strings[i] = (PlayerPrefs.HasKey(StringKeys[i]), PlayerPrefs.GetString(StringKeys[i], string.Empty));
            }
        }

        /// <summary>Records the agreement for the server <paramref name="config"/> sends to.</summary>
        public static TestServerConsent Grant(ServerConfig config)
        {
            var consent = new TestServerConsent();
            CaptureConsent.Grant(config.ResolveBaseUrl());
            return consent;
        }

        /// <summary>Puts back what was recorded before <see cref="Grant"/>.</summary>
        public void Dispose()
        {
            if (_restored)
            {
                return;
            }

            _restored = true;
            if (_hadVersion)
            {
                PlayerPrefs.SetInt(CaptureConsent.PlayerPrefsVersionKey, _version);
            }
            else
            {
                PlayerPrefs.DeleteKey(CaptureConsent.PlayerPrefsVersionKey);
            }

            for (int i = 0; i < StringKeys.Length; i++)
            {
                if (_strings[i].had)
                {
                    PlayerPrefs.SetString(StringKeys[i], _strings[i].value);
                }
                else
                {
                    PlayerPrefs.DeleteKey(StringKeys[i]);
                }
            }

            PlayerPrefs.Save();
        }
    }
}
