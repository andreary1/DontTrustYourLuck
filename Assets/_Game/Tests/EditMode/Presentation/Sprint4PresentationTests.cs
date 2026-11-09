using System.Collections.Generic;
using DontTrustYourLuck.Domain;
using DontTrustYourLuck.Domain.Match;
using DontTrustYourLuck.Presentation.Audio;
using DontTrustYourLuck.Presentation.Board;
using DontTrustYourLuck.Presentation.Characters;
using NUnit.Framework;
using UnityEngine;

namespace DontTrustYourLuck.Tests.Presentation
{
    /// <summary>Sprint 4: audio settings, event -> sound mapping, camera safe area, standee pieces, board markers.</summary>
    public class Sprint4PresentationTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }
            _created.Clear();
        }

        private T Track<T>(T o) where T : Object
        {
            _created.Add(o);
            return o;
        }

        // ------------------------------------------------------------------ audio settings

        private sealed class MemoryStore : IAudioSettingsStore
        {
            public bool Has;
            public float Master = 1f, Music = 0.7f, Sfx = 0.9f;
            public bool Muted;
            public int Saves;

            public bool TryLoad(out float master, out float music, out float sfx, out bool muted)
            {
                master = Master; music = Music; sfx = Sfx; muted = Muted;
                return Has;
            }

            public void Save(float master, float music, float sfx, bool muted)
            {
                Has = true; Master = master; Music = music; Sfx = sfx; Muted = muted; Saves++;
            }
        }

        [Test]
        public void AudioSettings_Defaults_WhenStoreIsEmpty()
        {
            var model = new AudioSettingsModel(new MemoryStore());
            Assert.AreEqual(AudioSettingsModel.DefaultMaster, model.Master);
            Assert.AreEqual(AudioSettingsModel.DefaultMusic, model.Music);
            Assert.AreEqual(AudioSettingsModel.DefaultSfx, model.Sfx);
            Assert.IsFalse(model.Muted);
        }

        [Test]
        public void AudioSettings_LoadsAndClampsStoredValues()
        {
            var model = new AudioSettingsModel(new MemoryStore { Has = true, Master = 2f, Music = -1f, Sfx = 0.25f, Muted = true });
            Assert.AreEqual(1f, model.Master);
            Assert.AreEqual(0f, model.Music);
            Assert.AreEqual(0.25f, model.Sfx);
            Assert.IsTrue(model.Muted);
        }

        [Test]
        public void AudioSettings_EffectiveVolumes_CombineMasterAndMute()
        {
            var model = new AudioSettingsModel();
            model.SetMaster(0.5f);
            model.SetMusic(0.8f);
            model.SetSfx(0.6f);
            Assert.AreEqual(0.4f, model.EffectiveMusic, 1e-5f);
            Assert.AreEqual(0.3f, model.EffectiveSfx, 1e-5f);
            model.SetMuted(true);
            Assert.AreEqual(0f, model.EffectiveMusic);
            Assert.AreEqual(0f, model.EffectiveSfx);
            model.ToggleMute();
            Assert.IsFalse(model.Muted);
            Assert.AreEqual(0.4f, model.EffectiveMusic, 1e-5f);
        }

        [Test]
        public void AudioSettings_ChangesRaiseEventAndPersist()
        {
            var store = new MemoryStore();
            var model = new AudioSettingsModel(store);
            int changed = 0;
            model.Changed += () => changed++;
            model.SetSfx(0.2f);
            model.SetMuted(true);
            Assert.AreEqual(2, changed);
            Assert.IsTrue(store.Has);
            Assert.AreEqual(0.2f, store.Sfx, 1e-5f);
            Assert.IsTrue(store.Muted);
        }

        // ------------------------------------------------------------------ event -> sound mapping

        private static List<SfxId> Map(MatchEvent e)
        {
            var list = new List<SfxId>();
            AudioEventRouter.MapEvent(e, list);
            return list;
        }

        [Test]
        public void MapEvent_CoversCoreMatchEvents()
        {
            CollectionAssert.AreEqual(new[] { SfxId.LuckyChime }, Map(new MatchStartedEvent(21, new[] { 1, 2 })));
            CollectionAssert.AreEqual(new[] { SfxId.DangerReveal }, Map(new UnluckyNumberDrawnEvent(1, 6)));
            CollectionAssert.AreEqual(new[] { SfxId.TurnChange }, Map(new TurnChangedEvent(1, 1)));
            CollectionAssert.AreEqual(new[] { SfxId.DiceRoll }, Map(new DiceRolledEvent(1, new DiceRoll(new[] { 2, 5 }))));
            CollectionAssert.AreEqual(new[] { SfxId.DieChoose }, Map(new DieSelectedEvent(1, 0, 2)));
            CollectionAssert.AreEqual(new[] { SfxId.Elimination }, Map(new PlayerEliminatedEvent(2, 6, EliminationReason.UnluckyTile)));
            CollectionAssert.AreEqual(new[] { SfxId.Victory }, Map(new PlayerWonEvent(1, MatchEndReason.ExactLucky)));
        }

        [Test]
        public void MapEvent_CardEffects()
        {
            CollectionAssert.AreEqual(new[] { SfxId.Swap }, Map(new PlayerPositionsSwappedEvent(1, 2, 3, 4)));
            CollectionAssert.AreEqual(new[] { SfxId.Retreat }, Map(new PlayerRetreatedEvent(1, 5, 2)));
            CollectionAssert.AreEqual(new[] { SfxId.ShieldUp }, Map(new ShieldActivatedEvent(1)));
            CollectionAssert.AreEqual(new[] { SfxId.ShieldBlock }, Map(new ShieldConsumedEvent(1, true, 6)));
            CollectionAssert.IsEmpty(Map(new ShieldConsumedEvent(1, false, 6)), "an unused shield expiring is silent");
        }

        [Test]
        public void MapEvent_MovementIsNotMapped_StepsFollowTheHopAnimation()
        {
            CollectionAssert.IsEmpty(Map(new RoundStartedEvent(2)));
        }

        // ------------------------------------------------------------------ camera safe area

        [Test]
        public void OffCenterExtents_FullScreenSafeArea_IsSymmetric()
        {
            var e = BoardCameraRig.ComputeOffCenterExtents(new Rect(0f, 0f, 1f, 1f), 0.4f, 0.3f);
            Assert.AreEqual(-0.4f, e.x, 1e-5f);
            Assert.AreEqual(0.4f, e.y, 1e-5f);
            Assert.AreEqual(-0.3f, e.z, 1e-5f);
            Assert.AreEqual(0.3f, e.w, 1e-5f);
        }

        [Test]
        public void OffCenterExtents_SafeAreaMapsExactlyOntoItsScreenRect()
        {
            var safe = new Rect(0.2f, 0.25f, 0.6f, 0.6f);
            const float halfW = 0.3f, halfH = 0.2f;
            var e = BoardCameraRig.ComputeOffCenterExtents(safe, halfW, halfH);
            // Screen x in [0,1] maps linearly to [left, right]; the safe edges must land on -halfW and +halfW.
            float SafeLeft() => Mathf.Lerp(e.x, e.y, safe.xMin);
            float SafeRight() => Mathf.Lerp(e.x, e.y, safe.xMax);
            float SafeBottom() => Mathf.Lerp(e.z, e.w, safe.yMin);
            float SafeTop() => Mathf.Lerp(e.z, e.w, safe.yMax);
            Assert.AreEqual(-halfW, SafeLeft(), 1e-5f);
            Assert.AreEqual(halfW, SafeRight(), 1e-5f);
            Assert.AreEqual(-halfH, SafeBottom(), 1e-5f);
            Assert.AreEqual(halfH, SafeTop(), 1e-5f);
        }

        // ------------------------------------------------------------------ standee pieces

        private CharacterDefinitionAsset Character(int w, int h)
        {
            var tex = Track(new Texture2D(w, h));
            var c = Track(ScriptableObject.CreateInstance<CharacterDefinitionAsset>());
            c.Configure("test", "Test", tex, null, Color.white, 1f);
            return c;
        }

        [Test]
        public void Piece_SetCharacter_ScalesCardToCutoutAspect()
        {
            var piece = PlayerPieceView.CreateDefault();
            Track(piece.gameObject);
            piece.SetCharacter(Character(96, 128));
            var card = piece.transform.Find("Visual/Card");
            Assert.IsNotNull(card, "standee hierarchy: Root/Visual/Card");
            Assert.AreEqual(0.75f, card.localScale.x, 1e-4f);
            Assert.AreEqual(1f, card.localScale.y, 1e-4f);
            Assert.AreEqual("test", piece.Character.Id);
        }

        [Test]
        public void Piece_VisualStates_NeverMoveTheLogicalRoot()
        {
            var piece = PlayerPieceView.CreateDefault();
            Track(piece.gameObject);
            piece.transform.position = new Vector3(1f, 0.15f, 2f);
            foreach (var state in new[] { PieceVisualState.Eliminated, PieceVisualState.Winner, PieceVisualState.Normal })
            {
                piece.SetVisualState(state);
                Assert.AreEqual(new Vector3(1f, 0.15f, 2f), piece.transform.position, state.ToString());
                Assert.AreEqual(Quaternion.identity, piece.transform.rotation, state.ToString());
            }
        }

        [Test]
        public void Piece_EliminatedCardLiesDown_WinnerGrows()
        {
            var piece = PlayerPieceView.CreateDefault();
            Track(piece.gameObject);
            piece.SetVisualState(PieceVisualState.Eliminated);
            Assert.Greater(Quaternion.Angle(Quaternion.identity, piece.Visual.localRotation), 60f);
            piece.SetVisualState(PieceVisualState.Winner);
            Assert.Greater(piece.Visual.localScale.y, 1.1f);
            piece.SetVisualState(PieceVisualState.Normal);
            Assert.AreEqual(1f, piece.Visual.localScale.y, 1e-3f);
        }

        [Test]
        public void Piece_ShieldAura_TogglesAndClearsWhenEliminated()
        {
            var piece = PlayerPieceView.CreateDefault();
            Track(piece.gameObject);
            Assert.IsFalse(piece.IsShielded);
            piece.SetShielded(true);
            Assert.IsTrue(piece.IsShielded);
            piece.SetVisualState(PieceVisualState.Eliminated);
            Assert.IsFalse(piece.IsShielded);
        }

        // ------------------------------------------------------------------ board markers / beyond-lucky tiles

        private BoardView Board(out Material even, out Material beyond)
        {
            var go = Track(new GameObject("Board"));
            var board = go.AddComponent<BoardView>();
            even = Track(new Material(Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Standard")));
            beyond = Track(new Material(even));
            var lucky = Track(new Material(even));
            var unlucky = Track(new Material(even));
            board.Configure(null, new BoardTileMaterials { even = even, odd = even, start = even, lucky = lucky, unlucky = unlucky, beyondLucky = beyond }, 31);
            board.Build(31);
            return board;
        }

        [Test]
        public void Board_TilesBeyondLucky_UsePatternedMaterial_AndKeepTheirIndex()
        {
            var board = Board(out var even, out var beyond);
            board.SetHighlights(20, 7);
            for (int i = 0; i < board.TileCount; i++)
            {
                Assert.AreEqual(i, board.Tiles[i].Index);
                var mat = board.Tiles[i].SurfaceRenderer.sharedMaterial;
                if (i > 20) Assert.AreSame(beyond, mat, $"tile {i}");
                else Assert.AreNotSame(beyond, mat, $"tile {i}");
            }
            Assert.AreEqual(TileHighlight.Lucky, board.Tiles[20].Highlight);
            Assert.AreEqual(TileHighlight.Unlucky, board.Tiles[7].Highlight);
        }

        [Test]
        public void Board_Markers_FollowHighlightsAndHideWhenCleared()
        {
            var board = Board(out _, out _);
            var sun = Track(new GameObject("Sun")).transform;
            var eye = Track(new GameObject("Eye")).transform;
            board.SetMarkers(sun, eye);
            board.SetHighlights(12, 3);
            Assert.IsTrue(sun.gameObject.activeSelf);
            Assert.IsTrue(eye.gameObject.activeSelf);
            var t12 = board.Tiles[12].transform.position;
            var t3 = board.Tiles[3].transform.position;
            Assert.AreEqual(t12.x, sun.position.x, 1e-4f);
            Assert.AreEqual(t12.z, sun.position.z, 1e-4f);
            Assert.Greater(sun.position.y, t12.y);
            Assert.AreEqual(t3.x, eye.position.x, 1e-4f);
            board.SetHighlights(12, -1);
            Assert.IsFalse(eye.gameObject.activeSelf);
            Assert.IsTrue(sun.gameObject.activeSelf);
        }
    }
}
