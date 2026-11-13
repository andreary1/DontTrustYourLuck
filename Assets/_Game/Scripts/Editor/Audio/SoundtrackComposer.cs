using System.Collections.Generic;
using DontTrustYourLuck.EditorTools.Sprint4;
using DontTrustYourLuck.Presentation.Audio;
using UnityEditor;
using UnityEngine;

namespace DontTrustYourLuck.EditorTools.Audio
{
    /// <summary>
    /// Procedural soundtrack: four ORIGINAL pieces in the style of 1920s-30s Rio samba (samba-marcha, samba de breque,
    /// maxixe, samba de roda). The project's reference recordings only informed tempo, instrumentation and feel;
    /// no melody, lyric or recording is reproduced. Melodies come from hand-written rhythmic motifs realised over
    /// each piece's chord chart, so they are new material.
    ///
    /// Each piece is 64 bars of 2/4 (form AABA twice; the second pass swaps the lead and adds counter-lines),
    /// 60-80 s, mono 22.05 kHz, and every event wraps around the buffer end so the file also loops seamlessly.
    /// A light "78 rpm" colour (band-limit, faint crackle) matches the aged lottery-card art direction.
    /// Batch: -executeMethod DontTrustYourLuck.EditorTools.Audio.SoundtrackComposer.BuildFromCommandLine
    /// </summary>
    public static class SoundtrackComposer
    {
        private const int Rate = Sprint4Audio.Rate;
        private const int BarsPerSection = 8;
        private static readonly char[] Form = { 'A', 'A', 'B', 'A', 'A', 'A', 'B', 'A' };

        [MenuItem("Tools/Don't Trust Your Luck/Build Soundtrack (Samba)")]
        public static void BuildFromMenu() => BuildFromCommandLine();

        public static void BuildFromCommandLine()
        {
            var library = AssetDatabase.LoadAssetAtPath<SoundEffectLibrary>(Sprint4Audio.LibraryPath);
            if (library == null)
            {
                Debug.LogWarning("[Soundtrack] SoundEffectLibrary not found; run the Sprint 4 builder first.");
                return;
            }
            Build(library);
            AssetDatabase.SaveAssets();
        }

        public static void Build(SoundEffectLibrary library)
        {
            Sprint4Art.EnsureFolder(Sprint4Audio.MusicDir);
            var clips = new List<AudioClip>();
            foreach (var song in Songs())
            {
                var samples = Compose(song);
                clips.Add(Sprint4Audio.SaveClip($"{Sprint4Audio.MusicDir}/{song.File}.wav", samples, true));
            }
            if (library == null) return;
            library.ConfigurePlaylist(clips);
            EditorUtility.SetDirty(library);
            Debug.Log($"[Soundtrack] {clips.Count} tracks generated and assigned to the music playlist.");
        }

        // ================================================================== song data

        private enum Q { Maj, Min, Dom7, Min6, Maj6, Dim7, Min7 }

        private readonly struct Chord
        {
            public readonly int Root; // pitch class, C = 0
            public readonly Q Quality;
            public Chord(int root, Q quality) { Root = root; Quality = quality; }
        }

        private enum Style { Marcha, Breque, Maxixe, Roda }
        private enum Voice { Flute, Clarinet, Brass, MutedBrass, Singer }

        private sealed class Motif
        {
            public readonly (int at, int len, int step)[] Notes; // positions/lengths in 16ths over 2 bars
            public Motif(params (int at, int len, int step)[] notes) { Notes = notes; }
        }

        private sealed class Song
        {
            public string File;
            public Style Style;
            public float Bpm;
            public int Tonic;
            public int[] Scale;
            public Chord[] A, B;
            public Motif MotifA, MotifB, Cadence;
            public Voice Lead1, Lead2;
            public int Center;
            public uint Seed;
            public float SurdoHz = 62f;
        }

        private static readonly int[] Major = { 0, 2, 4, 5, 7, 9, 11 };
        private static readonly int[] HarmonicMinor = { 0, 2, 3, 5, 7, 8, 11 };

        private static Chord Ch(int root, Q q) => new Chord(root, q);

