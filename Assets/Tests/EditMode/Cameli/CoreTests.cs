using System;
using System.Collections.Generic;
using System.Linq;
using Cameli.Core;
using NUnit.Framework;

namespace Cameli.Tests
{
    static class TestStages
    {
        public static StageDefinition Make(string target, string[] gates, params string[] rows) =>
            StageDefinition.FromFile(new StageFile { id = 1, rows = rows, target = target, gates = gates });

        /// <summary>
        /// R Y B
        /// Y S #
        /// B Y R      target: yellow 1 + blue 1
        /// </summary>
        public static StageDefinition Basic() => Make("YB", null, "R Y B", "Y S #", "B Y R");

        /// <summary><see cref="Basic"/> with its optimum (2 moves) stored, so clears earn stars by moves.</summary>
        public static StageDefinition BasicScored() => StageDefinition.FromFile(new StageFile
        {
            id = 1, rows = new[] { "R Y B", "Y S #", "B Y R" }, target = "YB", optimalMoves = 2,
        });

        /// <summary>
        /// Y B R
        /// g0 S R     g0 = orange gate (red 1 + yellow 1)
        /// # Y #      target: yellow 1 + blue 1
        /// </summary>
        public static StageDefinition WithGate() => Make("YB", new[] { "RY" }, "Y B R", "g0 S R", "# Y #");

        /// <summary>
        /// R R B
        /// # S B      target: red 1 + blue 1
        /// # # #
        /// </summary>
        public static StageDefinition Ratio() => Make("RB", null, "R R B", "# S B", "# # #");

        /// <summary>The layout of shipped stage 10 (5×5, orange gate used as a door), 7 moves at best.</summary>
        public static StageDefinition Door() => Make("YB", new[] { "RY" },
            "B R Y B Y", "R # g0 # R", "# B S R #", "B B R R Y", "B R Y R Y");
    }

    public class ColorMathTests
    {
        [Test]
        public void EmptyMixIsWhite()
        {
            Assert.AreEqual(Rgb.White, ColorMath.Mix(DropCounts.Empty));
        }

        [Test]
        public void RedPlusYellowIsOrange()
        {
            Assert.AreEqual(new Rgb(243, 109, 60), ColorMath.Mix(DropCounts.Parse("RY")));
        }

        [Test]
        public void SameRatioMixesTheSame()
        {
            Assert.AreEqual(ColorMath.Mix(DropCounts.Parse("RY")), ColorMath.Mix(DropCounts.Parse("RRYY")));
        }

        [Test]
        public void MatchAllowsTwoPerChannel()
        {
            Assert.IsTrue(ColorMath.Matches(new Rgb(10, 10, 10), new Rgb(12, 8, 10)));
            Assert.IsFalse(ColorMath.Matches(new Rgb(10, 10, 10), new Rgb(13, 10, 10)));
        }

        [Test]
        public void RecipeRoundTrips()
        {
            DropCounts drops = DropCounts.Parse("YRY");
            Assert.AreEqual(1, drops[Palette.IndexOf('R')]);
            Assert.AreEqual(2, drops[Palette.IndexOf('Y')]);
            Assert.AreEqual(3, drops.Total);
            Assert.AreEqual("RYY", drops.ToString());
        }
    }

    public class StageParsingTests
    {
        [Test]
        public void ParsesCellsAndStart()
        {
            StageDefinition stage = TestStages.Basic();
            Assert.AreEqual(3, stage.Width);
            Assert.AreEqual(3, stage.Height);
            Assert.AreEqual(4, stage.StartIndex);
            Assert.AreEqual(CellKind.Wall, stage.CellAt(5).Kind);
            Assert.AreEqual(Palette.IndexOf('B'), stage.CellAt(2).Color);
            Assert.AreEqual(2, stage.Target.Total);
        }

        [Test]
        public void EvenSizedStageStartsTopLeftOfCenter()
        {
            Assert.AreEqual(5, StageDefinition.CenterIndex(4, 4));
            Assert.DoesNotThrow(() => TestStages.Make("R", null, "R R R R", "R S R R", "R R R R", "R R R R"));
        }

