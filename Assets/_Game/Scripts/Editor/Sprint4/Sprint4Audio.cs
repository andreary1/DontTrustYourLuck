using System;
using System.Collections.Generic;
using System.IO;
using DontTrustYourLuck.Presentation.Audio;
using UnityEditor;
using UnityEngine;

namespace DontTrustYourLuck.EditorTools.Sprint4
{
    /// <summary>
    /// Placeholder audio synthesized from code (original, license-free): ~20 short SFX and a seamless
    /// baião-style loop (zabumba, triangle, bass, accordion-like reed voice) in G mixolydian.
    /// Final audio replaces the WAVs 1:1 (same file names); the library asset keeps its references.
    /// </summary>
    public static class Sprint4Audio
    {
        public const string SfxDir = "Assets/_Game/Audio/SFX";
        public const string MusicDir = "Assets/_Game/Audio/Music";
        public const string LibraryDir = "Assets/_Game/Data/Audio";
        public const string LibraryPath = LibraryDir + "/SoundEffectLibrary.asset";
        public const int Rate = 22050;

        private static uint _seed = 1u;

        public static SoundEffectLibrary Build()
        {
            Sprint4Art.EnsureFolder(SfxDir);
            Sprint4Art.EnsureFolder(MusicDir);
            Sprint4Art.EnsureFolder(LibraryDir);

            var entries = new List<SoundEffectEntry>
            {
                Sfx(SfxId.DiceRoll, "SFX_Dice_Roll", DiceRoll(), 0.8f, 0.05f, 0.05f),
                Sfx(SfxId.DiceStop, "SFX_Dice_Stop", DiceStop(), 0.85f, 0.04f, 0.05f),
                Sfx(SfxId.DieChoose, "SFX_Die_Choose", Notes(new[] { 79, 86 }, 0.07f, 0.16f, Wave.Triangle, 0.5f), 0.7f, 0.02f, 0.05f),
                Sfx(SfxId.PieceStep, "SFX_Piece_Step", Thunk(560f, 380f, 0.07f, 0.45f), 0.55f, 0.08f, 0.02f),
                Sfx(SfxId.PieceLand, "SFX_Piece_Land", PieceLand(), 0.8f, 0.04f, 0.05f),
                Sfx(SfxId.CardSelect, "SFX_Card_Select", CardFlick(0.08f, 2600f), 0.6f, 0.06f, 0.04f),
                Sfx(SfxId.CardPlay, "SFX_Card_Play", CardPlay(), 0.8f, 0.03f, 0.1f),
                Sfx(SfxId.CardDiscard, "SFX_Card_Discard", Sweep(0.18f, 3200f, 900f, 0.4f), 0.55f, 0.05f, 0.05f),
                Sfx(SfxId.ShieldUp, "SFX_Shield_Up", Notes(new[] { 72, 76, 79, 84 }, 0.06f, 0.35f, Wave.Square, 0.22f), 0.7f, 0f, 0.1f),
                Sfx(SfxId.ShieldBlock, "SFX_Shield_Block", Clang(), 0.85f, 0.02f, 0.1f),
                Sfx(SfxId.Swap, "SFX_Card_Swap", SwapGlide(), 0.75f, 0.02f, 0.1f),
                Sfx(SfxId.Retreat, "SFX_Card_Retreat", Notes(new[] { 76, 72, 67 }, 0.09f, 0.2f, Wave.Triangle, 0.45f), 0.7f, 0.02f, 0.1f),
                Sfx(SfxId.LuckyChime, "SFX_Lucky_Chime", Notes(new[] { 79, 83, 86, 91 }, 0.09f, 0.6f, Wave.Bell, 0.4f), 0.75f, 0f, 0.2f),
                Sfx(SfxId.Victory, "SFX_Victory", Victory(), 0.9f, 0f, 0.5f),
                Sfx(SfxId.DangerReveal, "SFX_Danger_Reveal", DangerReveal(), 0.8f, 0f, 0.3f),
                Sfx(SfxId.Elimination, "SFX_Elimination", Elimination(), 0.85f, 0f, 0.3f),
                Sfx(SfxId.UiClick, "SFX_UI_Click", Thunk(1500f, 1200f, 0.03f, 0.35f), 0.45f, 0.05f, 0.03f),
                Sfx(SfxId.UiConfirm, "SFX_UI_Confirm", Notes(new[] { 84, 91 }, 0.05f, 0.12f, Wave.Triangle, 0.45f), 0.6f, 0f, 0.05f),
                Sfx(SfxId.UiError, "SFX_UI_Error", Notes(new[] { 52, 49 }, 0.09f, 0.12f, Wave.Square, 0.2f), 0.6f, 0f, 0.15f),
                Sfx(SfxId.Restart, "SFX_Restart_Shuffle", Shuffle(), 0.7f, 0f, 0.3f),
                Sfx(SfxId.TurnChange, "SFX_Turn_Change", Notes(new[] { 88 }, 0.01f, 0.4f, Wave.Bell, 0.35f), 0.5f, 0f, 0.1f),
            };

            var music = SaveClip($"{MusicDir}/MUS_Fortaleza_Baiao.wav", Baiao(), true);

            var library = AssetDatabase.LoadAssetAtPath<SoundEffectLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SoundEffectLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            library.Configure(entries, music, 0.5f);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library;
        }

