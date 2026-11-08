using System;
using UnityEngine;

namespace DontTrustYourLuck.Presentation.Board
{
    /// <summary>
    /// Serpentine layout: row 0 runs left-to-right nearest the camera, row 1 right-to-left, and so on.
    /// Only maps index -> local position; the logical order never depends on it.
    /// </summary>
    [Serializable]
    public sealed class ZigzagBoardLayout
    {
        [Min(1)] public int columns = 8;
        [Min(0.1f)] public float columnSpacing = 1.25f;
        [Min(0.1f)] public float rowSpacing = 1.6f;

        public int RowCount(int tileCount) => Mathf.Max(1, Mathf.CeilToInt(tileCount / (float)Mathf.Max(1, columns)));

        public Vector3 GetLocalPosition(int index, int tileCount)
        {
            int cols = Mathf.Max(1, columns);
            int rows = RowCount(tileCount);
            int row = index / cols;
            int col = index % cols;
            if (row % 2 == 1) col = cols - 1 - col;

            float x = (col - (cols - 1) * 0.5f) * columnSpacing;
            float z = (row - (rows - 1) * 0.5f) * rowSpacing;
            return new Vector3(x, 0f, z);
        }
    }
}