        [Test]
        public void RejectsOffCenterStart()
        {
            Assert.Throws<StageFormatException>(() => TestStages.Make("R", null, "S R R", "R R R", "R R R"));
        }

        [Test]
        public void RejectsUnknownToken()
        {
            Assert.Throws<StageFormatException>(() => TestStages.Make("R", null, "R R R", "R S X", "R R R"));
        }

        [Test]
        public void RejectsMissingGate()
        {
            Assert.Throws<StageFormatException>(() => TestStages.Make("R", null, "R R R", "g0 S R", "R R R"));
        }

        [Test]
        public void RejectsRaggedRows()
        {
            Assert.Throws<StageFormatException>(() => TestStages.Make("R", null, "R R R", "R S", "R R R"));
        }

        [Test]
        public void MaxRecipeDropsCoversTargetAndGates()
        {
            Assert.AreEqual(2, TestStages.Basic().MaxRecipeDrops);
            Assert.AreEqual(3, TestStages.Make("R", new[] { "YYB" }, "Y Y B", "g0 S R", "R R R").MaxRecipeDrops);
        }

        [Test]
        public void RejectsBadTargetRecipe()
        {
            Assert.Throws<StageFormatException>(() => TestStages.Make("RX", null, "R R R", "R S R", "R R R"));
        }

        static StageDefinition WithTutorial(int[] focus, string swipe) => StageDefinition.FromFile(new StageFile
        {
            id = 1, rows = new[] { "Y Y Y", "Y S R", "Y Y Y" }, target = "R", focus = focus, swipe = swipe,
        });

        [Test]
        public void TutorialFieldsAreOptional()
        {
            StageDefinition stage = TestStages.Basic();
            Assert.AreEqual(-1, stage.FocusIndex);
            Assert.IsNull(stage.Swipe);

            stage = WithTutorial(new int[0], "");
            Assert.AreEqual(-1, stage.FocusIndex, "JsonUtility reads a missing array as empty");
            Assert.IsNull(stage.Swipe);
        }

        [Test]
        public void ParsesFocusAndSwipe()
        {
            StageDefinition stage = WithTutorial(new[] { 2, 1 }, "right");
            Assert.AreEqual(5, stage.FocusIndex, "x = column, y = row from the top");
            Assert.AreEqual(CellKind.Color, stage.CellAt(stage.FocusIndex).Kind);
            Assert.AreEqual(Direction.Right, stage.Swipe);
            Assert.AreEqual(Direction.Up, WithTutorial(null, "up").Swipe);
        }

        [Test]
        public void RejectsFocusOutsideTheBoard()
        {
            Assert.Throws<StageFormatException>(() => WithTutorial(new[] { 3, 1 }, null));
            Assert.Throws<StageFormatException>(() => WithTutorial(new[] { 0, -1 }, null));
            Assert.Throws<StageFormatException>(() => WithTutorial(new[] { 1 }, null));
        }

        [Test]
        public void RejectsUnknownSwipe()
        {
            Assert.Throws<StageFormatException>(() => WithTutorial(null, "north"));
            Assert.Throws<StageFormatException>(() => WithTutorial(null, "Right"));
        }

        [Test]
        public void RejectsFocusOnAWallOrTheStart()
        {
            // Y # Y
            // Y S R
            // Y Y Y
            StageFile File(int[] focus) => new StageFile
            {
                id = 1, rows = new[] { "Y # Y", "Y S R", "Y Y Y" }, target = "R", focus = focus,
            };
            Assert.Throws<StageFormatException>(() => StageDefinition.FromFile(File(new[] { 1, 0 })), "wall");
            Assert.Throws<StageFormatException>(() => StageDefinition.FromFile(File(new[] { 1, 1 })), "start");
            Assert.AreEqual(5, StageDefinition.FromFile(File(new[] { 2, 1 })).FocusIndex);
        }

