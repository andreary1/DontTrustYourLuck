using System;
using System.Collections.Generic;
using UnityEngine;

namespace DontTrustYourLuck.Presentation.Board
{
    [Serializable]
    public sealed class BoardTileMaterials
    {
        public Material even;
        public Material odd;
        public Material start;
        public Material lucky;
        public Material unlucky;
        public Material connector;
        [Tooltip("Optional (Sprint 4): tiles above the lucky number (unreachable). Patterned, not just darker.")]
        public Material beyondLucky;
        [Tooltip("Tile number material with depth test (pieces in front hide the number). " +
                 "Null keeps the font's default material, which draws over everything.")]
        public Material label;
    }

    /// <summary>
    /// Generates and owns the visual tiles. Tile i in <see cref="Tiles"/> always has Index == i, matching the
    /// logical board. Provides anchors, multi-occupancy offsets and lucky/unlucky highlights.
    /// Highlights only display numbers decided by the rules layer.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] private BoardTileView tilePrefab;
        [Min(2)] [SerializeField] private int tileCount = 31;
        [SerializeField] private ZigzagBoardLayout layout = new ZigzagBoardLayout();
        [Tooltip("Loop outline in circuit mode (per city; BoardThemeSwitcher sets it).")]
        [SerializeField] private CircuitShape circuitShape = CircuitShape.Plaza;
        [SerializeField] private BoardTileMaterials materials = new BoardTileMaterials();
        [Tooltip("Distance from the anchor when several pieces share a tile.")]
        [Min(0f)] [SerializeField] private float pieceSlotRadius = 0.24f;
        [SerializeField] private List<BoardTileView> tiles = new List<BoardTileView>();
        [SerializeField] private List<GameObject> connectors = new List<GameObject>();
        [Header("Highlight markers (optional, Sprint 4)")]
        [Tooltip("Floating icon placed over the lucky tile (e.g. sun).")]
        [SerializeField] private Transform luckyMarker;
        [Tooltip("Floating icon placed over the unlucky tile (e.g. eye).")]
        [SerializeField] private Transform unluckyMarker;
        [Min(0f)] [SerializeField] private float markerHeight = 1.15f;

        private int _luckyIndex = -1;
        private int _unluckyIndex = -1;
        private readonly List<int> _unluckyZone = new List<int>();
        private bool _circuit;
        public bool IsCircuit => _circuit;

        public void SetCircuit(bool value)
        {
            if (_circuit == value) return;
            _circuit = value;
            EnsureBuilt(tileCount);
            if (value) LayoutCircuit();
        }

        /// <summary>Loop outline used in circuit mode (per city). Re-lays the tiles at once when the circuit is active.</summary>
        public CircuitShape Shape => circuitShape;

        public void SetCircuitShape(CircuitShape shape)
        {
            if (circuitShape == shape) return;
            circuitShape = shape;
            if (_circuit) LayoutCircuit();
        }

        private void LayoutCircuit()
        {
            // Reuse the authored tiles/materials. The start is a spur; 1..30 form the closed perimeter.
            for (int i = 0; i < tiles.Count; i++)
            {
                var pose = CircuitPose(i, tiles.Count - 1, circuitShape);
                tiles[i].transform.localPosition = pose.position;
                tiles[i].transform.localRotation = pose.rotation;
            }
            foreach (var connector in connectors) if (connector != null) DestroySafe(connector);
            connectors.Clear();
            for (int i = 1; i < tiles.Count; i++)
                connectors.Add(CreateConnector(tiles[i - 1].transform.localPosition, tiles[i].transform.localPosition, i));
            connectors.Add(CreateConnector(tiles[tiles.Count - 1].transform.localPosition, tiles[1].transform.localPosition, tiles.Count));
            ApplyHighlights();
        }

        /// <summary>Plaza (rectangle) circuit position, kept for callers that predate the city shapes.</summary>
        public static Vector3 CircuitPosition(int index, int length) => CircuitPose(index, length, CircuitShape.Plaza).position;

