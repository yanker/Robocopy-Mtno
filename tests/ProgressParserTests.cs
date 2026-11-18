using System;
using CopiaSegura.Core;

namespace CopiaSegura.Tests
{
    public static class ProgressParserTests
    {
        [Test]
        public static void Parse_SpanishNewFile()
        {
            ParsedLine p = ProgressParser.Parse("\t\t    Nuevo arch  \t\t10619234816\tE:\\Usuarios\\Alejandro\\AppData\\backup_grande.dat");
            Check.Equal(LineKind.FileStart, p.Kind);
            Check.Equal(10619234816L, p.FileSize);
            Check.Equal(@"E:\Usuarios\Alejandro\AppData\backup_grande.dat", p.Path);
        }

        [Test]
        public static void Parse_EnglishNewFile()
        {
            ParsedLine p = ProgressParser.Parse("\t\t    New File  \t\t      1234\tE:\\a b\\c.txt");
            Check.Equal(LineKind.FileStart, p.Kind);
            Check.Equal(1234L, p.FileSize);
            Check.Equal(@"E:\a b\c.txt", p.Path);
        }

        [Test]
        public static void Parse_PercentLine()
        {
            ParsedLine a = ProgressParser.Parse("  45,3%");
            Check.Equal(LineKind.Percent, a.Kind);
            Check.True(Math.Abs(a.Percent - 45.3) < 0.001, "45,3");
            ParsedLine b = ProgressParser.Parse("100%");
            Check.Equal(LineKind.Percent, b.Kind);
            Check.Equal(100.0, b.Percent);
        }

        [Test]
        public static void Parse_ErrorLine()
        {
            ParsedLine p = ProgressParser.Parse("2026/11/18 10:00:00 ERROR 5 (0x00000005) Copying File E:\\x.tmp");
            Check.Equal(LineKind.Error, p.Kind);
            Check.True(p.Text.Contains("ERROR 5"));
        }

        [Test]
        public static void Parse_Garbage_IsOther()
        {
            Check.Equal(LineKind.Other, ProgressParser.Parse("").Kind);
            Check.Equal(LineKind.Other, ProgressParser.Parse("-------------------").Kind);
            Check.Equal(LineKind.Other, ProgressParser.Parse("   Total    Copiado   Omitido").Kind);
        }

        [Test]
        public static void Tracker_TwoFiles_ReachesHundred()
        {
            ProgressTracker t = new ProgressTracker(400);
            t.Feed(ProgressParser.Parse("\t\t    New File  \t\t       100\tE:\\a.bin"));
            t.Feed(ProgressParser.Parse("50%"));
            Check.True(Math.Abs(t.TotalPercent - 12.5) < 0.001, "12.5 era " + t.TotalPercent);
            Check.Equal(50.0, t.CurrentPercent);
            Check.Equal(@"E:\a.bin", t.CurrentFile);
            t.Feed(ProgressParser.Parse("\t\t    New File  \t\t       300\tE:\\b.bin"));
            Check.Equal(100L, t.ProcessedBytes);
            t.Finish();
            Check.True(Math.Abs(t.TotalPercent - 100.0) < 0.001, "100 era " + t.TotalPercent);
        }

        [Test]
        public static void Tracker_CountsErrors()
        {
            ProgressTracker t = new ProgressTracker(10);
            t.Feed(ProgressParser.Parse("2026/11/18 10:00:00 ERROR 32 (0x00000020) Copying File E:\\x"));
            Check.Equal(1, t.ErrorCount);
        }

        [Test]
        public static void Tracker_RetriedErrorCountsOnce()
        {
            ProgressTracker t = new ProgressTracker(10);
            t.Feed(ProgressParser.Parse("2026/11/18 10:00:00 ERROR 32 (0x00000020) Copying File E:\\x.dat"));
            t.Feed(ProgressParser.Parse("2026/11/18 10:00:03 ERROR 32 (0x00000020) Copying File E:\\x.dat"));
            t.Feed(ProgressParser.Parse("2026/11/18 10:00:05 ERROR 5 (0x00000005) Copying File E:\\y.dat"));
            Check.Equal(2, t.ErrorCount);
            Check.Equal("ERROR 32 (0x00000020) Copying File E:\\x.dat",
                ProgressParser.ErrorKey("2026/11/18 10:00:03 ERROR 32 (0x00000020) Copying File E:\\x.dat"));
        }

        [Test]
        public static void Tracker_ZeroTotal_NoDivideByZero_NothingToCopy()
        {
            ProgressTracker t = new ProgressTracker(0);
            t.Feed(ProgressParser.Parse("50%"));
            t.Finish();
            Check.Equal(0.0, t.TotalPercent);
            Check.True(t.NothingToCopy);
        }
    }
}