        private static IEnumerable<Song> Songs()
        {
            const int C = 0, D = 2, E = 4, F = 5, G = 7, A = 9, Bb = 10, B = 11;

            // "Bilhete Premiado": bright samba-marcha (feel of Com que Roupa? / Sinhô's marchas), C major, flute lead.
            yield return new Song
            {
                File = "MUS_Samba_BilhetePremiado", Style = Style.Marcha, Bpm = 108f, Tonic = C, Scale = Major,
                A = new[] { Ch(C, Q.Maj), Ch(C, Q.Maj), Ch(A, Q.Dom7), Ch(D, Q.Min), Ch(G, Q.Dom7), Ch(G, Q.Dom7), Ch(C, Q.Maj), Ch(G, Q.Dom7) },
                B = new[] { Ch(F, Q.Maj), Ch(F, Q.Maj), Ch(F, Q.Min6), Ch(C, Q.Maj), Ch(D, Q.Dom7), Ch(G, Q.Dom7), Ch(C, Q.Maj), Ch(G, Q.Dom7) },
                MotifA = new Motif((0, 1, 0), (1, 2, 1), (3, 1, 2), (4, 2, 4), (6, 2, 2), (8, 1, 1), (9, 2, 0), (11, 1, -1), (12, 4, 0)),
                MotifB = new Motif((0, 2, 4), (2, 1, 3), (3, 1, 2), (4, 2, 1), (6, 2, 2), (8, 3, 4), (11, 1, 5), (12, 2, 4), (14, 2, 2)),
                Cadence = new Motif((0, 1, 4), (1, 2, 3), (3, 1, 2), (4, 2, 1), (6, 2, -1), (8, 6, 0)),
                Lead1 = Voice.Flute, Lead2 = Voice.Clarinet, Center = 76, Seed = 1101u, SurdoHz = 60f,
            };

            // "Prosa no Balcão": laid-back samba de breque (feel of Conversa de Botequim), A minor, stop-time breaks.
            yield return new Song
            {
                File = "MUS_Samba_ProsaNoBalcao", Style = Style.Breque, Bpm = 96f, Tonic = A, Scale = HarmonicMinor,
                A = new[] { Ch(A, Q.Min), Ch(A, Q.Min), Ch(E, Q.Dom7), Ch(E, Q.Dom7), Ch(E, Q.Dom7), Ch(E, Q.Dom7), Ch(A, Q.Min), Ch(A, Q.Dom7) },
                B = new[] { Ch(D, Q.Min), Ch(D, Q.Min), Ch(A, Q.Min), Ch(A, Q.Min), Ch(B, Q.Dim7), Ch(E, Q.Dom7), Ch(A, Q.Min), Ch(E, Q.Dom7) },
                MotifA = new Motif((1, 1, 0), (2, 1, 1), (3, 2, 2), (6, 2, 0), (9, 2, -1), (11, 1, 0), (12, 4, -3)),
                MotifB = new Motif((0, 1, 2), (1, 2, 4), (3, 1, 3), (4, 1, 2), (5, 2, 1), (7, 1, 0), (8, 4, 1), (13, 1, 2), (14, 2, 0)),
                Cadence = new Motif((0, 2, 2), (2, 1, 1), (3, 1, 0), (4, 4, -1), (8, 8, 0)),
                Lead1 = Voice.Clarinet, Lead2 = Voice.MutedBrass, Center = 72, Seed = 2203u, SurdoHz = 58f,
            };

            // "Maxixe do Azar": ornamented maxixe/samba amaxixado (feel of 1928 Sinhô recordings), F major, piano-led groove.
            yield return new Song
            {
                File = "MUS_Samba_MaxixeDoAzar", Style = Style.Maxixe, Bpm = 112f, Tonic = F, Scale = Major,
                A = new[] { Ch(F, Q.Maj), Ch(C, Q.Dom7), Ch(C, Q.Dom7), Ch(F, Q.Maj), Ch(F, Q.Maj), Ch(Bb, Q.Maj), Ch(C, Q.Dom7), Ch(F, Q.Maj) },
                B = new[] { Ch(D, Q.Dom7), Ch(G, Q.Min), Ch(C, Q.Dom7), Ch(F, Q.Maj), Ch(D, Q.Dom7), Ch(G, Q.Min), Ch(C, Q.Dom7), Ch(F, Q.Maj) },
                MotifA = new Motif((0, 1, 0), (1, 1, 1), (2, 1, 2), (3, 1, 0), (4, 2, 4), (6, 1, 3), (7, 1, 2), (8, 1, 1), (9, 2, 2), (11, 1, 0), (12, 2, -1), (14, 2, 0)),
                MotifB = new Motif((0, 3, 4), (3, 1, 5), (4, 2, 4), (6, 2, 2), (8, 1, 3), (9, 2, 2), (11, 1, 1), (12, 4, 2)),
                Cadence = new Motif((0, 1, 2), (1, 1, 3), (2, 1, 2), (3, 1, 1), (4, 2, 0), (6, 2, -2), (8, 8, 0)),
                Lead1 = Voice.Clarinet, Lead2 = Voice.Flute, Center = 74, Seed = 3307u, SurdoHz = 64f,
            };

            // "Roda do Cantador": samba de roda with call and response (feel of A Baiana Deu Sinal), G major, claps and agogô.
            yield return new Song
            {
                File = "MUS_Samba_RodaDoCantador", Style = Style.Roda, Bpm = 126f, Tonic = G, Scale = Major,
                A = new[] { Ch(G, Q.Maj), Ch(G, Q.Maj), Ch(D, Q.Dom7), Ch(D, Q.Dom7), Ch(D, Q.Dom7), Ch(D, Q.Dom7), Ch(G, Q.Maj), Ch(G, Q.Maj) },
                B = new[] { Ch(C, Q.Maj), Ch(C, Q.Maj), Ch(G, Q.Maj), Ch(G, Q.Maj), Ch(A, Q.Dom7), Ch(D, Q.Dom7), Ch(G, Q.Maj), Ch(D, Q.Dom7) },
                MotifA = new Motif((0, 2, 4), (2, 2, 4), (4, 1, 2), (5, 2, 4), (7, 1, 5), (8, 2, 4), (10, 2, 2), (12, 4, 0)),
                MotifB = new Motif((0, 1, 0), (1, 2, 2), (3, 1, 1), (4, 2, 0), (6, 2, -1), (8, 8, 0)),
                Cadence = new Motif((0, 1, 2), (1, 2, 1), (3, 1, 0), (4, 2, -1), (6, 2, -3), (8, 8, 0)),
                Lead1 = Voice.Singer, Lead2 = Voice.Singer, Center = 71, Seed = 4409u, SurdoHz = 66f,
            };
        }

        // ================================================================== composition

        private sealed class Ctx
        {
            public Song Song;
            public float[] Buf;
            public float Beat, Six, Bar;
            public float[] Swing;
            public int[] ScaleNotes;
            public bool[] InScale = new bool[12];
        }

