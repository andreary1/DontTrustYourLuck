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

        public IReadOnlyList<BoardTileView> Tiles => tiles;
        public int TileCount => tiles.Count;
        public ZigzagBoardLayout Layout => layout;
        public int LuckyIndex => _luckyIndex;
        public int UnluckyIndex => _unluckyIndex;
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

        private void Awake()
        {
            EnsureBuilt(tileCount);
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

            float angle = Mathf.PI * 0.25f + slot * (2f * Mathf.PI / occupantCount);
            var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * pieceSlotRadius;
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

        /// <summary>Displays lucky/unlucky tiles. Pass -1 to clear one of them.</summary>
        public void SetHighlights(int luckyIndex, int unluckyIndex)
        {
            _luckyIndex = luckyIndex;
            _unluckyIndex = unluckyIndex;
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
                else if (i == _unluckyIndex) tiles[i].SetMaterial(materials.unlucky, TileHighlight.Unlucky);
                else if (_luckyIndex > 0 && i > _luckyIndex && materials.beyondLucky != null)
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
            if (show) marker.position = tiles[index].transform.position + Vector3.up * markerHeight;
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
