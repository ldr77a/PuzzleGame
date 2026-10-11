# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Unity **6000.3.26f1** (Unity 6) 2D color-mixing puzzle game. The player walks a grid and collects colored tiles, and the mix of the collected colors has to match a target color before moves run out. Uses URP, uGUI, and TextMeshPro. Code comments, log messages, and editor-window labels are in Korean.

**Direction:** This prototype is being reworked into the mobile game **Cameli Garden** (Android + iOS, portrait). [`docs/Cameli Garden.md`](docs/Cameli%20Garden.md) holds the full design, decisions with rationale, roadmap, and open items. **Read it before changing gameplay code.**

The architecture described below is the *current, pre-rework* prototype. Where it conflicts with the design doc, the design doc is the target. Examples of current behavior that the design replaces:
- Space to collect → stepping on a tile eats it
- empty tiles → every tile is colored
- reset tiles → removed (unlimited restart + limited undo)
- move limits → none (stars instead)
- dashboard in the game scene → split out into a dev-only tool

Phase 1 (the Unity prototype) is done on a separate git branch.

## Building and verifying

- There is no CLI build, lint, or test setup. `com.unity.test-framework` is installed, but there are no test assemblies and no `.asmdef` files. All runtime code compiles into `Assembly-CSharp`, and `Assets/Scripts/Editor/` compiles into `Assembly-CSharp-Editor`. `dotnet` is not installed, so the generated `.csproj` files can't be built directly.
- The Unity Editor usually has this project open. In that case, check `~/Library/Logs/Unity/Editor.log` for compile errors after editing scripts (Unity recompiles when the Editor regains focus).
- A batch-mode compile check only works while the Editor is **closed**:
  `"/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity" -batchmode -nographics -quit -projectPath . -logFile -`
- The only build scene is `Assets/0. Scenes/Puzzle.unity`. `Old_Puzzle.unity` is disabled.

## Unity-specific editing notes

- Scenes and prefabs reference scripts by the GUID in the `.meta` file. If you move or rename a `.cs` file from the shell, move its `.meta` file with it. Otherwise the scene's component references break.
- Manager wiring is done through `[SerializeField]` references in `Puzzle.unity`. If you rename a serialized field, the inspector link is silently dropped unless you add `[FormerlySerializedAs]`.
- Gameplay input uses the legacy `Input.GetKeyDown` API. This works because Active Input Handling is set to "Both" (`activeInputHandler: 2`).

## Architecture

### Stage data format (`Data/StageData.cs`)

Stages are JSON files deserialized with `JsonUtility` into `StageData`.
- `gridData` is a **1D row-major array**: `index = y * width + x`. `y = 0` is the **top** row, so "up" is `(0, -1)`.
- Cell values: `>0` = color tile whose value is a `colorPalette[].id` (the ids are *strings*, `"1"`, `"2"`, …), `0` = empty, `-1` = wall, `-2` = reset, `-3` = fake color (its color comes from `fakeColorRgb`).
- `colorClear` is the target RGB. `solutionColorRepeats[i]` is how many times `colorPalette[i]` is collected in the intended mix. `totalRepeats` is the sum of those counts.

### Color mixing (`Utils/ColorMixer.cs`)

The mix is the **per-channel geometric mean** of all collected colors, starting from white. Two consequences:
- Order doesn't matter, so the A* state key sorts the collected ids.
- Scaling the repeat counts gives the same color (`[2,2]` mixes to the same result as `[1,1]`). This is why `StageGenerator` retries until the repeat counts have GCD = 1.

The win check uses `ColorsMatch` with tolerance 2 in game and tolerance 1 in the A* solver.

### Stage loading

- Generated files are written to `Assets/Resources/Stages/stage_{id:000}.json`. Existing stages have been moved by hand into `1. Easy/`, `2. Medium/`, and `3. Hard/`.
- `StageLoader.LoadStage` looks in the root `Stages/` folder first, then in those three hardcoded subfolder names.
- `DashboardManager` and `StageDatabase` use `Resources.LoadAll("Stages")`, which is recursive.
- Stage IDs must be unique across all folders, and file names must follow `stage_XXX`. "Next stage" means `stageId + 1`.