        /// <summary>Local tile pose of a circuit: index 0 is the start spur, 1..length run around the loop.</summary>
        public static Pose CircuitPose(int index, int length, CircuitShape shape) =>
            shape == CircuitShape.Bay ? BayCircuit.Pose(index, length) : new Pose(PlazaPosition(index, length), Quaternion.identity);

        private static Vector3 PlazaPosition(int index, int length)
        {
            // Equal arc spacing on a rectangular plaza; default 30 = 10 + 5 + 10 + 5 edges.
            float distance = (index == 0 ? 0 : index - 1) * 30f / length;
            Vector3 p;
            if (distance < 10) p = new Vector3(-5.5f + distance * 1.1f, 0, -3f);
            else if (distance < 15) p = new Vector3(5.5f, 0, -3f + (distance - 10) * 1.2f);
            else if (distance < 25) p = new Vector3(5.5f - (distance - 15) * 1.1f, 0, 3f);
            else p = new Vector3(-5.5f, 0, 3f - (distance - 25) * 1.2f);
            return index == 0 ? p + Vector3.back * 1.2f : p;
        }

        public IReadOnlyList<BoardTileView> Tiles => tiles;
        public int TileCount => tiles.Count;
        public ZigzagBoardLayout Layout => layout;
        public int LuckyIndex => _luckyIndex;
        public int UnluckyIndex => _unluckyIndex;
        /// <summary>Every highlighted unlucky tile (empty when only <see cref="UnluckyIndex"/> is set).</summary>
        public IReadOnlyList<int> UnluckyZone => _unluckyZone;
        public Transform LuckyMarker => luckyMarker;
        public Transform UnluckyMarker => unluckyMarker;

        /// <summary>Sprint 4 builder: markers are scene objects, not part of the tiles.</summary>
        public void SetMarkers(Transform lucky, Transform unlucky)
        {
            luckyMarker = lucky;
            unluckyMarker = unlucky;
        }

        public bool IsBuilt(int expectedTileCount)
        {
            if (tiles.Count != expectedTileCount) return false;
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i] == null || tiles[i].Index != i) return false;
            }
            return true;
        }

        public void Configure(BoardTileView prefab, BoardTileMaterials tileMaterials, int count)
        {
            tilePrefab = prefab;
            materials = tileMaterials ?? new BoardTileMaterials();
            tileCount = Mathf.Max(2, count);
        }

        private Material _labelInstance;
        private Font _labelFont;

        private void Awake()
        {
            EnsureBuilt(tileCount);
            ApplyLabelMaterial();
        }

        private void OnDestroy()
        {
            Font.textureRebuilt -= OnFontTextureRebuilt;
            if (_labelInstance != null) Destroy(_labelInstance);
        }

        /// <summary>
        /// Gives every tile number a depth-tested copy of <see cref="BoardTileMaterials.label"/> fed with the font atlas.
        /// The default TextMesh font material (GUI/Text Shader) ignores depth, so numbers drew over the pieces.
        /// </summary>
        private void ApplyLabelMaterial()
        {
            if (materials.label == null || tiles.Count == 0 || tiles[0] == null || tiles[0].Label == null) return;
            _labelFont = tiles[0].Label.font;
            if (_labelFont == null) return;
            _labelInstance = new Material(materials.label) { name = materials.label.name + " (Runtime)" };
            _labelInstance.mainTexture = _labelFont.material.mainTexture;
            foreach (var tile in tiles) if (tile != null) tile.SetLabelMaterial(_labelInstance);
            Font.textureRebuilt += OnFontTextureRebuilt;
        }

        private void OnFontTextureRebuilt(Font font)
        {
            if (font == _labelFont && _labelInstance != null) _labelInstance.mainTexture = font.material.mainTexture;
        }

        public void EnsureBuilt(int count)
        {
            if (!IsBuilt(count)) Build(count);
        }

        [ContextMenu("Rebuild Board")]
        private void RebuildFromInspector() => Build(tileCount);

        public void Build(int count)
        {
            if (count < 2) throw new ArgumentOutOfRangeException(nameof(count), count, "Board needs at least 2 tiles.");
            Clear();
            tileCount = count;

            for (int i = 0; i < count; i++)
            {
                var tile = CreateTile();
                tile.transform.localPosition = layout.GetLocalPosition(i, count);
                tile.transform.localRotation = Quaternion.identity;
                tile.Initialize(i);
                tiles.Add(tile);
            }

            for (int i = 1; i < count; i++)
            {
                connectors.Add(CreateConnector(tiles[i - 1].transform.localPosition, tiles[i].transform.localPosition, i));
            }

            ApplyHighlights();
            RecordEditorChanges();
        }

        /// <summary>
        /// Edit Mode only: script changes on prefab instances must be recorded, otherwise Unity reverts them
        /// to the prefab values when the scene is saved.
        /// </summary>
        public void RecordEditorChanges()
        {
#if UNITY_EDITOR
            if (Application.isPlaying) return;
            RecordHierarchy(gameObject);
            foreach (var tile in tiles)
            {
                if (tile != null) RecordHierarchy(tile.gameObject);
            }
#endif
        }