        private static float[] Compose(Song song)
        {
            _seed = song.Seed;
            var c = new Ctx { Song = song, Beat = 60f / song.Bpm };
            c.Six = c.Beat / 4f;
            c.Bar = c.Beat * 2f;
            int totalBars = Form.Length * BarsPerSection;
            c.Buf = new float[Mathf.RoundToInt(c.Bar * totalBars * Rate)];
            c.Swing = song.Style switch
            {
                Style.Breque => new[] { 0f, 0.08f, 0.02f, -0.04f },
                Style.Roda => new[] { 0f, 0.07f, 0f, -0.03f },
                Style.Maxixe => new[] { 0f, 0.04f, 0f, -0.02f },
                _ => new[] { 0f, 0.05f, 0f, -0.03f },
            };
            var notes = new List<int>();
            for (int m = 24; m <= 108; m++)
            {
                int pc = ((m - song.Tonic) % 12 + 12) % 12;
                if (System.Array.IndexOf(song.Scale, pc) >= 0) notes.Add(m);
            }
            c.ScaleNotes = notes.ToArray();
            foreach (int iv in song.Scale) c.InScale[(song.Tonic + iv) % 12] = true;

            var seen = new Dictionary<char, int>();
            for (int sec = 0; sec < Form.Length; sec++)
            {
                char letter = Form[sec];
                seen.TryGetValue(letter, out int rep);
                seen[letter] = rep + 1;
                int pass = sec / 4;
                var chords = letter == 'A' ? song.A : song.B;
                var nextSection = Form[(sec + 1) % Form.Length] == 'A' ? song.A : song.B;

                for (int bar = 0; bar < BarsPerSection; bar++)
                {
                    int absBar = sec * BarsPerSection + bar;
                    var chord = chords[bar];
                    var next = bar < BarsPerSection - 1 ? chords[bar + 1] : nextSection[0];
                    int breakFrom = song.Style == Style.Breque && letter == 'A' && (bar == 3 || bar == 7) ? 4 : 8;
                    Accompany(c, absBar, bar, chord, next, pass, letter, breakFrom);
                }
                Melody(c, sec * BarsPerSection, chords, letter, rep, pass, song.Seed + (uint)sec * 7919u);
            }

            Reverb(c.Buf, song.Style == Style.Breque ? 0.1f : 0.14f);
            AgeTo78(c.Buf);
            Master(c.Buf);
            return c.Buf;
        }

        private static float T(Ctx c, int absBar, int s)
        {
            int k = ((s % 4) + 4) % 4;
            return absBar * c.Bar + (s + c.Swing[k]) * c.Six;
        }

        // ------------------------------------------------------------------ melody

        private static void Melody(Ctx c, int startBar, Chord[] chords, char letter, int rep, int pass, uint seed)
        {
            var song = c.Song;
            uint prev = _seed;
            _seed = seed;
            var main = letter == 'A' ? song.MotifA : song.MotifB;
            bool varied = rep > 0;
            int shiftB = letter == 'B' ? 3 : 0;
            var voice = pass == 0 ? song.Lead1 : song.Lead2;

            for (int seg = 0; seg < 4; seg++)
            {
                int segBar = seg * 2;
                bool cadence = seg == 3;
                Motif motif;
                bool response = false;
                if (song.Style == Style.Roda)
                {
                    // call (singer) / response (chorus in thirds)
                    response = seg % 2 == 1;
                    motif = cadence ? song.Cadence : response ? song.MotifB : song.MotifA;
                }
                else motif = cadence ? song.Cadence : main;

                int center = song.Center + shiftB + (seg == 2 ? 2 : 0);
                var line = Realize(c, motif, chords, segBar, center, cadence, varied || seg == 1);

                foreach (var (at, len, midi) in line)
                {
                    int absBar = startBar + segBar + at / 8;
                    float t = T(c, absBar, at % 8);
                    float dur = len * c.Six * 0.92f;
                    if (response)
                    {
                        PlayVoice(c, Voice.Flute, t, dur, midi, 0.17f);
                        int third = Harmonize(c, midi, chords[segBar + at / 8]);
                        PlayVoice(c, Voice.Clarinet, t, dur, third, 0.13f);
                        if (pass == 1) PlayVoice(c, Voice.Flute, t, dur, midi + 12, 0.06f);
                    }
                    else PlayVoice(c, voice, t, dur, midi, song.Style == Style.Roda && pass == 1 ? 0.26f : 0.22f);
                }
            }

            // Second pass: brass guide tones (3rds/7ths) under marcha and maxixe; a soft flute pad over the breque.
            if (pass == 1 && song.Style != Style.Roda)
            {
                int last = 58;
                for (int bar = 0; bar < BarsPerSection; bar++)
                {
                    var ch = chords[bar];
                    var iv = Intervals(ch.Quality);
                    int guide = iv.Length > 3 ? iv[3] : iv[1];
                    int pitch = NearestPc(last, (ch.Root + guide) % 12, 52, 66);
                    last = pitch;
                    float t = T(c, startBar + bar, 0);
                    var v = song.Style == Style.Breque ? Voice.Flute : Voice.Brass;
                    PlayVoice(c, v, t, c.Bar * 0.95f, song.Style == Style.Breque ? pitch + 12 : pitch, song.Style == Style.Breque ? 0.05f : 0.08f);
                }
            }
            _seed = prev;
        }

        private static List<(int at, int len, int midi)> Realize(Ctx c, Motif motif, Chord[] chords, int segBar, int center, bool cadence, bool varied)
        {
            var result = new List<(int, int, int)>();
            int anchor = NearestChordTone(center, chords[segBar]);
            int lastIdx = motif.Notes.Length - 1;
            for (int i = 0; i < motif.Notes.Length; i++)
            {
                var (at, len, step) = motif.Notes[i];
                if (varied && i > 0 && i < lastIdx && Rand01() < 0.25f) step += Rand01() < 0.5f ? -1 : 1;
                int pitch = ScaleStep(c, anchor, step);
                var chord = chords[Mathf.Min(chords.Length - 1, segBar + at / 8)];
                pitch = at % 4 == 0 ? NearestChordTone(pitch, chord) : FixChromatic(c, pitch, chord);
                if (cadence && i == lastIdx) pitch = NearestPc(pitch, c.Song.Tonic, center - 8, center + 9);
                while (pitch > center + 12) pitch -= 12;
                while (pitch < center - 8) pitch += 12;
                result.Add((at, len, pitch));
            }
            return result;
        }

        private static int Harmonize(Ctx c, int midi, Chord chord)
        {
            int third = ScaleStep(c, midi, -2);
            return FixChromatic(c, third, chord);
        }

        // ------------------------------------------------------------------ accompaniment

