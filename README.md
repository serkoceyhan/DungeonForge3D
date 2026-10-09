# 🏰 DungeonForge 3D

A browser-based **procedural map ideation tool for level designers** — generate coherent dungeon layouts in seconds, watch them assemble on screen with smooth animations, then export straight into **Unity** with your own prefabs.

Built with **Three.js + WebGL** on the web side and a lightweight **JSON → Unity (C#)** bridge on the engine side.

---

## ✨ What it does

DungeonForge 3D helps level designers **explore map ideas fast**. Instead of sketching layouts by hand, you tune a few parameters, hit *Generate*, and get a playable dungeon structure — rooms, corridors, entry/exit points — rendered in a clean isometric 3D view. When you find a layout you like, one click exports it as JSON that rebuilds inside your Unity scene using your real game assets.

The web tool and Unity talk to each other through a simple data file (JSON), so the same export could just as easily feed Unreal, Godot, or any custom engine.

---

## 🎮 Features

- **Multiple generation algorithms** — BSP rooms & corridors, organic caves, winding tunnels — for varied design inspiration.
- **Animated "build-in" effect** — blocks drop and spring into place sequentially, assembling the dungeon in front of you (great for video/demos).
- **Isometric 3D preview** — orthographic camera, soft shadows, moody dark game-map aesthetic with thick stone walls and tiled floors.
- **Full control** — grid size, room count, build speed, and a deterministic **seed** (same seed = same map, every time).
- **Orbit camera** — drag to rotate, scroll to zoom.
- **Export** — download the layout as **JSON** (the Unity bridge) or as a top-down **PNG minimap**.
- **Unity importer** — a small C# script rebuilds the exported layout in-scene from your prefabs.

---

## 🧩 How the Web → Unity bridge works

There is no live connection — the bridge is a **data file**.

1. The web tool generates the dungeon as a **grid matrix** (each cell = floor / wall / entry / exit).
2. **Export JSON** downloads that layout as a `.json` file:

```json
{
  "seed": "rune-01",
  "width": 48,
  "height": 48,
  "cells": [[0, 2, 1, 1, 2, 0], [2, 1, 1, 1, 1, 2]],
  "entry": { "x": 6, "y": 6 },
  "exit":  { "x": 40, "y": 38 }
}
```

3. In Unity, `DungeonImporter.cs` parses the JSON, loops over the grid, and **instantiates one prefab per cell** at the correct world position — walls taller than floors, entry/exit using their own prefabs.

Generate on the web → export → build in Unity. Three separate steps, no tight coupling.

---

## 🚀 Getting started

### Web tool

1. Open `index.html` in any modern browser (double-click — no build step, no server needed).
2. Pick an algorithm, tweak the sliders, try different seeds.
3. When you like a layout, click **Export JSON** (and optionally **Export PNG**).

### Unity importer

1. Copy `unity/DungeonImporter.cs` into your Unity project's `Assets` folder.
2. Drop the exported `.json` file into `Assets` too.
3. Create an empty GameObject, add the **Dungeon Importer** component.
4. Assign the JSON to the **Json File** slot and your **Floor / Wall / Entry / Exit** prefabs (plain cubes work fine for testing).
5. Right-click the component → **Build Level**. Your dungeon appears in the scene.

> See `unity/README.md` for detailed Unity setup.

---

## 🛠️ Tech stack

| Layer | Tech |
|-------|------|
| Rendering | Three.js, WebGL |
| Generation | Binary Space Partitioning (BSP), cellular automata, random walk |
| Animation | `requestAnimationFrame` + easing (spring / ease-out) |
| Camera | Orthographic isometric + OrbitControls |
| Bridge | JSON export / import |
| Engine | Unity (C#, `JsonUtility`) |

---

## 📁 Project structure

```
level-forge/
├── index.html              # The web ideation tool (single file)
├── README.md               # This file
└── unity/
    ├── DungeonImporter.cs   # Unity C# importer (JSON → scene)
    └── README.md            # Unity setup guide
```

---

## 🤖 Built with Claude AI

This project was developed through pair-programming with **Claude AI** — from the initial concept to the working tool and the Unity pipeline.

---

## 📝 Proje hakkında (TR)

**DungeonForge 3D**, level tasarımcıları için tarayıcıda çalışan prosedürel harita fikir aracıdır. Birkaç parametreyi ayarlayıp *Generate*'e basınca odalar, koridorlar ve giriş/çıkış noktalarından oluşan mantıklı bir zindan haritası üretir ve gözünüzün önünde animasyonla kurar. Beğendiğiniz haritayı **JSON** olarak dışa aktarıp, küçük bir **C# importer** ile doğrudan Unity sahnenize kendi prefab'larınızla aktarabilirsiniz. Web tarafı **Three.js + WebGL**, köprü ise basit bir **JSON** dosyasıdır — bu yüzden aynı çıktı Unreal veya Godot için de kullanılabilir.

---

## 📄 License

MIT — free to use, modify, and share.
