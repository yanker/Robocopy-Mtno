using System;

namespace CopiaSegura.Tests
{
    public static class HarnessTests
    {
        [Test]
        public static void Sanity_AddsUp()
        {
            Check.Equal(4, 2 + 2);
        }

        [Test]
        public static void Check_Throws_DetectsException()
        {
            Check.Throws<InvalidOperationException>(delegate { throw new InvalidOperationException(); });
        }
    }
}