        private static void Accompany(Ctx c, int absBar, int barInSection, Chord chord, Chord next, int pass, char letter, int breakFrom)
        {
            var song = c.Song;
            bool baixaria = (barInSection == 3 || barInSection == 7) && (pass == 1 || letter == 'B') && next.Root != chord.Root
                            && breakFrom == 8 && song.Style != Style.Maxixe;
            int root = BassMidi(chord.Root);
            int fifth = root + 7 > 55 ? root - 5 : root + 7;
            var cav = Voicing(chord, 62, 4);
            bool evenBar = absBar % 2 == 0;

            switch (song.Style)
            {
                case Style.Marcha:
                    Surdo(c, T(c, absBar, 0), true, 0.32f);
                    Surdo(c, T(c, absBar, 4), true, 0.5f);
                    for (int s = 0; s < 8; s++)
                    {
                        Jingle(c, T(c, absBar, s), s % 4 == 0 ? 0.09f : s % 4 == 3 ? 0.07f : 0.04f);
                        if (s % 4 == 0) PandeiroBass(c, T(c, absBar, s), 0.16f);
                    }
                    PandeiroSlap(c, T(c, absBar, 6), 0.1f);
                    foreach (int s in evenBar ? new[] { 0, 2, 3, 5, 7 } : new[] { 1, 2, 4, 6 }) Tamborim(c, T(c, absBar, s), 0.11f);
                    foreach (int s in evenBar ? new[] { 0, 3, 6 } : new[] { 2, 5 }) Strum(c, T(c, absBar, s), cav, c.Six * 1.6f, 0.07f, s % 2 == 0);
                    if (baixaria) Baixaria(c, absBar, chord, next);
                    else
                    {
                        Bass(c, T(c, absBar, 0), root, c.Six * 3f, 0.3f);
                        Bass(c, T(c, absBar, 4), fifth, c.Six * 3.5f, 0.26f);
                    }
                    break;

                case Style.Breque:
                    if (breakFrom == 4) // stop-time: one tutti hit on beat 2, then silence until the next bar
                    {
                        Surdo(c, T(c, absBar, 0), false, 0.22f);
                        PratoFaca(c, T(c, absBar, 0), 0.06f);
                        PratoFaca(c, T(c, absBar, 2), 0.04f);
                        Bass(c, T(c, absBar, 0), root, c.Six * 3f, 0.28f);
                        Strum(c, T(c, absBar, 0), cav, c.Six * 1.4f, 0.07f, true);
                        float hit = T(c, absBar, 4);
                        Surdo(c, hit, true, 0.55f);
                        PandeiroSlap(c, hit, 0.14f);
                        Strum(c, hit, cav, c.Six * 1.2f, 0.1f, true);
                        Bass(c, hit, root, c.Six * 1.2f, 0.3f);
                        break;
                    }
                    Surdo(c, T(c, absBar, 0), false, 0.2f);
                    Surdo(c, T(c, absBar, 4), true, 0.5f);
                    for (int s = 0; s < 8; s++)
                    {
                        PratoFaca(c, T(c, absBar, s), s % 4 == 0 ? 0.06f : s % 4 == 3 ? 0.05f : 0.03f);
                        if (s % 2 == 0) Jingle(c, T(c, absBar, s), 0.035f);
                    }
                    PandeiroSlap(c, T(c, absBar, 6), 0.08f);
                    if (pass == 1) foreach (int s in evenBar ? new[] { 0, 2, 3, 5, 7 } : new[] { 1, 2, 4, 6 }) Tamborim(c, T(c, absBar, s), 0.08f);
                    foreach (int s in evenBar ? new[] { 0, 3, 6 } : new[] { 2, 4, 7 }) Strum(c, T(c, absBar, s), cav, c.Six * 1.5f, 0.065f, s % 2 == 0);
                    if (baixaria) Baixaria(c, absBar, chord, next);
                    else
                    {
                        Bass(c, T(c, absBar, 0), root, c.Six * 2.5f, 0.28f);
                        Bass(c, T(c, absBar, 3), fifth, c.Six * 1f, 0.16f);
                        Bass(c, T(c, absBar, 4), fifth, c.Six * 3.5f, 0.26f);
                    }
                    break;

                case Style.Maxixe:
                {
                    // Piano left hand: habanera bass (root - fifth - third - fifth); right hand: syncopated chords.
                    int pRoot = root + 12;
                    var iv = Intervals(chord.Quality);
                    int third = NearestPc(pRoot + 4, (chord.Root + iv[1]) % 12, pRoot, pRoot + 11);
                    int pFifth = NearestPc(pRoot + 7, (chord.Root + 7) % 12, pRoot, pRoot + 11);
                    Piano(c, T(c, absBar, 0), c.Six * 3f, root, 0.2f);
                    Piano(c, T(c, absBar, 0), c.Six * 3f, pRoot, 0.12f);
                    Piano(c, T(c, absBar, 3), c.Six * 1f, pFifth, 0.12f);
                    Piano(c, T(c, absBar, 4), c.Six * 2f, third, 0.13f);
                    Piano(c, T(c, absBar, 6), c.Six * 2f, pFifth, 0.12f);
                    var rh = Voicing(chord, 64, 3);
                    foreach (int s in new[] { 1, 3, 6 })
                        foreach (int n in rh) Piano(c, T(c, absBar, s), c.Six * (s == 3 ? 2f : 1f), n, 0.06f);
                    Surdo(c, T(c, absBar, 4), true, 0.24f);
                    for (int s = 0; s < 8; s += 2) Jingle(c, T(c, absBar, s), s == 0 ? 0.05f : 0.035f);
                    if (pass == 1) foreach (int s in new[] { 0, 3, 4, 6 }) Ganza(c, T(c, absBar, s), 0.04f);
                    break;
                }

                case Style.Roda:
                    Atabaque(c, T(c, absBar, 0), 105f, 0.34f);
                    Atabaque(c, T(c, absBar, 3), 165f, 0.18f);
                    Atabaque(c, T(c, absBar, 4), 105f, 0.3f);
                    Atabaque(c, T(c, absBar, 6), 165f, 0.2f);
                    foreach (int s in new[] { 0, 3, 6 }) Palmas(c, T(c, absBar, s), 0.16f);
                    for (int s = 0; s < 8; s++) Ganza(c, T(c, absBar, s), s % 4 == 0 ? 0.06f : s % 2 == 1 ? 0.035f : 0.045f);
                    if (pass == 1)
                    {
                        var agogo = evenBar ? new[] { (0, false), (2, true), (4, false), (5, true), (7, true) } : new[] { (0, false), (2, true), (3, false), (5, true), (6, false) };
                        foreach (var (s, high) in agogo) Agogo(c, T(c, absBar, s), high, 0.06f);
                    }
                    foreach (int s in evenBar ? new[] { 0, 3, 6 } : new[] { 1, 4, 6 }) Strum(c, T(c, absBar, s), cav, c.Six * 1.5f, 0.07f, s % 2 == 0);
                    if (baixaria) Baixaria(c, absBar, chord, next);
                    else
                    {
                        Bass(c, T(c, absBar, 0), root, c.Six * 3f, 0.3f);
                        Bass(c, T(c, absBar, 4), fifth, c.Six * 3f, 0.26f);
                    }
                    break;
            }
        }