#if UNITY_EDITOR
        public static void RecordHierarchy(GameObject root)
        {
            if (!UnityEditor.PrefabUtility.IsPartOfPrefabInstance(root)) return;
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(component.gameObject);
            }
        }
#endif

        public void Clear()
        {
            foreach (var tile in tiles)
            {
                if (tile != null) DestroySafe(tile.gameObject);
            }
            foreach (var connector in connectors)
            {
                if (connector != null) DestroySafe(connector);
            }
            tiles.Clear();
            connectors.Clear();
        }

        public BoardTileView GetTile(int index)
        {
            if (index < 0 || index >= tiles.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Tile index must be in [0, {tiles.Count - 1}].");
            return tiles[index];
        }

        public Transform GetTileAnchor(int index) => GetTile(index).Anchor;

        /// <summary>World position for one of <paramref name="occupantCount"/> pieces sharing a tile.</summary>
        public Vector3 GetPieceWorldPosition(int tileIndex, int slot, int occupantCount)
        {
            var anchor = GetTileAnchor(tileIndex).position;
            if (occupantCount <= 1) return anchor;

            // A horizontal fan avoids one cutout hiding behind another at the lower camera angle.
            float spacing = Mathf.Min(pieceSlotRadius * 1.75f, 0.8f / Mathf.Max(1, occupantCount - 1));
            var offset = new Vector3((slot - (occupantCount - 1) * 0.5f) * spacing, 0f, 0f);
            return anchor + transform.rotation * offset;
        }

        /// <summary>
        /// Moves every piece to its logical tile. Pieces sharing a tile get distinct slots ordered by PlayerId,
        /// so placement is stable regardless of list order.
        /// </summary>
        public void PlacePieces(IReadOnlyList<PlayerPieceView> pieces)
        {
            if (pieces == null) return;

            var byTile = new Dictionary<int, List<PlayerPieceView>>();
            foreach (var piece in pieces)
            {
                if (piece == null) continue;
                int position = piece.LogicalPosition;
                if (position < 0 || position >= tiles.Count)
                {
                    Debug.LogError($"[BoardView] {piece.name} has invalid logical position {position}.", piece);
                    continue;
                }
                if (!byTile.TryGetValue(position, out var list))
                {
                    list = new List<PlayerPieceView>();
                    byTile.Add(position, list);
                }
                list.Add(piece);
            }

            foreach (var pair in byTile)
            {
                var occupants = pair.Value;
                occupants.Sort((a, b) => a.PlayerId.CompareTo(b.PlayerId));
                for (int slot = 0; slot < occupants.Count; slot++)
                {
                    occupants[slot].transform.localScale = Vector3.one * OccupantScale(occupants.Count);
                    occupants[slot].transform.position = GetPieceWorldPosition(pair.Key, slot, occupants.Count);
                }
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                foreach (var piece in pieces)
                {
                    if (piece != null) RecordHierarchy(piece.gameObject);
                }
            }
#endif
        }

        /// <summary>Horizontal offset (x, z) of the lucky/unlucky markers from the tile center: they sit on a tile corner.</summary>
        public static readonly Vector2 MarkerOffsetXZ = new Vector2(0.36f, -0.32f);

        public static float OccupantScale(int count) => count <= 1 ? 0.85f : count == 2 ? 0.52f : count == 3 ? 0.40f : 0.30f;

        /// <summary>Displays lucky/unlucky tiles. Pass -1 to clear one of them.</summary>
        public void SetHighlights(int luckyIndex, int unluckyIndex) => SetHighlights(luckyIndex, unluckyIndex, null);

        /// <summary>
        /// Displays the lucky tile and the unlucky zone. <paramref name="unluckyZone"/> lists every dangerous tile
        /// (null or empty: only <paramref name="unluckyIndex"/>); the marker stays on the zone center.
        /// </summary>
        public void SetHighlights(int luckyIndex, int unluckyIndex, IReadOnlyList<int> unluckyZone)
        {
            _luckyIndex = luckyIndex;
            _unluckyIndex = unluckyIndex;
            _unluckyZone.Clear();
            if (unluckyZone != null && unluckyIndex >= 0) _unluckyZone.AddRange(unluckyZone);
            ApplyHighlights();
            RecordEditorChanges();
        }

        /// <summary>World-space bounds of all tiles, with headroom for standing pieces.</summary>
        public Bounds GetBoardBounds(float headroom = 0.6f)
        {
            if (tiles.Count == 0) return new Bounds(transform.position, Vector3.zero);

            var bounds = new Bounds(tiles[0].transform.position, Vector3.zero);
            foreach (var tile in tiles)
            {
                if (tile == null) continue;
                var p = tile.transform.position;
                var half = tile.transform.lossyScale * 0.5f;
                bounds.Encapsulate(p + new Vector3(-half.x, 0f, -half.z));
                bounds.Encapsulate(p + new Vector3(half.x, BoardTileView.SurfaceHeight + headroom, half.z));
            }
            return bounds;
        }

        private void ApplyHighlights()
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i] == null) continue;
                if (i == _luckyIndex) tiles[i].SetMaterial(materials.lucky, TileHighlight.Lucky);
                else if (i == _unluckyIndex || _unluckyZone.Contains(i)) tiles[i].SetMaterial(materials.unlucky, TileHighlight.Unlucky);
                else if (!_circuit && _luckyIndex > 0 && i > _luckyIndex && materials.beyondLucky != null)
                    tiles[i].SetMaterial(materials.beyondLucky, TileHighlight.None); // unreachable: visual only
                else tiles[i].SetMaterial(BaseMaterial(i), TileHighlight.None);
            }
            PlaceMarker(luckyMarker, _luckyIndex);
            PlaceMarker(unluckyMarker, _unluckyIndex);
        }

        private void PlaceMarker(Transform marker, int index)
        {
            if (marker == null) return;
            bool show = index >= 0 && index < tiles.Count && tiles[index] != null;
            if (marker.gameObject.activeSelf != show) marker.gameObject.SetActive(show);
            if (show)
            {
                marker.position = tiles[index].transform.position +
                    new Vector3(MarkerOffsetXZ.x, Mathf.Min(markerHeight, 0.28f), MarkerOffsetXZ.y);
                marker.localScale = Vector3.one * 0.34f;
            }
        }

        private Material BaseMaterial(int index)
        {
            if (index == 0 && materials.start != null) return materials.start;
            return index % 2 == 0 ? materials.even : materials.odd;
        }

        private BoardTileView CreateTile()
        {
            if (tilePrefab == null) return BoardTileView.CreateDefault(transform);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var instance = (BoardTileView)UnityEditor.PrefabUtility.InstantiatePrefab(tilePrefab, transform);
                return instance;
            }
#endif
            return Instantiate(tilePrefab, transform);
        }

        private GameObject CreateConnector(Vector3 from, Vector3 to, int index)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Connector_{index - 1:00}_{index:00}";
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);

            var delta = to - from;
            float length = delta.magnitude;
            go.transform.localPosition = (from + to) * 0.5f + Vector3.up * 0.04f;
            go.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            go.transform.localScale = new Vector3(0.22f, 0.08f, length);

            var renderer = go.GetComponent<Renderer>();
            if (materials.connector != null) renderer.sharedMaterial = materials.connector;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private static void DestroySafe(UnityEngine.Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