        [Test]
        public void RejectsSwipeIntoAWallOrOffTheBoard()
        {
            // Y # Y
            // Y S R
            // Y Y Y
            StageFile Walled(string swipe) => new StageFile
            {
                id = 1, rows = new[] { "Y # Y", "Y S R", "Y Y Y" }, target = "R", swipe = swipe,
            };
            Assert.Throws<StageFormatException>(() => StageDefinition.FromFile(Walled("up")), "wall above the start");
            Assert.AreEqual(Direction.Right, StageDefinition.FromFile(Walled("right")).Swipe);

            // S R      on a 2x2 board the start is the top-left cell
            // Y Y
            StageFile Small(string swipe) => new StageFile { id = 1, rows = new[] { "S R", "Y Y" }, target = "R", swipe = swipe };
            Assert.Throws<StageFormatException>(() => StageDefinition.FromFile(Small("up")), "off the top edge");
            Assert.Throws<StageFormatException>(() => StageDefinition.FromFile(Small("left")), "off the left edge");
            Assert.AreEqual(Direction.Down, StageDefinition.FromFile(Small("down")).Swipe);
        }
    }

    public class RulesTests
    {
        [Test]
        public void SteppingOnColorEatsItAndLeavesFloor()
        {
            var session = new GameSession(TestStages.Basic());
            Assert.AreEqual(MoveOutcome.Ate, session.Move(Direction.Up));
            Assert.AreEqual(1, session.State.Position);
            Assert.AreEqual("Y", session.State.Drops.ToString());
            Assert.AreEqual(CellKind.Floor, Rules.VisibleKind(session.Stage, session.State, 1));
            Assert.AreEqual(1, session.State.Moves);
        }

        [Test]
        public void FloorAndStartMixNothing()
        {
            var session = new GameSession(TestStages.Basic());
            session.Move(Direction.Up);
            Assert.AreEqual(MoveOutcome.Moved, session.Move(Direction.Down));
            Assert.AreEqual(MoveOutcome.Moved, session.Move(Direction.Up));
            Assert.AreEqual("Y", session.State.Drops.ToString());
            Assert.AreEqual(3, session.State.Moves);
        }

        [Test]
        public void WallsAndEdgesBlockWithoutUsingAMove()
        {
            var session = new GameSession(TestStages.Basic());
            Assert.AreEqual(MoveOutcome.BlockedByWall, session.Move(Direction.Right));
            session.Move(Direction.Up);
            Assert.AreEqual(MoveOutcome.BlockedByEdge, session.Move(Direction.Up));
            Assert.AreEqual(1, session.State.Moves);
        }

        [Test]
        public void MatchingTheTargetClearsImmediately()
        {
            var session = new GameSession(TestStages.Basic());
            session.Move(Direction.Up);
            Assert.AreEqual(MoveOutcome.Cleared, session.Move(Direction.Right));
            Assert.IsTrue(session.IsCleared);
            Assert.AreEqual(MoveOutcome.AlreadyCleared, session.Move(Direction.Left));
        }

        static StageDefinition RatioStage() => TestStages.Ratio();

        [Test]
        public void ExactDropCountClears()
        {
            var session = new GameSession(RatioStage());
            session.Move(Direction.Up);
            Assert.AreEqual(MoveOutcome.Cleared, session.Move(Direction.Right));
        }

        [Test]
        public void RatioMultipleOfTheTargetDoesNotClear()
        {
            var session = new GameSession(RatioStage());
            session.Move(Direction.Up);
            session.Move(Direction.Left);
            session.Move(Direction.Right);
            session.Move(Direction.Right);
            Assert.AreEqual(MoveOutcome.Ate, session.Move(Direction.Down));
            Assert.AreEqual("RRBB", session.State.Drops.ToString());
            Assert.IsTrue(ColorMath.Matches(session.CurrentColor, session.Stage.TargetColor), "same color as the target");
            Assert.IsFalse(session.IsCleared, "but four drops are not the target's two");
        }

        [Test]
        public void OverfilledOnceDropsExceedEveryRecipe()
        {
            var session = new GameSession(RatioStage());
            session.Move(Direction.Up);
            session.Move(Direction.Left);
            Assert.IsFalse(Rules.IsOverfilled(session.Stage, session.State), "RR: two drops can still be used");

            session.Move(Direction.Right);
            session.Move(Direction.Right);
            Assert.IsTrue(Rules.IsOverfilled(session.Stage, session.State), "RRB: three drops for a two-drop target");

            session.Restart();
            Assert.IsFalse(Rules.IsOverfilled(session.Stage, session.State));
        }

