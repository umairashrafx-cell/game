using UnityEngine;

namespace RoyalVault.Game
{
    public enum HapticStrength
    {
        Selection,   // barely perceptible — lifting a piece
        Light,       // a valid placement
        Warning,     // a rejected move
        Success,     // Royal Match
        Celebration  // level complete / rare reveal
    }

    /// <summary>
    /// Subtle Android haptics, abstracted so gameplay code never touches platform APIs and so
    /// the whole system can be switched off from settings.
    ///
    /// Android's amplitude-controlled vibration only exists on API 26+, so this falls back to a
    /// plain short buzz rather than doing nothing on older devices.
    /// </summary>
    public static class HapticService
    {
        public static bool Enabled = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _vibrator;
        private static bool _supportsAmplitude;
        private static bool _initialised;
#endif

        public static void Play(HapticStrength strength)
        {
            if (!Enabled) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            Initialise();
            if (_vibrator == null) return;

            long milliseconds = DurationFor(strength);
            int amplitude = AmplitudeFor(strength);

            try
            {
                if (_supportsAmplitude)
                {
                    using (AndroidJavaClass effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    {
                        AndroidJavaObject effect = effectClass.CallStatic<AndroidJavaObject>(
                            "createOneShot", milliseconds, amplitude);
                        _vibrator.Call("vibrate", effect);
                    }
                }
                else
                {
                    _vibrator.Call("vibrate", milliseconds);
                }
            }
            catch (System.Exception)
            {
                // A device refusing to vibrate must never interrupt play.
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void Initialise()
        {
            if (_initialised) return;
            _initialised = true;

            try
            {
                using (AndroidJavaClass playerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = playerClass.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }

                using (AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    _supportsAmplitude = version.GetStatic<int>("SDK_INT") >= 26
                                         && _vibrator != null
                                         && _vibrator.Call<bool>("hasAmplitudeControl");
                }
            }
            catch (System.Exception)
            {
                _vibrator = null;
            }
        }

        private static long DurationFor(HapticStrength strength)
        {
            switch (strength)
            {
                case HapticStrength.Selection:   return 8;
                case HapticStrength.Light:       return 12;
                case HapticStrength.Warning:     return 26;
                case HapticStrength.Success:     return 34;
                case HapticStrength.Celebration: return 48;
                default: return 12;
            }
        }

        private static int AmplitudeFor(HapticStrength strength)
        {
            switch (strength)
            {
                case HapticStrength.Selection:   return 42;
                case HapticStrength.Light:       return 70;
                case HapticStrength.Warning:     return 140;
                case HapticStrength.Success:     return 170;
                case HapticStrength.Celebration: return 220;
                default: return 70;
            }
        }
#endif
    }
}