        private static SoundEffectEntry Sfx(SfxId id, string file, float[] samples, float volume, float jitter, float cooldown)
        {
            var clip = SaveClip($"{SfxDir}/{file}.wav", samples, false);
            return new SoundEffectEntry { id = id, clips = new[] { clip }, volume = volume, pitchJitter = jitter, cooldown = cooldown };
        }

        // ------------------------------------------------------------------ synthesis primitives

        private enum Wave { Sine, Triangle, Square, Saw, Bell }

        private static float Midi(float note) => 440f * Mathf.Pow(2f, (note - 69f) / 12f);

        private static float Rand()
        {
            _seed = _seed * 1664525u + 1013904223u;
            return ((_seed >> 8) & 0xFFFFFF) / 16777216f * 2f - 1f;
        }

        private static float Osc(Wave w, float phase)
        {
            float p = phase - Mathf.Floor(phase);
            switch (w)
            {
                case Wave.Triangle: return 4f * Mathf.Abs(p - 0.5f) - 1f;
                case Wave.Square: return p < 0.5f ? 0.8f : -0.8f;
                case Wave.Saw: return 2f * p - 1f;
                case Wave.Bell:
                    return Mathf.Sin(2f * Mathf.PI * p) + 0.35f * Mathf.Sin(2f * Mathf.PI * p * 2.76f) + 0.15f * Mathf.Sin(2f * Mathf.PI * p * 5.4f);
                default: return Mathf.Sin(2f * Mathf.PI * p);
            }
        }

        private static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

        /// <summary>Adds a tone with exponential decay and a linear pitch glide. Wraps when <paramref name="wrap"/> (seamless loops).</summary>
        private static void Tone(float[] buf, float start, float dur, float f0, float f1, Wave w, float vol,
            float attack = 0.004f, float decay = 6f, bool wrap = false)
        {
            int s0 = Mathf.RoundToInt(start * Rate);
            int n = Mathf.RoundToInt(dur * Rate);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float k = n > 1 ? i / (float)(n - 1) : 0f;
                float f = Mathf.Lerp(f0, f1, k);
                phase += f / Rate;
                float env = Mathf.Min(1f, t / Mathf.Max(0.0005f, attack)) * Mathf.Exp(-decay * t);
                env *= Mathf.Clamp01((n - i) / (Rate * 0.006f)); // de-click tail
                int idx = s0 + i;
                if (wrap) idx %= buf.Length;
                if (idx < 0 || idx >= buf.Length) continue;
                buf[idx] += Osc(w, phase) * env * vol;
            }
        }

