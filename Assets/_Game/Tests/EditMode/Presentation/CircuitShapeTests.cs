using System.Collections.Generic;
using DontTrustYourLuck.Domain;
using DontTrustYourLuck.Presentation.Board;
using NUnit.Framework;
using UnityEngine;

namespace DontTrustYourLuck.Tests.Presentation
{
    /// <summary>City circuit outlines (presentation only): Oaxaca plaza rectangle and Fortaleza bay "D".</summary>
    public class CircuitShapeTests
    {
        private const int Laps = 30;

        [Test]
        public void Plaza_KeepsTheOriginalRectangle()
        {
            Assert.AreEqual(new Vector3(-5.5f, 0f, -3f), BoardView.CircuitPosition(1, Laps));
            Assert.AreEqual(BoardView.CircuitPosition(7, Laps), BoardView.CircuitPose(7, Laps, CircuitShape.Plaza).position);
            Assert.AreEqual(Quaternion.identity, BoardView.CircuitPose(12, Laps, CircuitShape.Plaza).rotation);
        }

        [Test]
        public void Bay_StartsOnTheFrontLeftCorner_WithTheSpurInFront()
        {
            var first = BoardView.CircuitPose(1, Laps, CircuitShape.Bay).position;
            Assert.AreEqual(-BayCircuit.HalfWidth, first.x, 1e-3f);
            Assert.AreEqual(BayCircuit.FrontZ, first.z, 1e-3f);
            var start = BoardView.CircuitPose(0, Laps, CircuitShape.Bay).position;
            Assert.AreEqual(first.x, start.x, 1e-3f);
            Assert.Less(start.z, first.z, "start spur sits in front of tile 1");
        }

        [Test]
        public void Bay_TilesAreEvenlySpaced_AndNeverOverlap()
        {
            var positions = new List<Vector3>();
            for (int i = 1; i <= Laps; i++) positions.Add(BoardView.CircuitPose(i, Laps, CircuitShape.Bay).position);
            for (int i = 0; i < Laps; i++)
            {
                float step = Vector3.Distance(positions[i], positions[(i + 1) % Laps]);
                Assert.Greater(step, 1.05f, $"tiles {i + 1} and {(i + 1) % Laps + 1} are too close");
                Assert.Less(step, 1.25f, $"gap after tile {i + 1}");
                for (int j = i + 2; j < Laps; j++)
                {
                    if (i == 0 && j == Laps - 1) continue; // neighbours across the loop seam
                    Assert.Greater(Vector3.Distance(positions[i], positions[j]), 1.4f, $"tiles {i + 1} and {j + 1} overlap");
                }
            }
        }

        [Test]
        public void Bay_FrontIsStraight_BackIsRounded()
        {
            for (int i = 1; i <= 10; i++)
                Assert.AreEqual(BayCircuit.FrontZ, BoardView.CircuitPose(i, Laps, CircuitShape.Bay).position.z, 1e-3f, $"tile {i} on the promenade");
            var top = BoardView.CircuitPose(21, Laps, CircuitShape.Bay).position;
            Assert.AreEqual(BayCircuit.FrontZ + BayCircuit.Depth, top.z, 0.05f, "middle of the arc is the deepest point");
        }

        [Test]
        public void Bay_TileYawStaysReadable()
        {
            for (int i = 0; i <= Laps; i++)
            {
                float yaw = BoardView.CircuitPose(i, Laps, CircuitShape.Bay).rotation.eulerAngles.y;
                if (yaw > 180f) yaw -= 360f;
                Assert.LessOrEqual(Mathf.Abs(yaw), 45.01f, $"tile {i} number would read sideways or upside down");
            }
        }

        [Test]
        public void SetCircuitShape_RelaysTheTiles_OnlyInCircuitMode()
        {
            var go = new GameObject("BoardView_Shape");
            try
            {
                var board = go.AddComponent<BoardView>();
                board.Build(MatchRulesConfig.Default.TileCount);
                var zigzag = board.GetTile(5).transform.localPosition;
                board.SetCircuitShape(CircuitShape.Bay);
                Assert.AreEqual(zigzag, board.GetTile(5).transform.localPosition, "zigzag layout is not touched");

                board.SetCircuit(true);
                Assert.AreEqual(BoardView.CircuitPose(5, Laps, CircuitShape.Bay).position, board.GetTile(5).transform.localPosition);
                board.SetCircuitShape(CircuitShape.Plaza);
                Assert.AreEqual(BoardView.CircuitPosition(5, Laps), board.GetTile(5).transform.localPosition);
                Assert.AreEqual(Quaternion.identity, board.GetTile(20).transform.localRotation);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