        [Test]
        public void GateRejectsRatioMultipleOfItsRecipe()
        {
            // Y R R
            // g0 S Y     g0 = orange gate (red 1 + yellow 1)
            // # # #
            var session = new GameSession(TestStages.Make("B", new[] { "RY" }, "Y R R", "g0 S Y", "# # #"));
            session.Move(Direction.Up);
            session.Move(Direction.Right);
            session.Move(Direction.Down);
            session.Move(Direction.Left);
            session.Move(Direction.Up);
            session.Move(Direction.Left);
            Assert.AreEqual("RRYY", session.State.Drops.ToString());
            Assert.IsTrue(ColorMath.Matches(session.CurrentColor, session.Stage.Gates[0].Color), "same color as the gate");
            Assert.AreEqual(MoveOutcome.BlockedByGate, session.Move(Direction.Down));
        }

        [Test]
        public void GateBlocksUntilTheColorMatchesThenTakesIt()
        {
            var session = new GameSession(TestStages.WithGate());
            Assert.AreEqual(MoveOutcome.BlockedByGate, session.Move(Direction.Left));

            session.Move(Direction.Right);
            session.Move(Direction.Left);
            Assert.AreEqual(MoveOutcome.BlockedByGate, session.Move(Direction.Left), "red alone is not orange");

            session.Move(Direction.Down);
            session.Move(Direction.Up);
            Assert.AreEqual(MoveOutcome.PassedGate, session.Move(Direction.Left));
            Assert.IsTrue(session.State.Drops.IsEmpty, "a consuming gate empties the mix");
            Assert.AreEqual(Rgb.White, session.CurrentColor);

            session.Move(Direction.Up);
            Assert.AreEqual(MoveOutcome.Cleared, session.Move(Direction.Right));
        }

        [Test]
        public void RestartGoesBackToTheStartAndCounts()
        {
            var session = new GameSession(TestStages.Basic());
            session.Move(Direction.Up);
            Assert.IsTrue(session.CanRestart);
            Assert.IsTrue(session.Restart());
            Assert.AreEqual(session.Stage.StartIndex, session.State.Position);
            Assert.AreEqual(0UL, session.State.Eaten);
            Assert.AreEqual(0, session.State.Moves);
            Assert.IsTrue(session.State.Drops.IsEmpty);
            Assert.AreEqual(0, session.MixOrder.Count);
            Assert.AreEqual(1, session.Restarts);
        }

        [Test]
        public void RestartBeforeTheFirstMoveDoesNothing()
        {
            var session = new GameSession(TestStages.Basic());
            Assert.IsFalse(session.CanRestart);
            Assert.IsFalse(session.Restart());
            Assert.AreEqual(0, session.Restarts);

            int color = session.RevealColor(new Random(1));
            Assert.IsFalse(session.Restart(), "still no move");
            CollectionAssert.AreEqual(new[] { color }, session.RevealedColors, "a free restart can't hide a hint");
            Assert.IsTrue(session.UsedHint);
        }

        [Test]
        public void NoRestartAfterClearingButStartOverBeginsAFreshVisit()
        {
            var session = new GameSession(TestStages.Basic());
            session.Move(Direction.Left);
            session.Restart();
            session.Move(Direction.Up);
            session.Move(Direction.Right);
            Assert.IsTrue(session.IsCleared);
            Assert.IsFalse(session.Restart());
            Assert.AreEqual(1, session.Restarts);
            Assert.AreEqual(2, session.Stars);

            session.StartOver();
            Assert.IsFalse(session.IsCleared);
            Assert.AreEqual(0, session.Restarts);
            Assert.AreEqual(0, session.State.Moves);
            session.Move(Direction.Up);
            session.Move(Direction.Right);
            Assert.AreEqual(3, session.Stars);
        }

        [Test]
        public void StarsCountRestarts()
        {
            Assert.AreEqual(3, Scoring.Stars(0, false));
            Assert.AreEqual(2, Scoring.Stars(1, false));
            Assert.AreEqual(1, Scoring.Stars(2, false));
            Assert.AreEqual(1, Scoring.Stars(7, false));
        }

