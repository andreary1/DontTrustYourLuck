using System.Collections.Generic;
using DontTrustYourLuck.Domain;
using DontTrustYourLuck.Presentation.Board;
using NUnit.Framework;
using UnityEngine;

namespace DontTrustYourLuck.Tests.Presentation
{
    public class BoardViewTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();
        private BoardView _board;

        [SetUp]
        public void SetUp()
        {
            var go = Track(new GameObject("BoardView_Test"));
            _board = go.AddComponent<BoardView>(); // Awake does not run in Edit Mode tests
            _board.Build(MatchRulesConfig.Default.TileCount);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _created.Clear();
        }

        private GameObject Track(GameObject go)
        {
            _created.Add(go);
            return go;
        }

        private PlayerPieceView CreatePiece(int id, int position)
        {
            var piece = PlayerPieceView.CreateDefault();
            Track(piece.gameObject);
            piece.Configure(id, Color.white);
            piece.SetLogicalPosition(position);
            return piece;
        }

        [Test]
        public void Build_Creates31Tiles()
        {
            Assert.AreEqual(31, _board.TileCount);
            Assert.IsTrue(_board.IsBuilt(31));
        }

        [Test]
        public void TileIndices_MatchLogicalIndices_AndHaveAnchors()
        {
            for (int i = 0; i < _board.TileCount; i++)
            {
                var tile = _board.GetTile(i);
                Assert.AreEqual(i, tile.Index);
                Assert.IsNotNull(tile.Anchor);
                Assert.AreSame(tile.Anchor, _board.GetTileAnchor(i));
                Assert.AreNotSame(tile.transform, tile.Anchor, "Each tile needs a dedicated anchor child.");
            }
        }

        [Test]
        public void Anchors_AreDistinct_AndPathIsContinuous()
        {
            float maxStep = Mathf.Max(_board.Layout.columnSpacing, _board.Layout.rowSpacing) + 0.01f;
            for (int i = 1; i < _board.TileCount; i++)
            {
                var a = _board.GetTileAnchor(i - 1).position;
                var b = _board.GetTileAnchor(i).position;
                float d = Vector3.Distance(a, b);
                Assert.Greater(d, 0.5f, $"Tiles {i - 1} and {i} overlap.");
                Assert.LessOrEqual(d, maxStep, $"Gap between tiles {i - 1} and {i} breaks the path.");
            }
        }

        [Test]
        public void GetTileAnchor_InvalidIndex_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _board.GetTileAnchor(-1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _board.GetTileAnchor(31));
        }

        [Test]
        public void Rebuild_DoesNotDuplicateTiles()
        {
            _board.Build(31);
            _board.Build(31);
            Assert.AreEqual(31, _board.TileCount);
            Assert.AreEqual(31, _board.GetComponentsInChildren<BoardTileView>().Length);
        }

        [Test]
        public void SinglePiece_StandsOnAnchor()
        {
            var piece = CreatePiece(1, 9);
            _board.PlacePieces(new[] { piece });
            Assert.That(Vector3.Distance(piece.transform.position, _board.GetTileAnchor(9).position), Is.LessThan(0.001f));
        }

        [Test]
        public void PiecesOnSameTile_DoNotFullyOverlap_AndStayNearTile()
        {
            var pieces = new[] { CreatePiece(1, 27), CreatePiece(2, 27), CreatePiece(3, 27), CreatePiece(4, 27) };
            _board.PlacePieces(pieces);

            var anchor = _board.GetTileAnchor(27).position;
            for (int i = 0; i < pieces.Length; i++)
            {
                Assert.Less(Vector3.Distance(pieces[i].transform.position, anchor), 0.5f, "Piece left its tile.");
                for (int j = i + 1; j < pieces.Length; j++)
                {
                    Assert.Greater(Vector3.Distance(pieces[i].transform.position, pieces[j].transform.position), 0.15f,
                        $"Pieces {i + 1} and {j + 1} overlap.");
                }
            }
        }

        [Test]
        public void PlacePieces_IsStableRegardlessOfListOrder()
        {
            var p1 = CreatePiece(1, 5);
            var p2 = CreatePiece(2, 5);
            _board.PlacePieces(new[] { p1, p2 });
            var first = p1.transform.position;
            _board.PlacePieces(new[] { p2, p1 });
            Assert.That(Vector3.Distance(first, p1.transform.position), Is.LessThan(0.001f));
        }

        [Test]
        public void SetHighlights_MarksLuckyAndUnluckyTiles()
        {
            _board.SetHighlights(30, 13);
            Assert.AreEqual(TileHighlight.Lucky, _board.GetTile(30).Highlight);
            Assert.AreEqual(TileHighlight.Unlucky, _board.GetTile(13).Highlight);
            Assert.AreEqual(TileHighlight.None, _board.GetTile(12).Highlight);

            _board.SetHighlights(30, 14);
            Assert.AreEqual(TileHighlight.None, _board.GetTile(13).Highlight);
        }

        [TestCase(16f / 9f)]
        [TestCase(4f / 3f)]
        [TestCase(21f / 9f)]
        public void CameraRig_FramesWholeBoard(float aspect)
        {
            var camGo = Track(new GameObject("Camera_Test"));
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 35f;
            cam.aspect = aspect;
            var rig = camGo.AddComponent<BoardCameraRig>();
            rig.Configure(_board, 55f);

            rig.Frame();

            for (int i = 0; i < _board.TileCount; i++)
            {
                var vp = cam.WorldToViewportPoint(_board.GetTileAnchor(i).position);
                Assert.Greater(vp.z, 0f, $"Tile {i} behind camera.");
                Assert.That(vp.x, Is.InRange(0f, 1f), $"Tile {i} outside horizontally ({vp.x}).");
                Assert.That(vp.y, Is.InRange(0f, 1f), $"Tile {i} outside vertically ({vp.y}).");
            }
        }

        [Test]
        public void MovementResult_MapsToBoardTiles()
        {
            var movement = new MovementService(MatchRulesConfig.Default);
            var result = movement.Move(28, 5, 30);
            foreach (int tile in result.Path)
            {
                Assert.AreEqual(tile, _board.GetTile(tile).Index);
            }
            var piece = CreatePiece(1, result.FinalPosition);
            _board.PlacePieces(new[] { piece });
            Assert.That(Vector3.Distance(piece.transform.position, _board.GetTileAnchor(27).position), Is.LessThan(0.001f));
        }
    }
}
