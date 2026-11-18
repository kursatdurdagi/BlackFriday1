using UnityEngine;

namespace BlackFriday
{
    /// <summary>Oyuncu tercihleri (PlayerPrefs).</summary>
    public static class GameSettings
    {
        public static string PlayerName
        {
            get => PlayerPrefs.GetString("bf_name", "Oyuncu" + Random.Range(10, 99));
            set { PlayerPrefs.SetString("bf_name", string.IsNullOrWhiteSpace(value) ? "Oyuncu" : value.Trim()); PlayerPrefs.Save(); }
        }

        public static int SkinIndex
        {
            get => PlayerPrefs.GetInt("bf_skin", 0);
            set { PlayerPrefs.SetInt("bf_skin", value); PlayerPrefs.Save(); }
        }

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat("bf_vol", 0.8f);
            set { PlayerPrefs.SetFloat("bf_vol", value); AudioListener.volume = value; PlayerPrefs.Save(); }
        }

        public static string LastIP
        {
            get => PlayerPrefs.GetString("bf_ip", "localhost");
            set { PlayerPrefs.SetString("bf_ip", value); PlayerPrefs.Save(); }
        }

        public static float MouseSensitivity
        {
            get => PlayerPrefs.GetFloat("bf_sens", 1f);
            set { PlayerPrefs.SetFloat("bf_sens", Mathf.Clamp(value, 0.1f, 3f)); PlayerPrefs.Save(); }
        }

        public static bool InvertY
        {
            get => PlayerPrefs.GetInt("bf_invy", 0) == 1;
            set { PlayerPrefs.SetInt("bf_invy", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Varsayılan kamera: false = TPS, true = FPS</summary>
        public static bool FirstPerson
        {
            get => PlayerPrefs.GetInt("bf_fps", 0) == 1;
            set { PlayerPrefs.SetInt("bf_fps", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool VSync
        {
            get => PlayerPrefs.GetInt("bf_vsync", 0) == 1;
            set { PlayerPrefs.SetInt("bf_vsync", value ? 1 : 0); ApplyFrameRate(); PlayerPrefs.Save(); }
        }

        public static bool ShowFps
        {
            get => PlayerPrefs.GetInt("bf_showfps", 1) == 1;
            set { PlayerPrefs.SetInt("bf_showfps", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static void ApplyFrameRate()
        {
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : 500;
        }

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt("bf_fs", 1) == 1;
            set { PlayerPrefs.SetInt("bf_fs", value ? 1 : 0); Screen.fullScreen = value; PlayerPrefs.Save(); }
        }

        public static int Quality
        {
            get => PlayerPrefs.GetInt("bf_q", QualitySettings.GetQualityLevel());
            set { PlayerPrefs.SetInt("bf_q", value); QualitySettings.SetQualityLevel(value, true); PlayerPrefs.Save(); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Apply()
        {
            AudioListener.volume = MasterVolume;
            ApplyFrameRate();
            if (PlayerPrefs.HasKey("bf_q")) QualitySettings.SetQualityLevel(Quality, true);
        }
    }
}