        [Test]
        public void HintedClearsEarnAtMostTwoStars()
        {
            Assert.AreEqual(2, Scoring.Stars(0, true));
            Assert.AreEqual(2, Scoring.Stars(1, true));
            Assert.AreEqual(1, Scoring.Stars(2, true));
        }

        [Test]
        public void MovesDoNotAffectTheStars()
        {
            var session = new GameSession(TestStages.BasicScored());
            session.Move(Direction.Up);
            session.Move(Direction.Down);
            session.Move(Direction.Up);
            session.Move(Direction.Right);
            Assert.IsTrue(session.IsCleared);
            Assert.AreEqual(4, session.State.Moves, "twice the optimum");
            Assert.AreEqual(3, session.Stars);
        }

        [Test]
        public void StarsIfClearedFollowRestartsAndHints()
        {
            var session = new GameSession(TestStages.Basic());
            Assert.AreEqual(3, session.StarsIfCleared);
            Assert.AreEqual(0, session.Stars, "not cleared yet");
            session.RevealColor(new Random(1));
            Assert.AreEqual(2, session.StarsIfCleared);
            session.Move(Direction.Left);
            session.Restart();
            Assert.AreEqual(2, session.StarsIfCleared, "the restart hid the hint, and one restart is two stars");
            session.Move(Direction.Left);
            session.Restart();
            Assert.AreEqual(1, session.StarsIfCleared);
        }

        [Test]
        public void HintsRevealEachRecipeColorOnceThenStop()
        {
            var session = new GameSession(TestStages.Basic()); // target YB
            Assert.IsTrue(session.CanRevealColor);
            int first = session.RevealColor(new Random(3));
            int second = session.RevealColor(new Random(3));
            CollectionAssert.AreEquivalent(new[] { Palette.IndexOf('Y'), Palette.IndexOf('B') }, new[] { first, second });
            CollectionAssert.AreEqual(new[] { first, second }, session.RevealedColors, "in reveal order");
            Assert.IsFalse(session.CanRevealColor);
            Assert.AreEqual(-1, session.RevealColor(new Random(3)));
            Assert.AreEqual(2, session.RevealedColors.Count);
        }

        [Test]
        public void HintPicksAmongTheHiddenColorsAtRandom()
        {
            var seen = new HashSet<int>();
            for (int seed = 0; seed < 40; seed++)
                seen.Add(new GameSession(TestStages.Basic()).RevealColor(new Random(seed)));
            CollectionAssert.AreEquivalent(new[] { Palette.IndexOf('Y'), Palette.IndexOf('B') }, seen);
        }

        [Test]
        public void HintCapsTheStars()
        {
            var session = new GameSession(TestStages.BasicScored());
            session.RevealColor(new Random(1));
            session.Move(Direction.Up);
            session.Move(Direction.Right);
            Assert.AreEqual(2, session.Stars);
        }

        [Test]
        public void RestartHidesTheHintColors()
        {
            var session = new GameSession(TestStages.Basic());
            session.RevealColor(new Random(1));
            session.Move(Direction.Left);
            session.Restart();
            Assert.AreEqual(0, session.RevealedColors.Count);
            Assert.IsFalse(session.UsedHint);
            Assert.IsTrue(session.CanRevealColor);
        }

        [Test]
        public void NoHintAfterClearing()
        {
            var session = new GameSession(TestStages.Basic());
            session.Move(Direction.Up);
            session.Move(Direction.Right);
            Assert.IsFalse(session.CanRevealColor);
            Assert.AreEqual(-1, session.RevealColor(new Random(1)));
            Assert.AreEqual(3, session.Stars);
        }

        [Test]
        public void MixOrderFollowsEatingAndEmptiesAtAGate()
        {
            var basic = new GameSession(TestStages.Basic());
            basic.Move(Direction.Left);  // Y
            basic.Move(Direction.Right); // back to the start: floor
            basic.Move(Direction.Up);    // Y
            CollectionAssert.AreEqual(new[] { Palette.IndexOf('Y'), Palette.IndexOf('Y') }, basic.MixOrder);

            var gate = new GameSession(TestStages.WithGate());
            gate.Move(Direction.Right); // R
            gate.Move(Direction.Left);
            gate.Move(Direction.Down);  // Y
            CollectionAssert.AreEqual(new[] { Palette.IndexOf('R'), Palette.IndexOf('Y') }, gate.MixOrder);
            gate.Move(Direction.Up);
            Assert.AreEqual(MoveOutcome.PassedGate, gate.Move(Direction.Left));
            Assert.AreEqual(0, gate.MixOrder.Count);
        }
    }

