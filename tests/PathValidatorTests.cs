using System;
using System.Collections.Generic;
using System.IO;
using CopiaSegura.Core;

namespace CopiaSegura.Tests
{
    public static class PathValidatorTests
    {
        static bool Has(List<CheckResult> l, Severity s)
        {
            foreach (CheckResult c in l) if (c.Severity == s) return true;
            return false;
        }

        [Test]
        public static void DestinationInsideSource_IsError()
        {
            string src = Tmp.Create();
            try
            {
                List<CheckResult> r = PathValidator.Validate(src, Path.Combine(src, "sub"), 10, long.MaxValue);
                Check.True(Has(r, Severity.Error));
            }
            finally { Tmp.Delete(src); }
        }

        [Test]
        public static void SameFolder_IsError()
        {
            string src = Tmp.Create();
            try
            {
                Check.True(Has(PathValidator.Validate(src, src + "\\", 10, long.MaxValue), Severity.Error));
                Check.True(Has(PathValidator.Validate(src, src.ToUpperInvariant(), 10, long.MaxValue), Severity.Error));
            }
            finally { Tmp.Delete(src); }
        }

        [Test]
        public static void MissingSourceOrEmpty_IsError()
        {
            Check.True(Has(PathValidator.Validate(@"Z:\no\existe\jamas", @"D:\x", 0, 0), Severity.Error));
            Check.True(Has(PathValidator.Validate("", "", 0, 0), Severity.Error));
        }

        [Test]
        public static void NotEnoughSpace_IsError()
        {
            string src = Tmp.Create();
            string dst = Path.Combine(Tmp.Create(), "d");
            try { Check.True(Has(PathValidator.Validate(src, dst, 1000, 500), Severity.Error)); }
            finally { Tmp.Delete(src); }
        }

        [Test]
        public static void TightSpace_IsWarning()
        {
            string src = Tmp.Create();
            string dst = Path.Combine(Tmp.Create(), "d");
            try
            {
                List<CheckResult> r = PathValidator.Validate(src, dst, 1000, 1050);
                Check.True(Has(r, Severity.Warning));
                Check.True(!Has(r, Severity.Error));
            }
            finally { Tmp.Delete(src); }
        }

        [Test]
        public static void Healthy_NoErrors()
        {
            string src = Tmp.Create();
            string dst = Path.Combine(Tmp.Create(), "d");
            try
            {
                List<CheckResult> r = PathValidator.Validate(src, dst, 1000, long.MaxValue);
                Check.True(!Has(r, Severity.Error));
                Check.True(Has(r, Severity.Ok));
            }
            finally { Tmp.Delete(src); }
        }

        [Test]
        public static void IsInside_DetectsNestingCaseInsensitive()
        {
            Check.True(PathValidator.IsInside(@"F:\Fotos", @"F:\"));
            Check.True(PathValidator.IsInside(@"F:\Fotos\x", @"f:\fotos"));
            Check.True(PathValidator.IsInside(@"F:\Fotos", @"F:\Fotos\"));
            Check.True(!PathValidator.IsInside(@"F:\FotosViejas", @"F:\Fotos"));
            Check.True(!PathValidator.IsInside(@"E:\a", @"F:\"));
        }

        [Test]
        public static void BytesStillNeeded_SubtractsWhatDestinationAlreadyHas()
        {
            string dst = Tmp.Create();
            try
            {
                Tmp.WriteFile(Path.Combine(dst, "ya.bin"), 40);
                Check.Equal(60L, PathValidator.BytesStillNeeded(dst, 100, null));
                Check.Equal(0L, PathValidator.BytesStillNeeded(dst, 30, null));
                Check.Equal(100L, PathValidator.BytesStillNeeded(Path.Combine(dst, "nuevo"), 100, null));
            }
            finally { Tmp.Delete(dst); }
        }

        [Test]
        public static void GetFreeBytes_ForTempIsPositive_ForGarbageIsMinusOne()
        {
            Check.True(PathValidator.GetFreeBytes(Path.GetTempPath()) > 0);
            Check.Equal(-1L, PathValidator.GetFreeBytes("::no-es-una-ruta::"));
        }
    }
}
