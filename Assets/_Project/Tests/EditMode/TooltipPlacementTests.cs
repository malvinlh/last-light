using LastLight.Presentation.Common;
using NUnit.Framework;
using UnityEngine;

namespace LastLight.Tests.EditMode
{
    /// <summary>
    /// Where a tooltip lands, given a cursor and a canvas.
    /// </summary>
    /// <remarks>
    /// This fixture exists because of a shipped bug: the tooltip was positioned by mixing two
    /// coordinate origins - the cursor converted into canvas space, whose origin is the centre,
    /// assigned to a panel anchored at the canvas corner - so every tooltip appeared roughly half
    /// a screen from the pointer, and the ones belonging to bottom-left panels went off screen
    /// entirely and looked like they had never been wired up.
    ///
    /// A play-through did not catch it quickly; a test on the arithmetic would have. Hence the
    /// placement maths is a pure static function and hence these.
    ///
    /// Everything is expressed in canvas-local space: the origin is the canvas centre, and the
    /// returned position is the panel's TOP-LEFT corner.
    /// </remarks>
    [TestFixture]
    public sealed class TooltipPlacementTests
    {
        // A 1920x1080 canvas pivoted in the middle, which is how the real one is set up.
        private static readonly Rect Canvas = new Rect(-960f, -540f, 1920f, 1080f);
        private static readonly Vector2 Panel = new Vector2(400f, 90f);

        private static Rect RectFor(Vector2 position, Vector2 size) =>
            new Rect(position.x, position.y - size.y, size.x, size.y);

        private static void AssertInsideCanvas(Vector2 position, Vector2 size, string because)
        {
            Rect panel = RectFor(position, size);

            Assert.GreaterOrEqual(panel.xMin, Canvas.xMin, because);
            Assert.LessOrEqual(panel.xMax, Canvas.xMax, because);
            Assert.GreaterOrEqual(panel.yMin, Canvas.yMin, because);
            Assert.LessOrEqual(panel.yMax, Canvas.yMax, because);
        }

        [Test]
        public void InOpenSpaceThePanelSitsJustBelowAndRightOfTheCursor()
        {
            var cursor = new Vector2(0f, 0f);

            Vector2 position = TooltipView.ComputePosition(cursor, Panel, Canvas);

            Assert.Greater(position.x, cursor.x, "The panel should open to the right of the cursor.");
            Assert.Less(position.y, cursor.y, "The panel should hang below the cursor.");
            AssertInsideCanvas(position, Panel, "A tooltip in open space must be fully on screen.");
        }

        [Test]
        public void ThePanelTracksTheCursorRatherThanSittingAtAFixedSpot()
        {
            Vector2 left = TooltipView.ComputePosition(new Vector2(-600f, 200f), Panel, Canvas);
            Vector2 right = TooltipView.ComputePosition(new Vector2(-200f, 200f), Panel, Canvas);

            Assert.AreEqual(400f, right.x - left.x, 0.01f,
                "Moving the cursor 400 units should move the tooltip 400 units.");
        }

        [Test]
        public void NearTheRightEdgeThePanelFlipsToTheLeftOfTheCursor()
        {
            var cursor = new Vector2(900f, 0f);

            Vector2 position = TooltipView.ComputePosition(cursor, Panel, Canvas);

            Assert.Less(position.x + Panel.x, cursor.x, "The panel should be entirely left of the cursor.");
            AssertInsideCanvas(position, Panel, "Flipping must keep the panel on screen.");
        }

        [Test]
        public void NearTheBottomEdgeThePanelFlipsAboveTheCursor()
        {
            var cursor = new Vector2(0f, -500f);

            Vector2 position = TooltipView.ComputePosition(cursor, Panel, Canvas);

            Assert.Greater(position.y - Panel.y, cursor.y, "The panel should sit above the cursor.");
            AssertInsideCanvas(position, Panel, "Flipping must keep the panel on screen.");
        }

        [Test]
        public void InTheBottomRightCornerThePanelFlipsBothWays()
        {
            // This is the Discard box, and the mirror of it is the Focus box - the exact case that
            // was invisible before.
            var cursor = new Vector2(900f, -500f);

            Vector2 position = TooltipView.ComputePosition(cursor, Panel, Canvas);

            Assert.Less(position.x + Panel.x, cursor.x);
            Assert.Greater(position.y - Panel.y, cursor.y);
            AssertInsideCanvas(position, Panel, "A corner tooltip must still be fully readable.");
        }

        [Test]
        public void TheBottomLeftCornerIsOnScreenToo()
        {
            var cursor = new Vector2(-880f, -480f);

            Vector2 position = TooltipView.ComputePosition(cursor, Panel, Canvas);

            AssertInsideCanvas(position, Panel, "The Focus box tooltip must not fall off the canvas.");
        }

        [Test]
        public void NoCursorPositionOnTheCanvasPutsThePanelOffScreen()
        {
            for (float x = Canvas.xMin; x <= Canvas.xMax; x += 120f)
            {
                for (float y = Canvas.yMin; y <= Canvas.yMax; y += 120f)
                {
                    var cursor = new Vector2(x, y);
                    Vector2 position = TooltipView.ComputePosition(cursor, Panel, Canvas);

                    AssertInsideCanvas(position, Panel, $"Cursor at {cursor} pushed the tooltip off screen.");
                }
            }
        }

        [Test]
        public void APanelTooLargeToFitStillStartsInsideTheCanvas()
        {
            var oversized = new Vector2(2400f, 1400f);

            Vector2 position = TooltipView.ComputePosition(new Vector2(500f, 200f), oversized, Canvas);

            Assert.GreaterOrEqual(position.x, Canvas.xMin, "Its left edge must still be on screen.");
            Assert.LessOrEqual(position.y, Canvas.yMax, "Its top edge must still be on screen.");
        }
    }
}