    public class SolverTests
    {
        [Test]
        public void FindsAShortestClear()
        {
            // Y R Y
            // B S B      red 1 + blue 1: no two-move path, the shortest goes back over the start cell.
            // Y R Y
            StageDefinition stage = TestStages.Make("RB", null, "Y R Y", "B S B", "Y R Y");
            Solution solution = Solver.Solve(stage);
            Assert.IsNotNull(solution);
            Assert.AreEqual(3, solution.Moves);

            var session = new GameSession(stage);
            foreach (Direction direction in solution.Path) session.Move(direction);
            Assert.IsTrue(session.IsCleared);
        }

        [Test]
        public void SolvesThroughAGate()
        {
            Solution solution = Solver.Solve(TestStages.WithGate());
            Assert.IsNotNull(solution);
            Assert.AreEqual(2, solution.Moves, "blue then yellow next to the start is shorter than the gate route");
        }

        [Test]
        public void ReportsUnsolvableStages()
        {
            Assert.IsNull(Solver.Solve(TestStages.Make("B", null, "R R R", "R S R", "R R R")));
        }

        [Test]
        public void TrySolveReportsUnsolvable()
        {
            StageDefinition stage = TestStages.Make("B", null, "R R R", "R S R", "R R R");
            Assert.AreEqual(SearchResult.Unsolvable, Solver.TrySolve(stage, BoardState.Initial(stage), out Solution solution));
            Assert.IsNull(solution);
        }

        [Test]
        public void SolvesFromAGivenState()
        {
            var session = new GameSession(TestStages.Basic());
            session.Move(Direction.Up);
            Solution solution = Solver.Solve(session.Stage, session.State);
            CollectionAssert.AreEqual(new[] { Direction.Right }, solution.Path, "only the moves after the given state");
            Assert.AreEqual(2, solution.Final.Moves);
        }

        [Test]
        public void ClearedStartNeedsNoMoves()
        {
            var session = new GameSession(TestStages.Basic());
            session.Move(Direction.Up);
            session.Move(Direction.Right);
            Assert.AreEqual(0, Solver.Solve(session.Stage, session.State).Moves);
        }

        [Test]
        public void ReportsTheStateCap()
        {
            StageDefinition stage = TestStages.Door();
            Assert.AreEqual(SearchResult.LimitExceeded, Solver.TrySolve(stage, BoardState.Initial(stage), out Solution solution, maxStates: 5));
            Assert.IsNull(solution);
            Assert.Throws<SearchLimitException>(() => Solver.Solve(stage, maxStates: 5));
            Assert.AreEqual(SearchResult.Found, Solver.TrySolve(stage, BoardState.Initial(stage), out solution));
            Assert.AreEqual(7, solution.Moves);
        }
    }

    public class StageAuditTests
    {
        /// <summary>
        /// Shipped stage 10 (5×5, orange-purple gate used as a door), 7 moves at best.
        /// P P P B O
        /// P # g0 # P
        /// # B S B #
        /// P O O B O
        /// B P P B O      target: blue 1 + purple 1, g0 = orange 1 + purple 1
        /// </summary>
        static StageFile DoorStage(int optimalMoves = 7) => new StageFile
        {
            id = 10,
            rows = new[] { "P P P B O", "P # g0 # P", "# B S B #", "P O O B O", "B P P B O" },
            target = "BP",
            gates = new[] { "OP" },
            optimalMoves = optimalMoves,
        };

