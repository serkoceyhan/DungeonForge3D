// ═══════════════════════════════════════════════════════════════════════════
//  DungeonImporter.cs
//  ─────────────────────────────────────────────────────────────────────────
//  Rebuilds a layout exported from the DungeonForge web ideation tool using
//  your own Unity prefabs.
//
//  QUICK START
//    1. Put this file anywhere under Assets/ (e.g. Assets/Scripts/).
//    2. Create an empty GameObject in your scene, add this component.
//    3. Drag the exported .json file into the "Json File" slot.
//    4. Assign your Floor / Wall / Entry / Exit prefabs.
//    5. Right-click the component header → "Build Level".
//
//  COORDINATE CONVENTION
//    grid x  →  Unity X
//    grid y  →  Unity Z        (the map is laid out flat on the XZ plane)
//    Unity Y is up.
//
//  See unity/README.md for the full walkthrough.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DungeonForge
{
    // ═══════════════════════════════════════════════════════════════════════
    //  SECTION 1 — SERIALIZABLE DATA MODEL
    //  These classes mirror the JSON the web tool exports. Field names must
    //  match the JSON keys exactly — that is how JsonUtility maps them.
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>A grid coordinate. Matches { "x": 12, "y": 30 } in the JSON.</summary>
    [Serializable]
    public class GridPoint
    {
        public int x;
        public int y;
    }

    /// <summary>
    /// One room / chamber rectangle. `type` is "room", "chamber", "entry" or "exit".
    /// Handy for gameplay logic: spawn encounters per room, light them separately, etc.
    /// </summary>
    [Serializable]
    public class RoomData
    {
        public int x;       // left edge, in grid cells
        public int y;       // top edge, in grid cells
        public int w;       // width in cells
        public int h;       // height in cells
        public string type;
    }

    /// <summary>
    /// The whole exported layout.
    ///
    /// NOTE ON `cells` vs `cellsFlat`:
    /// The JSON contains BOTH a 2D `cells` array (readable, for humans and other
    /// tools) and a row-major `cellsFlat` array. Unity's JsonUtility cannot
    /// represent nested arrays, so we only declare `cellsFlat` here and strip the
    /// `cells` field out of the text before parsing. See StripCellsField() below.
    /// </summary>
    [Serializable]
    public class DungeonData
    {
        public string tool;
        public int version;
        public string algorithm;    // "bsp" | "cave" | "walk"
        public string seed;
        public int width;
        public int height;
        public string[] cellTypes;  // index == cell value, e.g. cellTypes[2] == "wall"
        public int[] cellsFlat;     // row-major: index = y * width + x
        public RoomData[] rooms;
        public GridPoint entry;
        public GridPoint exit;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  SECTION 2 — THE IMPORTER
    // ═══════════════════════════════════════════════════════════════════════

    [AddComponentMenu("DungeonForge/Dungeon Importer")]
    public class DungeonImporter : MonoBehaviour
    {
        // ── Cell type values. Must stay in sync with CELL in index.html. ────
        public const int CELL_EMPTY = 0;
        public const int CELL_FLOOR = 1;
        public const int CELL_WALL  = 2;
        public const int CELL_ENTRY = 3;
        public const int CELL_EXIT  = 4;
        public const int CELL_PROP  = 5;

        [Header("── Layout Source ──")]
        [Tooltip("The .json exported from the DungeonForge web tool. " +
                 "Unity imports .json files as TextAssets automatically — just drag it in.")]
        public TextAsset jsonFile;

        [Tooltip("Optional fallback. If no TextAsset is assigned, the importer reads " +
                 "this absolute or project-relative file path instead. Useful for " +
                 "hot-reloading a file you keep re-exporting outside of Assets/.")]
        public string jsonFilePath = "";

        [Header("── Prefabs ──")]
        [Tooltip("Spawned for every walkable cell (also under entry/exit/prop cells).")]
        public GameObject floorPrefab;

        [Tooltip("Spawned for every wall cell.")]
        public GameObject wallPrefab;

        [Tooltip("Spawned at the player spawn point. Falls back to a prop prefab if empty.")]
        public GameObject entryPrefab;

        [Tooltip("Spawned at the level goal.")]
        public GameObject exitPrefab;

        [Tooltip("Optional. One is picked at random (seeded, so it's reproducible) " +
                 "for each prop cell. Leave empty to skip props entirely.")]
        public List<GameObject> propPrefabs = new List<GameObject>();

        [Header("── Grid Layout ──")]
        [Tooltip("World size of one grid cell, in Unity units.")]
        public float cellSize = 1f;

        [Tooltip("Y position of the floor prefabs.")]
        public float floorY = 0f;

        [Tooltip("Y position of the wall prefabs. Raise this so walls sit ON the floor " +
                 "rather than inside it.")]
        public float wallY = 0.5f;

        [Tooltip("Y position of entry / exit / prop prefabs.")]
        public float markerY = 0.05f;

        [Tooltip("OFF (default) → cells land at exactly (x * cellSize, 0, y * cellSize), " +
                 "so the level grows out of this object's origin into +X / +Z.\n" +
                 "ON → the level is recentered so its middle sits on the origin, which is " +
                 "handier once you want to rotate or scale the whole dungeon.")]
        public bool centerOnPivot = false;

        [Header("── Prefab Scaling ──")]
        [Tooltip("ON  → prefabs are stretched to exactly fill a cell. Best with plain " +
                 "1×1×1 cubes.\n" +
                 "OFF → prefabs keep their authored scale. Use this once you have " +
                 "real art that is already the right size.")]
        public bool scalePrefabsToCell = true;

        [Tooltip("Wall height multiplier, applied only when 'Scale Prefabs To Cell' is on. " +
                 "Walls taller than floors is what sells the dungeon read.")]
        public float wallHeight = 2f;

        [Tooltip("Floor slab thickness, applied only when 'Scale Prefabs To Cell' is on.")]
        public float floorThickness = 0.2f;

        [Header("── Behaviour ──")]
        [Tooltip("Build the level automatically when the scene starts playing.")]
        public bool buildOnStart = false;

        [Tooltip("Give each spawned object a readable name like 'Wall_12_30'. " +
                 "Nice while wiring things up; turn off for big levels to save memory.")]
        public bool nameSpawnedObjects = true;

        // ── Runtime info, shown in the Inspector after a build ─────────────
        [Header("── Last Build (read-only) ──")]
        [SerializeField] private string lastAlgorithm = "-";
        [SerializeField] private string lastSeed = "-";
        [SerializeField] private int lastSpawnCount = 0;

        /// <summary>Parsed data from the most recent build. Null until you build once.</summary>
        public DungeonData Data { get; private set; }

        /// <summary>World position of the entry cell after the most recent build.</summary>
        public Vector3 EntryWorldPosition { get; private set; }

        /// <summary>World position of the exit cell after the most recent build.</summary>
        public Vector3 ExitWorldPosition { get; private set; }

        // ═══════════════════════════════════════════════════════════════════
        //  SECTION 3 — ENTRY POINTS
        // ═══════════════════════════════════════════════════════════════════

        private void Start()
        {
            if (buildOnStart) BuildLevel();
        }

        /// <summary>
        /// Reads the JSON and instantiates the whole level.
        /// Right-click the component header in the Inspector → "Build Level".
        /// </summary>
        [ContextMenu("Build Level")]
        public void BuildLevel()
        {
            string json = LoadJsonText();
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("[DungeonForge] No JSON to read. Assign 'Json File' or " +
                               "set 'Json File Path'.", this);
                return;
            }

            DungeonData data = ParseJson(json);
            if (data == null) return;                 // ParseJson already logged the reason

            ClearLevel();
            Data = data;

            int spawned = Spawn(data);

            lastAlgorithm  = data.algorithm;
            lastSeed       = data.seed;
            lastSpawnCount = spawned;

            Debug.Log($"[DungeonForge] Built '{data.algorithm}' / seed \"{data.seed}\" — " +
                      $"{data.width}×{data.height}, {data.rooms?.Length ?? 0} rooms, " +
                      $"{spawned} objects.", this);
        }

        /// <summary>Destroys everything this importer previously spawned.</summary>
        [ContextMenu("Clear Level")]
        public void ClearLevel()
        {
            // Collect first — you must never mutate a Transform's children while
            // iterating over them.
            var doomed = new List<GameObject>(transform.childCount);
            for (int i = 0; i < transform.childCount; i++)
                doomed.Add(transform.GetChild(i).gameObject);

            foreach (GameObject go in doomed)
            {
                if (Application.isPlaying) Destroy(go);
                else                       DestroyImmediate(go);
            }

            lastSpawnCount = 0;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  SECTION 4 — JSON LOADING & PARSING
        // ═══════════════════════════════════════════════════════════════════

        private string LoadJsonText()
        {
            if (jsonFile != null) return jsonFile.text;

            if (!string.IsNullOrEmpty(jsonFilePath))
            {
                string path = Path.IsPathRooted(jsonFilePath)
                    ? jsonFilePath
                    : Path.Combine(Application.dataPath, "..", jsonFilePath);

                if (File.Exists(path)) return File.ReadAllText(path);

                Debug.LogError($"[DungeonForge] File not found: {path}", this);
            }
            return null;
        }

        private DungeonData ParseJson(string json)
        {
            try
            {
                DungeonData data = JsonUtility.FromJson<DungeonData>(StripCellsField(json));

                if (data == null)
                {
                    Debug.LogError("[DungeonForge] JSON parsed to null — is the file valid?", this);
                    return null;
                }
                if (data.cellsFlat == null || data.cellsFlat.Length == 0)
                {
                    Debug.LogError("[DungeonForge] No 'cellsFlat' array in the JSON. Re-export " +
                                   "from the web tool — older files may only have 'cells'.", this);
                    return null;
                }
                if (data.width <= 0 || data.height <= 0 ||
                    data.cellsFlat.Length != data.width * data.height)
                {
                    Debug.LogError($"[DungeonForge] Size mismatch: width={data.width}, " +
                                   $"height={data.height}, cells={data.cellsFlat.Length}.", this);
                    return null;
                }
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[DungeonForge] Failed to parse JSON: {e.Message}", this);
                return null;
            }
        }

        /// <summary>
        /// Removes the 2D "cells" field from the JSON text.
        ///
        /// Why: JsonUtility has no concept of a nested array, so we hand it only the
        /// flat version. The exported file keeps the 2D form for readability and for
        /// other tools; this strips it so JsonUtility never has to look at it.
        /// Walks the brackets to find the matching close, so it is safe regardless
        /// of formatting or how large the grid is.
        /// </summary>
        private static string StripCellsField(string json)
        {
            int key = json.IndexOf("\"cells\"", StringComparison.Ordinal);
            if (key < 0) return json;

            int open = json.IndexOf('[', key);
            if (open < 0) return json;

            int depth = 0, close = -1;
            for (int i = open; i < json.Length; i++)
            {
                if      (json[i] == '[') depth++;
                else if (json[i] == ']') { depth--; if (depth == 0) { close = i; break; } }
            }
            if (close < 0) return json;

            // Also swallow the trailing comma so the result stays valid JSON.
            int end = close + 1;
            while (end < json.Length && char.IsWhiteSpace(json[end])) end++;
            if (end < json.Length && json[end] == ',') end++;

            return json.Remove(key, end - key);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  SECTION 5 — SPAWNING
        // ═══════════════════════════════════════════════════════════════════

        private int Spawn(DungeonData data)
        {
            // Deterministic prop variety: the same JSON always picks the same prefabs.
            System.Random rng = new System.Random(StableHash(data.seed));

            // Offset that recenters the grid on this object's pivot.
            Vector3 origin = Vector3.zero;
            if (centerOnPivot)
            {
                origin = new Vector3(-(data.width  - 1) * cellSize * 0.5f, 0f,
                                     -(data.height - 1) * cellSize * 0.5f);
            }

            int count = 0;

            for (int y = 0; y < data.height; y++)
            {
                for (int x = 0; x < data.width; x++)
                {
                    int cell = data.cellsFlat[y * data.width + x];
                    if (cell == CELL_EMPTY) continue;

                    // grid x → Unity X, grid y → Unity Z
                    Vector3 basePos = origin + new Vector3(x * cellSize, 0f, y * cellSize);

                    if (cell == CELL_WALL)
                    {
                        if (SpawnWall(basePos, x, y)) count++;
                        continue;
                    }

                    // Everything else is walkable, so it gets a floor tile first.
                    if (SpawnFloor(basePos, x, y)) count++;

                    switch (cell)
                    {
                        case CELL_ENTRY:
                            EntryWorldPosition = basePos + Vector3.up * markerY;
                            if (SpawnMarker(entryPrefab, basePos, "Entry", x, y)) count++;
                            break;

                        case CELL_EXIT:
                            ExitWorldPosition = basePos + Vector3.up * markerY;
                            if (SpawnMarker(exitPrefab, basePos, "Exit", x, y)) count++;
                            break;

                        case CELL_PROP:
                            if (propPrefabs != null && propPrefabs.Count > 0)
                            {
                                GameObject prefab = propPrefabs[rng.Next(propPrefabs.Count)];
                                if (SpawnMarker(prefab, basePos, "Prop", x, y)) count++;
                            }
                            break;
                    }
                }
            }
            return count;
        }

        private bool SpawnFloor(Vector3 basePos, int x, int y)
        {
            if (floorPrefab == null) return false;

            GameObject go = Instantiate(floorPrefab,
                                        transform.position + basePos + Vector3.up * floorY,
                                        Quaternion.identity, transform);

            if (scalePrefabsToCell)
                go.transform.localScale = new Vector3(cellSize, floorThickness, cellSize);

            Name(go, "Floor", x, y);
            return true;
        }

        private bool SpawnWall(Vector3 basePos, int x, int y)
        {
            if (wallPrefab == null) return false;

            GameObject go = Instantiate(wallPrefab,
                                        transform.position + basePos + Vector3.up * wallY,
                                        Quaternion.identity, transform);

            if (scalePrefabsToCell)
                go.transform.localScale = new Vector3(cellSize, wallHeight, cellSize);

            Name(go, "Wall", x, y);
            return true;
        }

        private bool SpawnMarker(GameObject prefab, Vector3 basePos, string label, int x, int y)
        {
            if (prefab == null) return false;

            // Markers keep their authored scale — you almost always want your real
            // door / chest / torch art at the size the artist built it.
            GameObject go = Instantiate(prefab,
                                        transform.position + basePos + Vector3.up * markerY,
                                        Quaternion.identity, transform);
            Name(go, label, x, y);
            return true;
        }

        private void Name(GameObject go, string label, int x, int y)
        {
            if (nameSpawnedObjects) go.name = $"{label}_{x}_{y}";
        }

        /// <summary>
        /// Deterministic string hash. C#'s built-in string.GetHashCode() is NOT
        /// stable across runs or platforms, so we roll our own (FNV-1a) to keep
        /// prop placement reproducible.
        /// </summary>
        private static int StableHash(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            unchecked
            {
                uint h = 2166136261;
                for (int i = 0; i < s.Length; i++)
                {
                    h ^= s[i];
                    h *= 16777619;
                }
                return (int)h;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  SECTION 6 — SCENE VIEW GIZMO
        //  Draws the level footprint plus entry / exit markers so you can see
        //  the layout bounds before you ever press Build.
        // ═══════════════════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            if (Data == null) return;

            Vector3 origin = centerOnPivot
                ? new Vector3(-(Data.width  - 1) * cellSize * 0.5f, 0f,
                              -(Data.height - 1) * cellSize * 0.5f)
                : Vector3.zero;

            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.5f);
            Gizmos.DrawWireCube(
                transform.position + origin +
                    new Vector3((Data.width - 1) * cellSize * 0.5f, 0f,
                                (Data.height - 1) * cellSize * 0.5f),
                new Vector3(Data.width * cellSize, 0.1f, Data.height * cellSize));

            if (Data.entry != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(EntryWorldPosition, cellSize * 0.6f);
            }
            if (Data.exit != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(ExitWorldPosition, cellSize * 0.6f);
            }
        }
    }
}