        /// <summary>7-string-guitar style run: four eighth notes walking by scale step into the next chord's root.</summary>
        private static void Baixaria(Ctx c, int absBar, Chord chord, Chord next)
        {
            int target = BassMidi(next.Root);
            int from = BassMidi(chord.Root);
            int dir = target >= from ? 1 : -1;
            for (int k = 0; k < 4; k++)
            {
                int pitch = ScaleStep(c, target, -dir * (4 - k));
                pitch = FixChromatic(c, pitch, k < 3 ? chord : next);
                Bass(c, T(c, absBar, k * 2), pitch, c.Six * 1.8f, k == 0 ? 0.28f : 0.24f);
            }
        }

        // ================================================================== theory helpers

        private static int[] Intervals(Q q) => q switch
        {
            Q.Min => new[] { 0, 3, 7 },
            Q.Dom7 => new[] { 0, 4, 7, 10 },
            Q.Min6 => new[] { 0, 3, 7, 9 },
            Q.Maj6 => new[] { 0, 4, 7, 9 },
            Q.Dim7 => new[] { 0, 3, 6, 9 },
            Q.Min7 => new[] { 0, 3, 7, 10 },
            _ => new[] { 0, 4, 7 },
        };

        private static int BassMidi(int pc)
        {
            int m = 36 + pc;
            return m < 40 ? m + 12 : m;
        }

        private static List<int> Voicing(Chord chord, int floor, int count)
        {
            var iv = Intervals(chord.Quality);
            var list = new List<int>();
            for (int i = 0; i < iv.Length && list.Count < count; i++)
            {
                int m = floor + ((chord.Root + iv[i] - floor) % 12 + 12) % 12;
                list.Add(m);
            }
            if (list.Count < count) list.Add(floor + ((chord.Root - floor) % 12 + 12) % 12 + 12);
            list.Sort();
            return list;
        }

        private static bool IsChordTone(int midi, Chord chord)
        {
            int rel = ((midi - chord.Root) % 12 + 12) % 12;
            return System.Array.IndexOf(Intervals(chord.Quality), rel) >= 0;
        }

        private static int NearestChordTone(int midi, Chord chord)
        {
            for (int d = 0; d < 12; d++)
            {
                if (IsChordTone(midi + d, chord)) return midi + d;
                if (IsChordTone(midi - d, chord)) return midi - d;
            }
            return midi;
        }

        private static int NearestPc(int midi, int pc, int lo, int hi)
        {
            int best = midi, bestDist = int.MaxValue;
            for (int m = lo; m <= hi; m++)
            {
                if (((m % 12) + 12) % 12 != pc) continue;
                int d = Mathf.Abs(m - midi);
                if (d < bestDist) { best = m; bestDist = d; }
            }
            return best;
        }

        private static int ScaleStep(Ctx c, int midi, int steps)
        {
            var notes = c.ScaleNotes;
            int idx = 0, bestDist = int.MaxValue;
            for (int i = 0; i < notes.Length; i++)
            {
                int d = Mathf.Abs(notes[i] - midi);
                if (d < bestDist) { bestDist = d; idx = i; }
            }
            return notes[Mathf.Clamp(idx + steps, 0, notes.Length - 1)];
        }

        /// <summary>Weak-beat notes a semitone away from a chromatic chord tone (C# in A7, Ab in Fm6) take that tone.</summary>
        private static int FixChromatic(Ctx c, int midi, Chord chord)
        {
            foreach (int iv in Intervals(chord.Quality))
            {
                int pc = (chord.Root + iv) % 12;
                if (c.InScale[pc]) continue;
                int rel = ((pc - midi) % 12 + 12) % 12;
                if (rel == 1) return midi + 1;
                if (rel == 11) return midi - 1;
            }
            return midi;
        }

        // ================================================================== synthesis

        private static uint _seed = 1u;

        private static float Rand()
        {
            _seed = _seed * 1664525u + 1013904223u;
            return ((_seed >> 8) & 0xFFFFFF) / 16777216f * 2f - 1f;
        }

        private static float Rand01() => (Rand() + 1f) * 0.5f;

        private static float Midi(float note) => 440f * Mathf.Pow(2f, (note - 69f) / 12f);

        private static void Add(float[] buf, int idx, float v)
        {
            idx %= buf.Length;
            if (idx < 0) idx += buf.Length;
            buf[idx] += v;
        }

        private static float Humanize(float t) => Mathf.Max(0f, t + Rand() * 0.004f);

        // ---------------------------------------------------------------- plucked strings (Karplus-Strong)

        private static readonly float[] Ring = new float[4096];
        private const int RingMask = 4095;