        [Test]
        public void GoodStagePasses()
        {
            StageReport report = StageAudit.Audit(DoorStage());
            Assert.AreEqual(StageProblems.None, report.Problems);
            Assert.AreEqual(7, report.Optimal);
            Assert.AreEqual(GateUse.Door, report.GateUse);
            Assert.IsTrue(report.GatesExact);
            Assert.AreEqual("OBP", report.Colors);
            Assert.AreEqual("BB", report.TargetNearest);
            Assert.AreEqual(28.7, report.TargetDeltaE, 0.05);
            Assert.AreEqual("OP~OO", report.GateNearest);
        }

        [Test]
        public void WrongOptimalMovesIsReported()
        {
            StageReport report = StageAudit.Audit(DoorStage(optimalMoves: 8));
            Assert.AreEqual(StageProblems.OptimalMismatch, report.Problems);
            Assert.AreEqual(8, report.StoredOptimal);
            Assert.AreEqual(7, report.Optimal);
        }

        [Test]
        public void AmbiguousTargetIsReported()
        {
            // Navy 2 + blue 1 mixes about ΔE 6 from navy 3, too close to tell apart.
            StageReport report = StageAudit.Audit(new StageFile
            {
                id = 1, rows = new[] { "N B N", "B S B", "N B N" }, target = "NNB", optimalMoves = 4,
            });
            Assert.AreEqual(StageProblems.TargetAmbiguous, report.Problems);
            Assert.AreEqual("NNN", report.TargetNearest);
            Assert.Less(report.TargetDeltaE, StageAudit.AmbiguityThreshold);
        }

        [Test]
        public void GateNotNeededAsDoorIsReported()
        {
            // TestStages.WithGate: blue then yellow clears without ever touching the orange gate.
            StageReport report = StageAudit.Audit(new StageFile
            {
                id = 1, rows = new[] { "Y B R", "g0 S R", "# Y #" }, target = "YB", gates = new[] { "RY" }, optimalMoves = 2,
            });
            Assert.AreEqual(GateUse.Skippable, report.GateUse);
            Assert.AreEqual(StageProblems.GateSkippable, report.Problems);
        }

        [Test]
        public void UnsolvableStageIsReported()
        {
            StageReport report = StageAudit.Audit(new StageFile
            {
                id = 1, rows = new[] { "Y Y Y", "Y S Y", "Y Y Y" }, target = "B", optimalMoves = 1,
            });
            Assert.IsFalse(report.Solvable);
            Assert.AreEqual(-1, report.Optimal);
            Assert.AreEqual(StageProblems.Unsolvable, report.Problems);
        }

        [Test]
        public void WithoutGatesWallsOffGateCells()
        {
            StageFile file = StageAudit.WithoutGates(DoorStage());
            Assert.AreEqual("P P P B O", file.rows[0]);
            Assert.AreEqual("P # # # P", file.rows[1]);
        }
    }

    public class StageSectionTests
    {
        [Test]
        public void FoldersAreKnownInPlayOrder()
        {
            Assert.AreEqual(0, StageSections.IndexOf("0_Tutorial"));
            Assert.AreEqual(3, StageSections.IndexOf("3_Hard"));
            Assert.AreEqual(-1, StageSections.IndexOf("CameliStages"));
            Assert.AreEqual(-1, StageSections.IndexOf("1_easy"), "folder names are case-sensitive, like Resources paths");
        }

        [Test]
        public void IdsFollowingTheFoldersPass()
        {
            Assert.IsNull(StageSections.FirstOutOfOrder(new[] { (3, 1), (1, 0), (2, 0), (4, 1), (5, 2) }));
            Assert.IsNull(StageSections.FirstOutOfOrder(new (int, int)[0]));
        }

        [Test]
        public void ReportsTheFirstIdInAnEarlierFolder()
        {
            // id 3 is in 1_Easy but id 4 went back to 0_Tutorial.
            var order = StageSections.FirstOutOfOrder(new[] { (1, 0), (2, 0), (3, 1), (4, 0), (5, 1) });
            Assert.AreEqual((4, 0, 3, 1), order);
        }

        [Test]
        public void FromFileKeepsTheSection()
        {
            var file = new StageFile { id = 1, rows = new[] { "R Y B", "Y S #", "B Y R" }, target = "YB" };
            Assert.AreEqual(0, StageDefinition.FromFile(file).Section);
            Assert.AreEqual(2, StageDefinition.FromFile(file, 2).Section);
        }
    }
}
