using SMS_Search.Utils;
using Xunit;

namespace SMSSearch.Tests
{
    public class FloatingWindowSnapTests
    {
        [Fact]
        public void ComputeSnappedRect_SnapsLeftToTargetRight_AndPreservesDimensions()
        {
            var target = new SnapRect(100, 100, 400, 300);
            var candidate = new SnapRect(408, 120, 608, 320); // 8px from target.Right (400)

            var snapped = FloatingWindowSnapBehavior.ComputeSnappedRect(candidate, new[] { target }, 12);

            Assert.Equal(400, snapped.Left);
            Assert.Equal(120, snapped.Top);
            Assert.Equal(200, snapped.Width);
            Assert.Equal(200, snapped.Height);
        }

        [Fact]
        public void ComputeSnappedRect_SnapsTopToTargetBottom_AndAlignsLeftEdge()
        {
            var target = new SnapRect(100, 100, 400, 300);
            var candidate = new SnapRect(106, 309, 306, 509); // 6px from Left, 9px from Bottom

            var snapped = FloatingWindowSnapBehavior.ComputeSnappedRect(candidate, new[] { target }, 12);

            Assert.Equal(100, snapped.Left);
            Assert.Equal(300, snapped.Top);
            Assert.Equal(200, snapped.Width);
            Assert.Equal(200, snapped.Height);
        }

        [Fact]
        public void ComputeSnappedRect_OutsideThreshold_DoesNotSnap()
        {
            var target = new SnapRect(100, 100, 400, 300);
            var candidate = new SnapRect(425, 120, 625, 320); // 25px away

            var snapped = FloatingWindowSnapBehavior.ComputeSnappedRect(candidate, new[] { target }, 12);

            Assert.Equal(candidate, snapped);
        }
    }
}