        private static void Pluck(Ctx c, float start, float dur, float freq, float vol, float bright, float decaySeconds)
        {
            float period = Rate / freq;
            int s0 = Mathf.RoundToInt(start * Rate);
            int n = Mathf.RoundToInt(dur * Rate);
            int fill = Mathf.CeilToInt(period) + 2;
            float lp = 0f;
            for (int i = 0; i < Ring.Length; i++) Ring[i] = 0f;
            for (int i = 0; i < fill; i++)
            {
                lp += bright * (Rand() - lp);
                Ring[i] = lp;
            }
            float g = Mathf.Pow(0.001f, 1f / Mathf.Max(0.05f, decaySeconds * freq));
            float d = period - 0.5f;
            int di = Mathf.FloorToInt(d);
            float frac = d - di;
            int release = Mathf.Max(1, Mathf.RoundToInt(0.025f * Rate));
            for (int i = 0; i < n; i++)
            {
                int w = fill + i;
                float a = (1f - frac) * Ring[(w - di) & RingMask] + frac * Ring[(w - di - 1) & RingMask];
                float b = (1f - frac) * Ring[(w - di - 1) & RingMask] + frac * Ring[(w - di - 2) & RingMask];
                float y = g * 0.5f * (a + b);
                Ring[w & RingMask] = y;
                float env = Mathf.Min(1f, (n - i) / (float)release);
                Add(c.Buf, s0 + i, Ring[i & RingMask] * vol * env);
            }
        }

        private static void Strum(Ctx c, float t, List<int> notes, float dur, float vol, bool down)
        {
            t = Humanize(t);
            for (int i = 0; i < notes.Count; i++)
            {
                int k = down ? i : notes.Count - 1 - i;
                Pluck(c, t + i * 0.007f, dur, Midi(notes[k]), vol * (down ? 1f : 0.8f), 0.85f, 1.1f);
            }
        }

        private static void Bass(Ctx c, float t, int midi, float dur, float vol) =>
            Pluck(c, Humanize(t), dur, Midi(midi), vol, 0.45f, 2.2f);

        // ---------------------------------------------------------------- winds, brass, voice

        private static void PlayVoice(Ctx c, Voice voice, float t, float dur, int midi, float vol)
        {
            t = Humanize(t);
            float f = Midi(midi);
            switch (voice)
            {
                case Voice.Flute: Flute(c, t, dur, f, vol); break;
                case Voice.Clarinet: Clarinet(c, t, dur, f, vol); break;
                case Voice.Brass: Brass(c, t, dur, f, vol, 700f, 2600f, 0f); break;
                case Voice.MutedBrass: Brass(c, t, dur, f, vol, 900f, 1800f, 1400f); break;
                case Voice.Singer: Singer(c, t, dur, f, vol); break;
            }
        }

        private static float Envelope(float t, float dur, float attack, float release, float sustain = 0.85f)
        {
            float a = Mathf.Min(1f, t / attack);
            float body = Mathf.Lerp(1f, sustain, Mathf.Clamp01((t - attack) / 0.25f));
            float r = Mathf.Clamp01((dur - t) / release);
            return a * body * r;
        }

        private static void Flute(Ctx c, float start, float dur, float f, float vol)
        {
            int s0 = Mathf.RoundToInt(start * Rate);
            float total = dur + 0.06f;
            int n = Mathf.RoundToInt(total * Rate);
            float phase = 0f, breath = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float vib = 1f + 0.005f * Mathf.Sin(2f * Mathf.PI * 5.2f * t) * Mathf.Clamp01((t - 0.15f) / 0.2f);
                phase += f * vib / Rate;
                float p = 2f * Mathf.PI * phase;
                float v = Mathf.Sin(p) + 0.18f * Mathf.Sin(2f * p) + 0.06f * Mathf.Sin(3f * p);
                breath += 0.25f * (Rand() - breath);
                v += breath * (0.25f + 0.6f * Mathf.Exp(-t * 25f));
                Add(c.Buf, s0 + i, v * Envelope(t, total, 0.03f, 0.06f) * vol);
            }
        }

