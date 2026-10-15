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

            session.Undo();
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
        public void UndoRestoresTheEatenCellAndMix()
        {
            var session = new GameSession(TestStages.Basic());
            session.Move(Direction.Up);
            Assert.IsTrue(session.Undo());
            Assert.AreEqual(session.Stage.StartIndex, session.State.Position);
            Assert.IsTrue(session.State.Drops.IsEmpty);
            Assert.AreEqual(CellKind.Color, Rules.VisibleKind(session.Stage, session.State, 1));
            Assert.AreEqual(GameSession.UndoAllowance - 1, session.UndosRemaining);
        }

        [Test]
        public void UndoIsLimitedPerAttempt()
        {
            var session = new GameSession(TestStages.Basic());
            for (int i = 0; i < 5; i++)
            {
                session.Move(Direction.Up);
                session.Move(Direction.Down);
            }
            for (int i = 0; i < GameSession.UndoAllowance; i++) Assert.IsTrue(session.Undo());
            Assert.IsFalse(session.CanUndo);
            Assert.IsFalse(session.Undo());
        }

        [Test]
        public void NoUndoAfterClearing()
        {
            var session = new GameSession(TestStages.Basic());
            session.Move(Direction.Up);
            session.Move(Direction.Right);
            Assert.IsFalse(session.Undo());
        }

        [Test]
        public void RestartResetsBoardAndUndos()
        {
            var session = new GameSession(TestStages.Basic());
            session.Move(Direction.Up);
            session.Undo();
            session.Move(Direction.Left);
            session.Restart();
            Assert.AreEqual(BoardState.Initial(session.Stage).Position, session.State.Position);
            Assert.AreEqual(0UL, session.State.Eaten);
            Assert.AreEqual(0, session.State.Moves);
            Assert.AreEqual(GameSession.UndoAllowance, session.UndosRemaining);
        }

        [Test]
        public void StarsCompareMovesToOptimal()
        {
            Assert.AreEqual(3, Scoring.Stars(4, 4));
            Assert.AreEqual(2, Scoring.Stars(6, 4));
            Assert.AreEqual(1, Scoring.Stars(7, 4));
        }

        [Test]
        public void AssistedClearsEarnAtMostTwoStars()
        {
            Assert.AreEqual(3, Scoring.Stars(4, 4, false));
            Assert.AreEqual(2, Scoring.Stars(4, 4, true));
            Assert.AreEqual(2, Scoring.Stars(6, 4, true));
            Assert.AreEqual(1, Scoring.Stars(7, 4, true));
        }

        [Test]
        public void UndoCapsTheStars()
        {
            var session = new GameSession(TestStages.BasicScored());
            Assert.IsFalse(session.Undo(), "nothing to undo yet");
            Assert.IsFalse(session.UsedUndo, "a refused undo does not count");

            session.Move(Direction.Left);
            session.Undo();
            Assert.IsTrue(session.UsedUndo);
            session.Move(Direction.Up);
            session.Move(Direction.Right);
            Assert.AreEqual(2, session.State.Moves, "optimal after the undo");
            Assert.AreEqual(2, session.Stars);
        }

        [Test]
        public void StepHintCapsTheStars()
        {
            var session = new GameSession(TestStages.BasicScored());
            session.MarkStepHint();
            Assert.IsTrue(session.UsedStepHint);
            Assert.IsTrue(session.Assisted);
            session.Move(Direction.Up);
            session.Move(Direction.Right);
            Assert.AreEqual(2, session.Stars);
        }

        [Test]
        public void StepHintAfterClearingIsIgnored()
        {
            var session = new GameSession(TestStages.BasicScored());
            session.Move(Direction.Up);
            session.Move(Direction.Right);
            session.MarkStepHint();
            Assert.IsFalse(session.UsedStepHint);
            Assert.AreEqual(3, session.Stars);
        }

        [Test]
        public void RestartForgetsAssists()
        {
            var session = new GameSession(TestStages.BasicScored());
            session.Move(Direction.Left);
            session.Undo();
            session.MarkStepHint();
            session.Restart();
            Assert.IsFalse(session.UsedUndo);
            Assert.IsFalse(session.UsedStepHint);
            session.Move(Direction.Up);
            session.Move(Direction.Right);
            Assert.AreEqual(3, session.Stars);
        }

        [Test]
        public void GrantedUndosCanBeUsedAndStillCapTheStars()
        {
            var session = new GameSession(TestStages.BasicScored());
            for (int i = 0; i < GameSession.UndoAllowance; i++)
            {
                session.Move(Direction.Left);
                Assert.IsTrue(session.Undo());
            }
            session.Move(Direction.Left);
            Assert.AreEqual(0, session.UndosRemaining);
            Assert.IsFalse(session.CanUndo, "out of undos");

            session.GrantUndos(0);
            session.GrantUndos(-2);
            Assert.AreEqual(0, session.UndosRemaining, "non-positive grants are ignored");

            int history = session.HistoryCount;
            session.GrantUndos(3);
            Assert.AreEqual(3, session.UndosRemaining);
            Assert.AreEqual(history, session.HistoryCount, "granting keeps the history");
            Assert.IsTrue(session.UsedUndo, "granting keeps the undo use");
            Assert.IsTrue(session.Undo());
            Assert.AreEqual(2, session.UndosRemaining);
            session.Move(Direction.Up);
            session.Move(Direction.Right);
            Assert.AreEqual(2, session.Stars, "still assisted");

            session.Restart();
            Assert.AreEqual(GameSession.UndoAllowance, session.UndosRemaining, "restart refills to the allowance");
        }

        [Test]
        public void GrantingUndosIsNotAnAssist()
        {
            var session = new GameSession(TestStages.BasicScored());
            session.GrantUndos(3);
            Assert.AreEqual(GameSession.UndoAllowance + 3, session.UndosRemaining);
            Assert.IsFalse(session.UsedUndo);
            Assert.AreEqual(0, session.HistoryCount);
            session.Move(Direction.Up);
            session.Move(Direction.Right);
            Assert.AreEqual(3, session.Stars);
        }

        [Test]
        public void HistoryListsEarlierStatesMostRecentFirst()
        {
            var session = new GameSession(TestStages.Basic());
            session.Move(Direction.Up);
            session.Move(Direction.Down);
            Assert.AreEqual(2, session.HistoryCount);
            CollectionAssert.AreEqual(new[] { 1, session.Stage.StartIndex }, session.History.Select(s => s.Position).ToArray());
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
            Assert.Throws<System.InvalidOperationException>(() => Solver.Solve(stage, maxStates: 5));
            Assert.AreEqual(SearchResult.Found, Solver.TrySolve(stage, BoardState.Initial(stage), out solution));
            Assert.AreEqual(7, solution.Moves);
        }
    }

    public class HintTests
    {
        [Test]
        public void FollowingNextStepHintsClearsInTheOptimalMoves()
        {
            var session = new GameSession(TestStages.Door());
            while (!session.IsCleared && session.State.Moves < 20)
            {
                Hint hint = Hints.Next(session);
                Assert.AreEqual(HintKind.NextStep, hint.Kind);
                Assert.IsTrue(session.Move(hint.Direction).IsMove());
            }
            Assert.IsTrue(session.IsCleared);
            Assert.AreEqual(7, session.State.Moves);
        }

        [Test]
        public void NextStepAfterAWrongTurnStillLeadsToAClear()
        {
            var session = new GameSession(TestStages.Basic());
            session.Move(Direction.Down);
            Hint hint = Hints.Next(session);
            Assert.AreEqual(HintKind.NextStep, hint.Kind);
            Assert.AreEqual(Direction.Left, hint.Direction, "yellow below the start, then the blue beside it");
            Assert.AreEqual(MoveOutcome.Cleared, session.Move(hint.Direction));
        }

        [Test]
        public void DeadEndReportsTheFewestUndos()
        {
            // Two reds leave no way to the target red 1 + blue 1; the state two moves back (one red) has one.
            var session = new GameSession(TestStages.Ratio());
            session.Move(Direction.Up);
            session.Move(Direction.Left);
            session.Move(Direction.Right);
            Hint hint = Hints.Next(session);
            Assert.AreEqual(HintKind.Stuck, hint.Kind);
            Assert.AreEqual(2, hint.UndosNeeded);
            Assert.IsTrue(hint.UndoWithinReach);

            for (int i = 0; i < hint.UndosNeeded; i++) session.Undo();
            Assert.AreEqual(HintKind.NextStep, Hints.Next(session).Kind);
        }

        [Test]
        public void DeadEndBeyondTheRemainingUndosSaysRestart()
        {
            var session = new GameSession(TestStages.Ratio());
            session.Move(Direction.Up);
            session.Move(Direction.Left);
            session.Move(Direction.Right);
            session.Move(Direction.Left);
            session.Undo();
            session.Move(Direction.Left);
            session.Undo();
            Assert.AreEqual(1, session.UndosRemaining);
            Hint hint = Hints.Next(session);
            Assert.AreEqual(HintKind.Stuck, hint.Kind);
            Assert.AreEqual(2, hint.UndosNeeded);
            Assert.IsFalse(hint.UndoWithinReach);
        }

        [Test]
        public void ClearedSessionNeedsNoHint()
        {
            var session = new GameSession(TestStages.Basic());
            session.Move(Direction.Up);
            session.Move(Direction.Right);
            Assert.AreEqual(HintKind.AlreadyCleared, Hints.Next(session).Kind);
        }

        [Test]
        public void SearchCapGivesNoHint()
        {
            var session = new GameSession(TestStages.Door());
            Assert.AreEqual(HintKind.SearchLimit, Hints.Next(session, maxStates: 5).Kind);
        }

        [Test]
        public void HintsDoNotChangeTheSession()
        {
            var session = new GameSession(TestStages.Door());
            Hints.Next(session);
            Assert.IsFalse(session.UsedStepHint);
            Assert.AreEqual(0, session.State.Moves);
        }
    }
}