### Runtime flow (all in `Puzzle.unity`)

- `GameManager` is a singleton. `StartStage(id)` loads the data, then calls `GridManager.GenerateGrid`, `PlayerController.Initialize`, and `UIManager.Initialize`, in that order. It then subscribes UI and win/lose handlers to the `System.Action` events on `PlayerController`. Because `StartStage` runs again on replay, next stage, and dashboard selection, it **unsubscribes before resubscribing**. Keep that pattern when adding events.
- The whole board is **uGUI on a Canvas** (UI `Image` tiles, no Tilemap or world-space sprites).
  - The main `GridManager.GenerateGrid(StageData)` reads the cell size, spacing, and padding from the container's `GridLayoutGroup`, then **disables** that component and positions tiles manually.
  - The overload `GenerateGrid(data, parent, cellSize)` is a separate preview-only renderer. It is used for the dashboard thumbnails, creates no `Tile` state, and keeps the `GridLayoutGroup` enabled.
- `PlayerController` reparents the player visual to the grid container's parent and forces it to be the last sibling every frame so it draws on top.
- `DashboardManager` runs in the same scene. It shows the stage list (left), thumbnail previews (right), and filter/sort dropdowns.
  - Dropdown options are built in code (`InitializeDropdowns`) because Unity serialization kept resetting them.
  - The filter logic maps dropdown indexes straight to values (difficulty = index, colorCount = index + 1, totalRepeats = index + 1). If you change the option lists, update `RefreshList` to match.
- `UIManager`'s Solution button runs `AStarPathfinder` live and draws the result with `SolutionVisualizer`.

### Game rules vs. solver model (keep in mind when touching either one)

| | In game (`PlayerController`) | A* (`AStarPathfinder`) / generator |
|---|---|---|
| Arrow-key move | costs 1 move, does **not** collect | stepping onto a color tile **auto-collects** |
| Collecting | Space on the current tile, **free** | "stay and re-collect" action, costs 1 move |
| Reset tile | stepping on it clears the mix | same |
| Fake color tile | Space adds the fake color to the mix | no effect |

Both columns describe the current prototype and are superseded by the design doc's rules. In the rework, the game and the solver must share a single pure-C# rules implementation so this mismatch can't come back.

So `solutionMoves` counts actions in the solver's model, not the in-game move count. `maxMoves = ceil(solutionMoves * moveMultiplier)`, with the multiplier set per difficulty.

The A* search has a 10-second wall-clock timeout and a `maxCollections` cap. It uses a custom `PriorityQueue<T>` defined at the bottom of `AStarPathfinder.cs`, which handles priority updates by inserting duplicates and relying on the closed set to skip them.

### Procedural generation (`Generators/StageGenerator.cs`)

`DifficultyProfile[]` (Easy 4×4, Medium 5×5, Hard 6×6) sets the color counts, repeat ranges, walls, special tiles, and placement strategy. `GenerateStage` works like this:
1. Pick the palette and repeat counts, then compute the target color.
2. Try up to 20 grid layouts, validating each one with A*.
3. Accept the first layout that meets the collection-count and move-count bounds. If none does, return the best-scoring candidate.

The 7-color `baseColors` palette is **duplicated** in `StageGenerator` and `GridManager` (which uses it to pick a random fake color when `fakeColorRgb` is missing or empty). Keep the two copies in sync.

### Editor tools (`Assets/Scripts/Editor/`, under the **Tools** menu)

- **Stage Generator**: generates an ID range for one difficulty into `Resources/Stages/`. It overwrites any existing file with the same ID.
- **Stage Data Manager**: browses, filters, and previews stages through the `Assets/StageDatabase.asset` ScriptableObject, which is reloaded from Resources whenever the window opens. "Delete" removes the stage only from the database, not the JSON file. "Play" is a stub.
- **Solution Viewer**: opened from Stage Data Manager. Steps through the A* `stateHistory`.