        private static void Clarinet(Ctx c, float start, float dur, float f, float vol)
        {
            int s0 = Mathf.RoundToInt(start * Rate);
            float total = dur + 0.05f;
            int n = Mathf.RoundToInt(total * Rate);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float vib = 1f + 0.003f * Mathf.Sin(2f * Mathf.PI * 4.8f * t) * Mathf.Clamp01((t - 0.2f) / 0.2f);
                phase += f * vib / Rate;
                float p = 2f * Mathf.PI * phase;
                float v = Mathf.Sin(p) + 0.4f * Mathf.Sin(3f * p) + (f * 5f < 10000f ? 0.22f * Mathf.Sin(5f * p) : 0f)
                          + (f * 7f < 10000f ? 0.1f * Mathf.Sin(7f * p) : 0f);
                Add(c.Buf, s0 + i, v * 0.7f * Envelope(t, total, 0.025f, 0.05f, 0.9f) * vol);
            }
        }

        /// <summary>Saw through a swelling low-pass; with <paramref name="mute"/> &gt; 0 a resonant band-pass (cup mute).</summary>
        private static void Brass(Ctx c, float start, float dur, float f, float vol, float cutoffStart, float cutoffPeak, float mute)
        {
            int s0 = Mathf.RoundToInt(start * Rate);
            float total = dur + 0.06f;
            int n = Mathf.RoundToInt(total * Rate);
            float phase = 0f, lp1 = 0f, lp2 = 0f;
            var bp = mute > 0f ? new Resonator(mute, 2.5f) : null;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                phase += f * (1f + 0.003f * Mathf.Sin(2f * Mathf.PI * 5f * t)) / Rate;
                float saw = 2f * (phase - Mathf.Floor(phase)) - 1f;
                float cutoff = Mathf.Lerp(cutoffStart, cutoffPeak, Mathf.Clamp01(t / 0.08f)) * Mathf.Lerp(1f, 0.75f, Mathf.Clamp01(t / 0.6f));
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
                lp1 += a * (saw - lp1);
                lp2 += a * (lp1 - lp2);
                float v = bp != null ? bp.Process(lp2) * 1.6f + lp2 * 0.15f : lp2;
                Add(c.Buf, s0 + i, v * Envelope(t, total, 0.04f, 0.06f, 0.8f) * vol);
            }
        }

        /// <summary>Wordless "aah" voice: saw through two formant resonators, with vibrato (the Cantador's call).</summary>
        private static void Singer(Ctx c, float start, float dur, float f, float vol)
        {
            int s0 = Mathf.RoundToInt(start * Rate);
            float total = dur + 0.08f;
            int n = Mathf.RoundToInt(total * Rate);
            float phase = 0f;
            var f1 = new Resonator(720f, 6f);
            var f2 = new Resonator(1150f, 8f);
            var f3 = new Resonator(2600f, 10f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float vib = 1f + 0.011f * Mathf.Sin(2f * Mathf.PI * 5.6f * t) * Mathf.Clamp01((t - 0.1f) / 0.25f);
                float scoop = 1f - 0.02f * Mathf.Exp(-t * 30f); // slight scoop into the note
                phase += f * vib * scoop / Rate;
                float saw = 2f * (phase - Mathf.Floor(phase)) - 1f;
                float v = f1.Process(saw) + 0.6f * f2.Process(saw) + 0.2f * f3.Process(saw);
                Add(c.Buf, s0 + i, v * 1.4f * Envelope(t, total, 0.045f, 0.08f, 0.9f) * vol);
            }
        }

        private static void Piano(Ctx c, float start, float dur, int midi, float vol)
        {
            start = Humanize(start);
            float f = Midi(midi);
            int s0 = Mathf.RoundToInt(start * Rate);
            float total = dur + 0.12f;
            int n = Mathf.RoundToInt(total * Rate);
            float baseDecay = 1.5f + (midi - 48) * 0.05f;
            for (int k = 1; k <= 6; k++)
            {
                float fk = f * k * (1f + 0.0004f * k * k);
                if (fk > 10000f) break;
                float amp = 1f / Mathf.Pow(k, 1.4f);
                float decay = baseDecay * (1f + 0.5f * k);
                float phase = 0f, phase2 = 0f;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    phase += fk / Rate;
                    phase2 += fk * 1.0015f / Rate;
                    float v = 0.5f * (Mathf.Sin(2f * Mathf.PI * phase) + Mathf.Sin(2f * Mathf.PI * phase2));
                    float env = Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-decay * t) * Mathf.Clamp01((total - t) / 0.1f);
                    Add(c.Buf, s0 + i, v * amp * env * vol);
                }
            }
            Noise(c, start, 0.02f, 3000f, 500f, vol * 0.25f, 150f); // hammer
        }

        // ---------------------------------------------------------------- percussion

        private static void Tone(Ctx c, float start, float dur, float f0, float f1, float vol, float decay)
        {
            int s0 = Mathf.RoundToInt(start * Rate);
            int n = Mathf.RoundToInt(dur * Rate);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float k = n > 1 ? i / (float)(n - 1) : 0f;
                phase += Mathf.Lerp(f0, f1, Mathf.Sqrt(k)) / Rate;
                float env = Mathf.Min(1f, t / 0.0015f) * Mathf.Exp(-decay * t) * Mathf.Clamp01((n - i) / (Rate * 0.005f));
                Add(c.Buf, s0 + i, Mathf.Sin(2f * Mathf.PI * phase) * env * vol);
            }
        }

        private static void Noise(Ctx c, float start, float dur, float lp, float hp, float vol, float decay)
        {
            int s0 = Mathf.RoundToInt(start * Rate);
            int n = Mathf.RoundToInt(dur * Rate);
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Min(lp, 10500f) / Rate);
            float ah = 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Max(1f, hp) / Rate);
            float low = 0f, lowHp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                low += a * (Rand() - low);
                lowHp += ah * (low - lowHp);
                float v = hp > 0f ? low - lowHp : low;
                float env = Mathf.Min(1f, t / 0.001f) * Mathf.Exp(-decay * t) * Mathf.Clamp01((n - i) / (Rate * 0.003f));
                Add(c.Buf, s0 + i, v * env * vol);
            }
        }

        private static void Surdo(Ctx c, float t, bool open, float vol)
        {
            float f = c.Song.SurdoHz;
            Tone(c, t, open ? 0.6f : 0.15f, f * 1.25f, f * 0.9f, vol, open ? 5.5f : 22f);
            Noise(c, t, 0.03f, 900f, 60f, vol * 0.35f, 80f);
        }

        private static void Jingle(Ctx c, float t, float vol)
        {
            t = Humanize(t);
            Noise(c, t, 0.07f, 10500f, 5500f, vol * 2.2f, 38f);
            Tone(c, t, 0.06f, 6800f, 6800f, vol * 0.15f, 50f);
        }

        private static void PandeiroBass(Ctx c, float t, float vol) => Tone(c, Humanize(t), 0.18f, 120f, 85f, vol, 18f);

        private static void PandeiroSlap(Ctx c, float t, float vol)
        {
            t = Humanize(t);
            Noise(c, t, 0.06f, 4500f, 900f, vol * 1.8f, 55f);
            Tone(c, t, 0.06f, 340f, 280f, vol * 0.5f, 45f);
        }

        private static void Tamborim(Ctx c, float t, float vol)
        {
            t = Humanize(t);
            Tone(c, t, 0.09f, 640f, 560f, vol, 38f);
            Noise(c, t, 0.015f, 6000f, 1500f, vol * 0.8f, 140f);
        }

        /// <summary>Plate-and-knife scrape, the metallic shuffle heard on 1930s samba records.</summary>
        private static void PratoFaca(Ctx c, float t, float vol)
        {
            t = Humanize(t);
            Noise(c, t, 0.06f, 8000f, 3000f, vol * 2f, 45f);
            Tone(c, t, 0.05f, 3150f, 3100f, vol * 0.2f, 60f);
            Tone(c, t, 0.05f, 4420f, 4400f, vol * 0.12f, 70f);
        }

        private static void Ganza(Ctx c, float t, float vol) => Noise(c, Humanize(t), 0.07f, 9000f, 3500f, vol * 2f, 40f);

        private static void Palmas(Ctx c, float t, float vol)
        {
            t = Humanize(t);
            foreach (float o in new[] { 0f, 0.006f, 0.012f })
                Noise(c, t + o + Rand() * 0.002f, 0.05f, 3500f, 900f, vol * 1.5f, 70f);
        }

        private static void Atabaque(Ctx c, float t, float f, float vol)
        {
            t = Humanize(t);
            Tone(c, t, 0.35f, f * 1.15f, f, vol, f > 140f ? 16f : 9f);
            Noise(c, t, 0.02f, 2500f, 300f, vol * 0.3f, 120f);
        }

        private static void Agogo(Ctx c, float t, bool high, float vol)
        {
            t = Humanize(t);
            float f = high ? 1180f : 880f;
            Tone(c, t, 0.25f, f, f, vol, 14f);
            Tone(c, t, 0.2f, f * 2.7f, f * 2.7f, vol * 0.3f, 22f);
        }

        /// <summary>Two-pole band-pass resonator (RBJ constant-peak), used for formants and the cup mute.</summary>
        private sealed class Resonator
        {
            private readonly float _b0, _b2, _a1, _a2;
            private float _x1, _x2, _y1, _y2;

            public Resonator(float freq, float q)
            {
                float w = 2f * Mathf.PI * Mathf.Min(freq, Rate * 0.45f) / Rate;
                float alpha = Mathf.Sin(w) / (2f * q);
                float a0 = 1f + alpha;
                _b0 = alpha / a0;
                _b2 = -alpha / a0;
                _a1 = -2f * Mathf.Cos(w) / a0;
                _a2 = (1f - alpha) / a0;
            }

            public float Process(float x)
            {
                float y = _b0 * x + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
                _x2 = _x1; _x1 = x;
                _y2 = _y1; _y1 = y;
                return y;
            }
        }

        // ================================================================== mastering (circular, so the loop stays seamless)

        private static void Reverb(float[] buf, float wet)
        {
            int[] combs = { 557, 593, 641, 677 };
            int[] allpasses = { 113, 277 };
            var dry = (float[])buf.Clone();
            var outBuf = new float[buf.Length];
            var combBuf = new float[combs.Length][];
            var combIdx = new int[combs.Length];
            var combLp = new float[combs.Length];
            for (int k = 0; k < combs.Length; k++) combBuf[k] = new float[combs[k]];
            var apBuf = new float[allpasses.Length][];
            var apIdx = new int[allpasses.Length];
            for (int k = 0; k < allpasses.Length; k++) apBuf[k] = new float[allpasses[k]];

            for (int pass = 0; pass < 2; pass++) // first pass warms the tails, second pass writes
            {
                for (int i = 0; i < dry.Length; i++)
                {
                    float x = dry[i] * 0.25f, sum = 0f;
                    for (int k = 0; k < combs.Length; k++)
                    {
                        float y = combBuf[k][combIdx[k]];
                        combLp[k] += 0.35f * (y - combLp[k]);
                        combBuf[k][combIdx[k]] = x + combLp[k] * 0.72f;
                        combIdx[k] = (combIdx[k] + 1) % combs[k];
                        sum += y;
                    }
                    for (int k = 0; k < allpasses.Length; k++)
                    {
                        float bufOut = apBuf[k][apIdx[k]];
                        float v = -sum + bufOut;
                        apBuf[k][apIdx[k]] = sum + bufOut * 0.5f;
                        apIdx[k] = (apIdx[k] + 1) % allpasses[k];
                        sum = v;
                    }
                    if (pass == 1) outBuf[i] = sum;
                }
            }
            for (int i = 0; i < buf.Length; i++) buf[i] = dry[i] + outBuf[i] * wet;
        }

        /// <summary>Gentle period colour: band-limit to roughly 140 Hz-5.5 kHz and add a faint crackle.</summary>
        private static void AgeTo78(float[] buf)
        {
            float aLp = 1f - Mathf.Exp(-2f * Mathf.PI * 5500f / Rate);
            float aHp = 1f - Mathf.Exp(-2f * Mathf.PI * 140f / Rate);
            float lp1 = 0f, lp2 = 0f, hp = 0f;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < buf.Length; i++)
                {
                    lp1 += aLp * (buf[i] - lp1);
                    lp2 += aLp * (lp1 - lp2);
                    hp += aHp * (lp2 - hp);
                    if (pass == 1) buf[i] = lp2 - hp;
                }
            }
            float rms = Rms(buf);
            int clicks = Mathf.RoundToInt(buf.Length / (float)Rate * 2.5f);
            for (int k = 0; k < clicks; k++)
            {
                int at = Mathf.Abs(Mathf.RoundToInt(Rand01() * (buf.Length - 1)));
                float amp = rms * (0.15f + 0.35f * Rand01());
                Add(buf, at, amp);
                Add(buf, at + 1, -amp * 0.6f);
            }
            float hiss = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                hiss += 0.3f * (Rand() - hiss);
                buf[i] += hiss * rms * 0.02f;
            }
        }

        private static float Rms(float[] buf)
        {
            double sum = 0;
            foreach (float v in buf) sum += v * v;
            return (float)System.Math.Sqrt(sum / Mathf.Max(1, buf.Length));
        }

        /// <summary>RMS-normalize, soft-clip, then cap the peak, so the four tracks sit at a similar loudness.</summary>
        private static void Master(float[] buf)
        {
            float rms = Rms(buf);
            if (rms <= 0f) return;
            float gain = 0.16f / rms;
            float drive = (float)System.Math.Tanh(1.3);
            float peak = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                buf[i] = (float)System.Math.Tanh(buf[i] * gain * 1.3f) / drive;
                peak = Mathf.Max(peak, Mathf.Abs(buf[i]));
            }
            if (peak > 0.89f)
                for (int i = 0; i < buf.Length; i++) buf[i] *= 0.89f / peak;
        }
    }
}
