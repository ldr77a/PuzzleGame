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

        /// <summary>
        /// Y B R
        /// g0 S R     g0 = orange gate (red 1 + yellow 1)
        /// # Y #      target: yellow 1 + blue 1
        /// </summary>
        public static StageDefinition WithGate() => Make("YB", new[] { "RY" }, "Y B R", "g0 S R", "# Y #");
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

        /// <summary>
        /// R R B
        /// # S B      target: red 1 + blue 1
        /// # # #
        /// </summary>
        static StageDefinition RatioStage() => TestStages.Make("RB", null, "R R B", "# S B", "# # #");

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
    }
}
