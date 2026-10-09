# DungeonImporter — Unity Setup

Rebuilds a layout exported from the DungeonForge web tool in your Unity scene using your own prefabs.

Tested against **Unity 2020.3 LTS and newer**. No packages, no dependencies — one plain C# file.

---

## 1. Install the script

Copy `DungeonImporter.cs` anywhere under your project's `Assets/` folder. `Assets/Scripts/` is the usual home.

The class lives in the `DungeonForge` namespace, so it won't collide with anything you already have.

---

## 2. Get your JSON into Unity

1. In the web tool, click **↓ JSON**. You'll get a file like `dungeon_bsp_ideate-001_48.json`.
2. Drag that file into your Unity project (e.g. `Assets/Layouts/`).

Unity imports `.json` files as **TextAsset** automatically — no renaming to `.txt` needed.

> **Alternative:** leave the JSON outside `Assets/` and set **Json File Path** on the component instead — an absolute path, or a path relative to your project root. Handy when you're re-exporting repeatedly and don't want Unity reimporting every time.

---

## 3. Add the component

1. Create an empty GameObject in your scene. Name it `Dungeon`.
2. Reset its transform to `(0, 0, 0)`.
3. **Add Component → DungeonForge → Dungeon Importer**.

Everything the importer spawns is parented under this object, so the whole level moves/rotates as one and you can delete it in a single click.

---

## 4. Assign prefabs

| Slot | What goes here | Required |
| --- | --- | --- |
| **Floor Prefab** | Your floor tile | Yes |
| **Wall Prefab** | Your wall block | Yes |
| **Entry Prefab** | Player spawn marker / door | Optional |
| **Exit Prefab** | Level goal / stairs | Optional |
| **Prop Prefabs** | A list — one is picked at random per prop cell | Optional |

**No art yet?** Two Unity cubes get you a working level in 30 seconds:

1. `GameObject → 3D Object → Cube`, drag it into your Project window to make a prefab, name it `Floor`.
2. Duplicate it, name it `Wall`, give it a darker material.
3. Assign both, delete the originals from the scene.

Leave **Scale Prefabs To Cell** ON while you're using plain cubes — the importer stretches them to fill each cell (thin slabs for floors, tall blocks for walls). Turn it **OFF** once you have real art that's already authored at the correct size.

---

## 5. Press Build

Right-click the **Dungeon Importer** component header in the Inspector → **Build Level**.

The level appears immediately in the Editor — no Play mode needed. The Console logs what was built:

```
[DungeonForge] Built 'bsp' / seed "ideate-001" — 48×48, 9 rooms, 1873 objects.
```

**Clear Level** (same right-click menu) removes everything it spawned.

To rebuild after exporting a new layout: drop in the new JSON, press **Build Level** again. It clears the old level first, so you never stack two dungeons.

Tick **Build On Start** if you want the level to assemble when you press Play instead.

---

## 6. Tuning the layout

| Field | What it does |
| --- | --- |
| **Cell Size** | World units per grid cell. Raise for a bigger-feeling dungeon. |
| **Floor Y / Wall Y / Marker Y** | Vertical placement per layer. If your walls sink into the floor, raise **Wall Y**. |
| **Center On Pivot** | OFF (default) puts each cell at exactly `(x * cellSize, 0, y * cellSize)`, so the level grows out of the GameObject's origin into +X / +Z. ON recenters it, which is handier once you want to rotate or scale the dungeon as a whole. |
| **Wall Height** | How much taller walls are than floors (scaling mode only). |
| **Floor Thickness** | Floor slab thickness (scaling mode only). |
| **Name Spawned Objects** | Names everything `Wall_12_30`. Turn off on huge levels to save memory. |

---

## 7. Building on top of it

After a build, the component exposes:

```csharp
var importer = GetComponent<DungeonForge.DungeonImporter>();

importer.EntryWorldPosition   // Vector3 — drop your player here
importer.ExitWorldPosition    // Vector3 — put the goal trigger here
importer.Data.rooms           // RoomData[] — x, y, w, h, type per room
importer.Data.algorithm       // "bsp" | "cave" | "walk"
importer.Data.seed            // the seed string that produced this layout
```

`Data.rooms` is the useful one for gameplay: iterate it to spawn one encounter per room, place a light in each chamber, or skip rooms tagged `"entry"` / `"exit"`.

Select the GameObject in the Scene view to see gizmos for the level bounds and the entry/exit spheres.

---

## 8. Reference

### Cell values

The grid is a flat `int[]` indexed `y * width + x`:

| Value | Meaning |
| --- | --- |
| `0` | empty — nothing spawned |
| `1` | floor |
| `2` | wall |
| `3` | entry |
| `4` | exit |
| `5` | prop |

Entry, exit and prop cells **also** get a floor tile spawned underneath them.

These constants are exposed as `DungeonImporter.CELL_FLOOR` etc. If you ever change them, change `CELL` in `index.html` to match.

### Coordinates

grid **x → Unity X**, grid **y → Unity Z**, Unity **Y is up**. The map lies flat on the XZ plane.

### Why the JSON has both `cells` and `cellsFlat`

Unity's `JsonUtility` cannot represent nested arrays, so it can't read `cells: [[...]]`. The exporter writes the grid twice: `cells` (2D, readable, for humans and other tools) and `cellsFlat` (row-major, what the importer actually uses). `StripCellsField()` removes the 2D version from the text before parsing so `JsonUtility` never sees a structure it can't model.

---

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| `No JSON to read` | The **Json File** slot is empty and **Json File Path** is blank. |
| `No 'cellsFlat' array in the JSON` | The file came from something other than this tool, or was hand-edited. Re-export. |
| `Size mismatch` | The JSON was truncated or edited. Re-export. |
| Nothing appears | **Floor Prefab** and **Wall Prefab** are unassigned — the importer spawns nothing without them. |
| Walls sunk into the floor | Raise **Wall Y** (try `wallHeight / 2`). |
| Everything is one giant blob | Your prefabs are bigger than 1 unit. Turn **Scale Prefabs To Cell** ON, or raise **Cell Size** to match your art. |
| Level built off to one side | Turn on **Center On Pivot**, and make sure the GameObject's transform is at the origin. |
| Editor hitches on big levels | A 90×90 grid is ~8,000 GameObjects. Drop the grid size in the web tool, or turn off **Name Spawned Objects**. |
