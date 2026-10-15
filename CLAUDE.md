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
- Progress logs are dated `docs/MMDD 작업 정리.md`. Read the latest one before resuming. `docs/1011 작업 정리.md` §2 holds the remaining Phase 1 task list.
- `docs/1011 에이전트 지시문.md` holds a copy-paste prompt per task. Its §0 lists the shared rules for agents: verification, git, and docs.

## Building and verifying

- There is no CLI build or lint setup, and a system `dotnet` is not installed. Assemblies:
  - `Cameli.Core` (`Assets/Scripts/Cameli/Core/`) and `Cameli.Tests.EditMode` (`Assets/Tests/EditMode/Cameli/`) have their own `.asmdef` files.
  - All other runtime code compiles into `Assembly-CSharp`, and `Editor/` folders compile into `Assembly-CSharp-Editor`.
  - In the editor, run the tests from Window → General → Test Runner → EditMode.
- **`Tools/CameliHarness/` checks code without the editor** (even while it is open). It uses the C# compiler and .NET runtime bundled with the Unity version in `ProjectVersion.txt`:
  - `test-core.sh`: builds `Cameli.Core` (netstandard 2.1, like Unity) and runs the engine-free tests in `Assets/Tests/EditMode/Cameli/CoreTests.cs`.
  - `check-unity.sh`: compile-checks Assembly-CSharp, Assembly-CSharp-Editor, and the Cameli test assembly, using references and defines from the generated `.csproj` files. Run it after editing any Unity script.
  - `lab.sh check`: validates every stage in the difficulty folders under `Assets/Resources/CameliStages`, plus the folder placement and id order (below). It checks that each stage is solvable, that `optimalMoves` matches the solver, that every shortest clear ends beyond any gate (the gate is used as a door, not a reset), that gates are passed with their exact recipe, and the ambiguity filter: no other recipe with the target's (or a gate's) drop count, built from the board's tile colors, may mix within CIELAB ΔE 10 of it. It also prints the closest tile-color pair and the chance a random walk clears. The checks live in `Core/StageAudit.cs`, shared with the Unity menu "Cameli/스테이지 검증"; `LevelLab.cs` only formats them. `lab.sh search …` finds candidate boards and `lab.sh solve …` prints a shortest path; usage is in the script header.
  - Output goes to `Tools/CameliHarness/out/`, which is gitignored.
- The Unity Editor usually has this project open. In that case, check `~/Library/Logs/Unity/Editor.log` for compile errors after editing scripts (Unity recompiles when the Editor regains focus).
- A batch-mode compile check only works while the Editor is **closed**:
  `"/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity" -batchmode -nographics -quit -projectPath . -logFile -`
- The only build scene is `Assets/0. Scenes/Puzzle.unity`. `Old_Puzzle.unity` is disabled.

## Unity-specific editing notes

- Scene work always goes in a **new** scene. Don't modify or re-save the existing scenes (`Puzzle.unity`, `Old_Puzzle.unity`). Create new scenes from code through a `Cameli/` menu item, the way `CameliMenu` creates `Prototype.unity`.
- Scenes and prefabs reference scripts by the GUID in the `.meta` file. If you move or rename a `.cs` file from the shell, move its `.meta` file with it. Otherwise the scene's component references break.
- Manager wiring is done through `[SerializeField]` references in `Puzzle.unity`. If you rename a serialized field, the inspector link is silently dropped unless you add `[FormerlySerializedAs]`.
- Gameplay input uses the legacy `Input.GetKeyDown` API. This works because Active Input Handling is set to "Both" (`activeInputHandler: 2`).

## Architecture

### Cameli Garden rework (Phase 1, `Assets/Scripts/Cameli/`)

- **`Core/` (assembly `Cameli.Core`, `noEngineReferences`)** holds the game rules in pure C#. Game and solver share this one implementation:
  - `Rules.Step` is the only move logic.
    - Clearing, and passing a gate, need the color (±2) **and** the exact drop count (`Rules.Meets`). Ratio multiples don't count.
    - `Rules.IsOverfilled` flags a mix with more drops than any recipe on the stage. The solver prunes those states.
    - A passed gate leaves the player standing on the gate cell. Stepping back out the same side with an empty mix is allowed by design, so stages must need the gate as a door; `lab.sh check` verifies this.
  - `GameSession` adds undo (3 per attempt; `GrantUndos` adds more, the ad reward) and restart, which also refills the undos. Using an undo or a "next step" hint caps the attempt at ★★ (`UsedUndo`, `UsedStepHint`, `Scoring.Stars(moves, optimal, assisted)`); restart clears both.
  - `Solver` is a BFS for shortest clears, from the initial board or any `BoardState`. Tooling and tests run it capped at `Solver.DefaultMaxStates` (2,000,000) states; at runtime only `Hints.Next(session)` runs it, capped at `Hints.DefaultMaxStates` states.
  - Colors are `Rgb`/`DropCounts`. `ColorMath` reproduces the legacy `ColorMixer` math.
  - `StageAudit.Audit(StageFile)` returns a `StageReport` (solvable, optimal vs stored, gate use, exact gate recipes, ambiguity ΔE, tile ΔE, walk%) with `StageProblems` flags. Tooling only: `lab.sh check` and the "Cameli/스테이지 검증" menu.
  - `StageSections.All` lists the difficulty folders in play order with their garden headings; `StageDefinition.Section` is the index of the folder a stage was loaded from. Unity can't list Resources folders at runtime, so a new folder must be added there.
  - Keep this assembly free of UnityEngine so the harness can run it.
