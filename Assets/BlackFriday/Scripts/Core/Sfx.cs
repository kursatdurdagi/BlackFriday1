using System.Collections.Generic;
using UnityEngine;

namespace BlackFriday
{
    public enum SfxId { Pickup, Drop, Deliver, Wrong, Beep, Go, Bump, Win, Lose, Click }

    /// <summary>Asset gerektirmeyen, kodla üretilen basit ses efektleri.</summary>
    public static class Sfx
    {
        static readonly Dictionary<SfxId, AudioClip> clips = new Dictionary<SfxId, AudioClip>();
        static AudioSource source;
        const int Rate = 44100;

        public static void Play(SfxId id, float vol = 0.6f)
        {
            if (source == null)
            {
                var go = new GameObject("BF_Sfx");
                Object.DontDestroyOnLoad(go);
                source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0;
            }
            if (!clips.TryGetValue(id, out var clip)) { clip = Build(id); clips[id] = clip; }
            source.PlayOneShot(clip, vol);
        }

        static AudioClip Build(SfxId id)
        {
            switch (id)
            {
                case SfxId.Pickup: return Tones("pickup", new[] { 660f, 990f }, 0.07f, 0.5f);
                case SfxId.Drop: return Tones("drop", new[] { 330f, 220f }, 0.07f, 0.5f);
                case SfxId.Deliver: return Tones("deliver", new[] { 1046f, 1318f, 1568f, 2093f }, 0.07f, 0.45f);
                case SfxId.Wrong: return Tones("wrong", new[] { 180f, 150f }, 0.15f, 0.5f, square: true);
                case SfxId.Beep: return Tones("beep", new[] { 880f }, 0.15f, 0.5f);
                case SfxId.Go: return Tones("go", new[] { 1320f }, 0.35f, 0.5f);
                case SfxId.Bump: return Noise("bump", 0.18f);
                case SfxId.Win: return Tones("win", new[] { 523f, 659f, 784f, 1046f, 784f, 1046f }, 0.12f, 0.45f);
                case SfxId.Lose: return Tones("lose", new[] { 392f, 349f, 311f, 262f }, 0.18f, 0.45f);
                default: return Tones("click", new[] { 1200f }, 0.03f, 0.35f);
            }
        }

        static AudioClip Tones(string name, float[] freqs, float each, float amp, bool square = false)
        {
            int per = Mathf.CeilToInt(each * Rate);
            var data = new float[per * freqs.Length];
            for (int t = 0; t < freqs.Length; t++)
                for (int i = 0; i < per; i++)
                {
                    float time = i / (float)Rate;
                    float s = Mathf.Sin(2 * Mathf.PI * freqs[t] * time);
                    if (square) s = Mathf.Sign(s) * 0.6f;
                    float env = Mathf.Min(1f, i / 200f) * (1f - i / (float)per);
                    data[t * per + i] = s * amp * env;
                }
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        static AudioClip Noise(string name, float len)
        {
            int n = Mathf.CeilToInt(len * Rate);
            var data = new float[n];
            var rnd = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float env = 1f - i / (float)n;
                data[i] = ((float)rnd.NextDouble() * 2 - 1) * env * env * 0.6f * (0.5f + 0.5f * Mathf.Sin(i * 0.02f));
            }
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