        /// <summary>Band-limited noise burst (one-pole low-pass at <paramref name="lp"/>, optional high-pass).</summary>
        private static void Noise(float[] buf, float start, float dur, float lp, float hp, float vol, float decay = 20f, bool wrap = false, float lpEnd = -1f)
        {
            int s0 = Mathf.RoundToInt(start * Rate);
            int n = Mathf.RoundToInt(dur * Rate);
            float low = 0f, lowHp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float k = n > 1 ? i / (float)(n - 1) : 0f;
                float cutoff = lpEnd > 0f ? Mathf.Lerp(lp, lpEnd, k) : lp;
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
                float ah = 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Max(1f, hp) / Rate);
                low += a * (Rand() - low);
                lowHp += ah * (low - lowHp);
                float v = hp > 0f ? low - lowHp : low;
                float env = Mathf.Min(1f, t / 0.002f) * Mathf.Exp(-decay * t) * Mathf.Clamp01((n - i) / (Rate * 0.004f));
                int idx = s0 + i;
                if (wrap) idx %= buf.Length;
                if (idx < 0 || idx >= buf.Length) continue;
                buf[idx] += v * env * vol;
            }
        }

        private static float[] Notes(int[] notes, float spacing, float tail, Wave w, float vol)
        {
            var buf = Buffer(spacing * notes.Length + tail + 0.05f);
            for (int i = 0; i < notes.Length; i++)
            {
                float f = Midi(notes[i]);
                Tone(buf, i * spacing, tail + spacing, f, f, w, vol, 0.003f, 9f);
            }
            return buf;
        }

        private static float[] Thunk(float f0, float f1, float dur, float vol)
        {
            var buf = Buffer(dur + 0.02f);
            Tone(buf, 0f, dur, f0, f1, Wave.Sine, vol, 0.001f, 45f);
            Noise(buf, 0f, 0.015f, 4000f, 800f, vol * 0.5f, 120f);
            return buf;
        }

        private static float[] DiceRoll()
        {
            var buf = Buffer(0.45f);
            float t = 0f;
            for (int i = 0; i < 9; i++)
            {
                float f = 900f + 500f * Mathf.Abs(Rand());
                Tone(buf, t, 0.04f, f, f * 0.8f, Wave.Triangle, 0.25f, 0.001f, 80f);
                Noise(buf, t, 0.03f, 5000f, 1200f, 0.35f, 110f);
                t += 0.03f + 0.025f * Mathf.Abs(Rand());
            }
            return buf;
        }

        private static float[] DiceStop()
        {
            var buf = Buffer(0.3f);
            Tone(buf, 0f, 0.12f, 260f, 170f, Wave.Sine, 0.6f, 0.001f, 30f);
            Noise(buf, 0f, 0.03f, 3000f, 300f, 0.4f, 90f);
            Tone(buf, 0.11f, 0.12f, 300f, 200f, Wave.Sine, 0.45f, 0.001f, 35f);
            Noise(buf, 0.11f, 0.03f, 3000f, 300f, 0.3f, 90f);
            return buf;
        }

        private static float[] PieceLand()
        {
            var buf = Buffer(0.25f);
            Tone(buf, 0f, 0.2f, 320f, 150f, Wave.Sine, 0.7f, 0.001f, 18f);
            Noise(buf, 0f, 0.05f, 2200f, 200f, 0.35f, 60f);
            return buf;
        }

        private static float[] CardFlick(float dur, float lp)
        {
            var buf = Buffer(dur + 0.02f);
            Noise(buf, 0f, dur, lp, 900f, 0.8f, 30f, false, lp * 1.8f);
            return buf;
        }

        private static float[] Sweep(float dur, float from, float to, float vol)
        {
            var buf = Buffer(dur + 0.02f);
            Noise(buf, 0f, dur, from, 400f, vol * 2f, 10f, false, to);
            return buf;
        }

        private static float[] CardPlay()
        {
            var buf = Buffer(0.6f);
            Noise(buf, 0f, 0.22f, 800f, 300f, 0.9f, 4f, false, 5000f);
            Tone(buf, 0.18f, 0.4f, Midi(84), Midi(84), Wave.Bell, 0.3f, 0.002f, 8f);
            Tone(buf, 0.26f, 0.35f, Midi(91), Midi(91), Wave.Bell, 0.25f, 0.002f, 8f);
            return buf;
        }

        private static float[] Clang()
        {
            var buf = Buffer(0.7f);
            foreach (var (f, v) in new[] { (523f, 0.35f), (1310f, 0.22f), (2093f, 0.16f), (2760f, 0.1f) })
                Tone(buf, 0f, 0.65f, f, f * 0.995f, Wave.Sine, v, 0.001f, 7f);
            Noise(buf, 0f, 0.05f, 6000f, 1500f, 0.4f, 70f);
            return buf;
        }

        private static float[] SwapGlide()
        {
            var buf = Buffer(0.45f);
            Tone(buf, 0f, 0.38f, Midi(67), Midi(79), Wave.Triangle, 0.35f, 0.01f, 3f);
            Tone(buf, 0f, 0.38f, Midi(79), Midi(67), Wave.Triangle, 0.35f, 0.01f, 3f);
            Noise(buf, 0f, 0.3f, 1500f, 500f, 0.3f, 6f, false, 4000f);
            return buf;
        }

        private static float[] Victory()
        {
            var buf = Buffer(1.9f);
            int[] mel = { 67, 71, 74, 79, 76, 79 };
            float[] at = { 0f, 0.14f, 0.28f, 0.42f, 0.7f, 0.84f };
            float[] len = { 0.16f, 0.16f, 0.16f, 0.3f, 0.16f, 0.9f };
            for (int i = 0; i < mel.Length; i++)
            {
                Reed(buf, at[i], len[i], Midi(mel[i]), 0.22f, false);
                Reed(buf, at[i], len[i], Midi(mel[i] - 12), 0.12f, false);
            }
            for (int i = 0; i < 8; i++) Tone(buf, 0.84f + i * 0.11f, 0.15f, 5200f, 5200f, Wave.Sine, i % 2 == 0 ? 0.08f : 0.04f, 0.001f, 25f);
            Tone(buf, 0f, 0.3f, 90f, 70f, Wave.Sine, 0.5f, 0.002f, 12f);
            Tone(buf, 0.84f, 0.4f, 90f, 70f, Wave.Sine, 0.5f, 0.002f, 8f);
            return buf;
        }

        private static float[] DangerReveal()
        {
            var buf = Buffer(0.95f);
            Tone(buf, 0f, 0.9f, Midi(40), Midi(38), Wave.Saw, 0.18f, 0.01f, 3f);
            Tone(buf, 0f, 0.9f, Midi(46), Midi(44), Wave.Saw, 0.14f, 0.01f, 3f);
            Tone(buf, 0f, 0.5f, 70f, 45f, Wave.Sine, 0.6f, 0.002f, 6f);
            Noise(buf, 0f, 0.6f, 600f, 80f, 0.25f, 5f);
            LowPass(buf, 1400f);
            return buf;
        }

        private static float[] Elimination()
        {
            var buf = Buffer(1.15f);
            Tone(buf, 0f, 0.28f, Midi(67), Midi(66), Wave.Square, 0.16f, 0.01f, 3f);
            Tone(buf, 0.3f, 0.28f, Midi(66), Midi(65), Wave.Square, 0.16f, 0.01f, 3f);
            Tone(buf, 0.6f, 0.5f, Midi(65), Midi(58), Wave.Square, 0.16f, 0.01f, 2.5f);
            Noise(buf, 0.6f, 0.45f, 2500f, 600f, 0.25f, 6f, false, 700f); // paper flutter
            LowPass(buf, 2500f);
            return buf;
        }

        private static float[] Shuffle()
        {
            var buf = Buffer(0.5f);
            for (int i = 0; i < 14; i++) Noise(buf, i * 0.028f, 0.03f, 4500f, 1500f, 0.35f, 70f);
            Noise(buf, 0.4f, 0.06f, 2500f, 300f, 0.5f, 40f);
            return buf;
        }

        private static void LowPass(float[] buf, float cutoff)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate), y = 0f;
            for (int i = 0; i < buf.Length; i++) buf[i] = y += a * (buf[i] - y);
        }

        /// <summary>Accordion-like reed: two detuned saws (musette) plus a gentle tremolo, sustained.</summary>
        private static void Reed(float[] buf, float start, float dur, float f, float vol, bool wrap)
        {
            int s0 = Mathf.RoundToInt(start * Rate);
            int n = Mathf.RoundToInt(dur * Rate);
            float p1 = 0f, p2 = 0f, lp = 0f;
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * 2400f / Rate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                p1 += f * 1.0035f / Rate;
                p2 += f * 0.9965f / Rate;
                float v = 0.5f * (Osc(Wave.Saw, p1) + Osc(Wave.Saw, p2)) + 0.3f * Osc(Wave.Square, p1 * 0.5f);
                lp += a * (v - lp);
                float env = Mathf.Min(1f, t / 0.02f) * Mathf.Clamp01((n - i) / (Rate * 0.03f)) * (1f + 0.06f * Mathf.Sin(t * 2f * Mathf.PI * 5.5f));
                int idx = s0 + i;
                if (wrap) idx %= buf.Length;
                if (idx < 0 || idx >= buf.Length) continue;
                buf[idx] += lp * env * vol;
            }
        }

        // ------------------------------------------------------------------ music

        /// <summary>
        /// 16 bars of 2/4 at 110 BPM: zabumba (baião figure), triangle 16ths, bass on root/fifth, reed melody.
        /// All events wrap around the buffer end, so the loop is seamless.
        /// </summary>
        private static float[] Baiao()
        {
            _seed = 77u;
            const float bpm = 110f;
            float beat = 60f / bpm;
            float bar = beat * 2f;
            const int bars = 16;
            var buf = new float[Mathf.RoundToInt(bar * bars * Rate)];

            // Chords (root MIDI) in G mixolydian: G G F G | C G D G (x2).
            int[] roots = { 43, 43, 41, 43, 48, 43, 50, 43 };
            int[][] triads =
            {
                new[] { 67, 71, 74 }, new[] { 67, 71, 74 }, new[] { 65, 69, 72 }, new[] { 67, 71, 74 },
                new[] { 67, 72, 76 }, new[] { 67, 71, 74 }, new[] { 66, 69, 72 }, new[] { 67, 71, 74 }
            };
            // Melody: (beat offset in the bar, length in beats, MIDI) per bar, 8-bar phrase played twice (2nd varied).
            var phrase = new List<(int bar, float at, float len, int note)>
            {
                (0, 0f, 0.5f, 74), (0, 0.5f, 0.5f, 76), (0, 1f, 0.75f, 77), (0, 1.75f, 0.25f, 76),
                (1, 0f, 1f, 74), (1, 1f, 0.5f, 71), (1, 1.5f, 0.5f, 74),
                (2, 0f, 0.5f, 72), (2, 0.5f, 0.5f, 74), (2, 1f, 0.75f, 72), (2, 1.75f, 0.25f, 69),
                (3, 0f, 1.5f, 71), (3, 1.5f, 0.5f, 67),
                (4, 0f, 0.5f, 72), (4, 0.5f, 0.5f, 76), (4, 1f, 0.75f, 79), (4, 1.75f, 0.25f, 77),
                (5, 0f, 1f, 76), (5, 1f, 1f, 74),
                (6, 0f, 0.5f, 72), (6, 0.5f, 0.5f, 74), (6, 1f, 0.5f, 72), (6, 1.5f, 0.5f, 69),
                (7, 0f, 2f, 67),
            };

            for (int b = 0; b < bars; b++)
            {
                float t0 = b * bar;
                int ci = b % 8;
                int root = roots[ci];

                // Zabumba: low "bum" on 1 and the 16th before 2 (dotted figure), muffled hit on 2.
                Tone(buf, t0, 0.3f, 95f, 60f, Wave.Sine, 0.55f, 0.002f, 9f, true);
                Tone(buf, t0 + beat * 0.75f, 0.25f, 95f, 62f, Wave.Sine, 0.4f, 0.002f, 11f, true);
                Tone(buf, t0 + beat, 0.12f, 120f, 90f, Wave.Sine, 0.25f, 0.002f, 25f, true);
                // Bacalhau (thin stick) on the off-beats.
                Noise(buf, t0 + beat * 0.5f, 0.04f, 6000f, 2500f, 0.18f, 80f, true);
                Noise(buf, t0 + beat * 1.5f, 0.04f, 6000f, 2500f, 0.18f, 80f, true);

                // Triangle: 16ths, open on the off-beat 16ths.
                for (int s = 0; s < 8; s++)
                {
                    bool open = s % 2 == 1;
                    Tone(buf, t0 + s * beat * 0.25f, open ? 0.18f : 0.05f, 4700f, 4700f, Wave.Bell, open ? 0.045f : 0.025f, 0.001f, open ? 14f : 60f, true);
                }

                // Bass: root, fifth, octave on the zabumba figure.
                Tone(buf, t0, beat * 0.7f, Midi(root - 12), Midi(root - 12), Wave.Triangle, 0.32f, 0.004f, 3f, true);
                Tone(buf, t0 + beat * 0.75f, beat * 0.25f, Midi(root - 5), Midi(root - 5), Wave.Triangle, 0.26f, 0.004f, 4f, true);
                Tone(buf, t0 + beat, beat * 0.9f, Midi(root), Midi(root), Wave.Triangle, 0.26f, 0.004f, 3f, true);

                // Reed chord stabs on the off-beats (left hand of the accordion).
                foreach (int n in triads[ci])
                {
                    Reed(buf, t0 + beat * 0.5f, beat * 0.3f, Midi(n - 12), 0.035f, true);
                    Reed(buf, t0 + beat * 1.5f, beat * 0.3f, Midi(n - 12), 0.035f, true);
                }
            }

            for (int rep = 0; rep < 2; rep++)
            {
                foreach (var (barIdx, at, len, note) in phrase)
                {
                    int n = note;
                    if (rep == 1 && barIdx == 7) n = 79; // answer ends up high the second time
                    if (rep == 1 && barIdx == 3) n = note + (note == 71 ? 3 : 0);
                    float start = (rep * 8 + barIdx) * bar + at * beat;
                    Reed(buf, start, len * beat * 0.95f, Midi(n), 0.13f, true);
                }
            }

            Normalize(buf, 0.8f);
            return buf;
        }

        // ------------------------------------------------------------------ file output

        private static void Normalize(float[] buf, float peak)
        {
            float max = 0f;
            foreach (float v in buf) max = Mathf.Max(max, Mathf.Abs(v));
            if (max <= 0f) return;
            float k = peak / max;
            for (int i = 0; i < buf.Length; i++) buf[i] *= k;
        }

        internal static AudioClip SaveClip(string assetPath, float[] samples, bool music)
        {
            if (!music) Normalize(samples, 0.85f);
            using (var stream = new FileStream(assetPath, FileMode.Create))
            using (var w = new BinaryWriter(stream))
            {
                int bytes = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + bytes);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1); // PCM
                w.Write((short)1); // mono
                w.Write(Rate);
                w.Write(Rate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(bytes);
                foreach (float s in samples) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var importer = (AudioImporter)AssetImporter.GetAtPath(assetPath);
            importer.forceToMono = true;
            importer.loadInBackground = music;
            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = music ? 0.5f : 0.7f;
            settings.preloadAudioData = !music;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        }
    }
}