- **`Game/` (Assembly-CSharp, namespace `Cameli.Game`)** is the prototype client:
  - `CameliGame` is the entry point on an empty GameObject. It builds the whole uGUI in code: no prefabs and no scene wiring.
  - `BoardView` positions cells with normalized anchors. `BoardInput` handles swipe and tap.
  - `HudView` and `ClearPanel` are the UI. `SpriteFactory` generates the flat shapes and icons; `Blossoms` stacks them into camellias and buds, and `CameliFigure` into the temporary Cameli. `StageLibrary` loads the stages folder by folder (`StageSections`), ordered by folder then id. `Progress` stores progress in PlayerPrefs; `Progress.DevUnlockAll` (PlayerPrefs `cameli.dev.unlockAll`, honored only in the editor and development builds) makes `Unlocked` cover every stage.
  - `Theme` holds the "동백 잎" colors and `Ui.Font` loads Jua from `Resources/Fonts/` (OFL). `Sfx` synthesizes the sound effects, `Haptics.Tap` vibrates (iOS plugin in `Assets/Plugins/iOS/`), and `Settings` stores the sound and vibration switches.
  - Playtest tools: holding the stage title for 2 s (`LongPress`) opens `PlaytestPanel`, which offers next tester / log summary. `PlaytestLog` appends one row per stage attempt to `persistentDataPath/playtest.csv`. The moderator's guide is `docs/플레이테스트 기록지.md`.
  - **Phase 2 app (`GardenApp`, scene `Garden.unity`)** follows `docs/2단계 화면 명세.md`. `GardenApp` builds everything in code and routes Title → Garden → Play, with `MenuSheet`, `GrantPopup` (reward placeholder, marked `// 4단계: 보상형 광고`), and `PlaytestPanel` on top; Escape (Android back) closes the top window first. The screens live in `Game/Screens/`:
    - `TitleScreen`, `GardenScreen` (scrolling 4-column stage bed, one section per difficulty folder; also decides the next stage and "이어하기"), `PlayScreen` (session, input buffering, hints, guides, feedback, playtest log; same attempt boundaries as `CameliGame`), `PlayHud` + `GoalCard` (everything around the board), `ClearView` (bloom sequence, tap to skip).
    - `Widgets` holds the shared card/button/star helpers; `Sway`, `Blink`, `SlideIn` are tiny unscaled-time animators.
    - `BoardView.FigureToken` draws the token as `CameliFigure`; `ShowGlow`, `ShowArrow`, `ShowSwipe` draw the tutorial/hint guides. `CameliGame` leaves them off.
    - `HintStore` keeps the shared hint count (`cameli.hints`, starts at 3) and per-stage recipe reveals (`cameli.recipe.{id}`). `Progress.ResetAll` (next tester) clears them too.
    - Jua lacks "·", arrows, and similar symbols: keep UI strings to Hangul, Latin, digits, and ★, and draw icons with `SpriteFactory`.
- **Stage files** live in `Assets/Resources/CameliStages/<folder>/stage_NN.json`, matching `StageFile` in `Core/Stage.cs`:
  - The folder is the difficulty and the garden section: `0_Tutorial` (1–5), `1_Easy` (6–45), `2_Medium` (46–49 so far), `3_Hard` (empty, kept by `.gitkeep`). Files outside these folders are not loaded.
  - `rows`: space-separated tokens, top row first. Color letters are `R O Y G B N P`. `S` is the start (must be the center cell; on even sizes, the top-left of the central 2×2), `#` is a wall, `gN` is a gate.
  - `target`, `gates`: recipe strings such as `"RRY"`.
  - `optimalMoves`: from the solver.
  - Optional tutorial fields: `focus` (`[x, y]`, column and row from the top; the cell glows before the first move) and `swipe` (`"up"`, `"down"`, `"left"`, `"right"`).
  - Ids must run 1..n across all folders, in folder order (every tutorial id < every Easy id < …), so adding a stage anywhere but the end of the last used folder means renumbering. Moving files from the shell needs their `.meta` too.
  - The user's Korean editing guide is [`docs/스테이지 수정 방법.md`](docs/스테이지%20수정%20방법.md); keep it in sync when the format, rules, or audit change.
- **Editor menu `Cameli/`** (`Assets/Scripts/Editor/Cameli/CameliMenu.cs`):
  - Apply the mobile player settings.
  - Create `Assets/0. Scenes/Prototype.unity` (Phase 1) or `Assets/0. Scenes/Garden.unity` ("정원 씬 만들기", Phase 2) and make it the only enabled build scene.
  - "스테이지 검증" runs `StageAudit` on every stage file in every folder: one Console line per stage and a summary dialog. A wrong `optimalMoves` is rewritten in place (regex on that field only, then `AssetDatabase.Refresh`); other problems (files outside the difficulty folders, ids not running 1..n or out of folder order) are only reported.
  - "개발: 모든 스테이지 열기" is a checkable toggle for `Progress.DevUnlockAll`, so locked garden stages can be tapped while testing.
  - One-click builds into `~/Builds/CameliGarden`. "Android 빌드 후 폰에서 실행" builds the APK and runs it on a USB-connected phone (`AutoRunPlayer`). "iOS Xcode 프로젝트 만들기" generates the Xcode project and opens it.

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
