using UnityEngine;

namespace PokerDefense.UI
{
    /**
     * AudioPreferences
     *
     * 씬 믹스 음량 위에 곱하는 사용자 음량 설정 (PlayerPrefs에 저장)
     */
    public static class AudioPreferences
    {
        const string Prefix = "PokerDefense.Audio.";

        public static float Master => Read("Master");
        public static float Music => Read("Music");
        public static float Effects => Read("Effects");
        public static bool Muted => PlayerPrefs.GetInt(Prefix + "Muted", 0) == 1;
        public static float MusicGain => Muted ? 0f : Master * Music;
        public static float EffectsGain => Muted ? 0f : Master * Effects;

        static float Read(string key) => Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + key, 1f));

        public static void Set(float master, float music, float effects, bool muted)
        {
            PlayerPrefs.SetFloat(Prefix + "Master", Mathf.Clamp01(master));
            PlayerPrefs.SetFloat(Prefix + "Music", Mathf.Clamp01(music));
            PlayerPrefs.SetFloat(Prefix + "Effects", Mathf.Clamp01(effects));
            PlayerPrefs.SetInt(Prefix + "Muted", muted ? 1 : 0);
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
