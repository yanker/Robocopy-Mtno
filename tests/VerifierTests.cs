using System;
using System.Collections.Generic;
using System.IO;
using CopiaSegura.Core;

namespace CopiaSegura.Tests
{
    public static class VerifierTests
    {
        static void Make(out string src, out string dst)
        {
            src = Tmp.Create();
            dst = Path.Combine(Tmp.Create(), "copia");
            Tmp.WriteFile(Path.Combine(src, "a.bin"), 1000);
            Tmp.WriteFile(Path.Combine(src, @"Ñandú & Co áé\con espacios.txt"), 2000);
            Tmp.WriteFile(Path.Combine(src, @"sub\b.bin"), 3000);
            Tmp.CopyTree(src, dst);
        }

        [Test]
        public static void Quick_IdenticalTree_Ok()
        {
            string src, dst; Make(out src, out dst);
            try
            {
                VerifyResult r = Verifier.Verify(src, dst, VerifyMode.Quick, true, null, null, null);
                Check.True(r.Ok);
                Check.Equal(3L, r.FilesChecked);
            }
            finally { Tmp.Delete(src); Tmp.Delete(Path.GetDirectoryName(dst)); }
        }

        [Test]
        public static void Quick_MissingFile_Reported()
        {
            string src, dst; Make(out src, out dst);
            try
            {
                File.Delete(Path.Combine(dst, "a.bin"));
                VerifyResult r = Verifier.Verify(src, dst, VerifyMode.Quick, true, null, null, null);
                Check.True(!r.Ok);
                Check.Equal(1, r.Missing.Count);
                Check.Equal("a.bin", r.Missing[0]);
            }
            finally { Tmp.Delete(src); Tmp.Delete(Path.GetDirectoryName(dst)); }
        }

        [Test]
        public static void Quick_TruncatedFile_SizeMismatch()
        {
            string src, dst; Make(out src, out dst);
            try
            {
                File.WriteAllBytes(Path.Combine(dst, @"sub\b.bin"), new byte[10]);
                VerifyResult r = Verifier.Verify(src, dst, VerifyMode.Quick, true, null, null, null);
                Check.Equal(1, r.SizeMismatch.Count);
            }
            finally { Tmp.Delete(src); Tmp.Delete(Path.GetDirectoryName(dst)); }
        }

        [Test]
        public static void Sha256_FlippedByteSameSize_DetectedByShaNotByQuick()
        {
            string src, dst; Make(out src, out dst);
            try
            {
                string f = Path.Combine(dst, "a.bin");
                byte[] b = File.ReadAllBytes(f);
                b[500] = (byte)(b[500] ^ 0xFF);
                File.WriteAllBytes(f, b);
                Check.True(Verifier.Verify(src, dst, VerifyMode.Quick, true, null, null, null).Ok, "Quick no ve el cambio");
                VerifyResult r = Verifier.Verify(src, dst, VerifyMode.Sha256, true, null, null, null);
                Check.True(!r.Ok);
                Check.Equal(1, r.HashMismatch.Count);
            }
            finally { Tmp.Delete(src); Tmp.Delete(Path.GetDirectoryName(dst)); }
        }

        [Test]
        public static void Sha256_UnicodeAndSpacesPaths_Ok()
        {
            string src, dst; Make(out src, out dst);
            try
            {
                VerifyResult r = Verifier.Verify(src, dst, VerifyMode.Sha256, true, null, null, null);
                Check.True(r.Ok, "missing=" + r.Missing.Count + " hash=" + r.HashMismatch.Count);
            }
            finally { Tmp.Delete(src); Tmp.Delete(Path.GetDirectoryName(dst)); }
        }

        [Test]
        public static void SkipList_ReportedAsNotVerified_NeverOk()
        {
            string src, dst; Make(out src, out dst);
            try
            {
                File.Delete(Path.Combine(dst, "a.bin"));
                List<string> skip = new List<string>();
                skip.Add("A.BIN");
                VerifyResult r = Verifier.Verify(src, dst, VerifyMode.Quick, true, skip, null, null);
                Check.Equal(1, r.Skipped.Count);
                Check.Equal(0, r.Missing.Count);
                Check.True(!r.Ok, "un archivo sin verificar no puede dar Ok");
            }
            finally { Tmp.Delete(src); Tmp.Delete(Path.GetDirectoryName(dst)); }
        }

        [Test]
        public static void Quick_DifferentTimestampSameSize_Reported()
        {
            string src, dst; Make(out src, out dst);
            try
            {
                File.SetLastWriteTimeUtc(Path.Combine(dst, "a.bin"), new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                Check.Equal(1, Verifier.Verify(src, dst, VerifyMode.Quick, true, null, null, null).SizeMismatch.Count);
                Check.True(Verifier.Verify(src, dst, VerifyMode.Quick, true, null, null, null, false).Ok, "sin comparar fechas es Ok");
            }
            finally { Tmp.Delete(src); Tmp.Delete(Path.GetDirectoryName(dst)); }
        }

        [Test]
        public static void RelativePathsFromErrors_ExtractsPathsUnderSource()
        {
            List<string> lines = new List<string>();
            lines.Add(@"2026/11/18 10:00:00 ERROR 5 (0x00000005) Copying File E:\src\sub\x.tmp");
            lines.Add(@"2026/11/18 10:00:01 ERROR 32 (0x00000020) Copying File e:\SRC\y.bin");
            lines.Add("texto sin ruta del origen");
            List<string> rel = Verifier.RelativePathsFromErrors(lines, @"E:\src\");
            Check.Equal(2, rel.Count);
            Check.Equal(@"sub\x.tmp", rel[0]);
            Check.Equal("y.bin", rel[1]);
        }

        [Test]
        public static void Cancel_SetsCancelled()
        {
            string src, dst; Make(out src, out dst);
            try
            {
                VerifyResult r = Verifier.Verify(src, dst, VerifyMode.Sha256, true, null, delegate { return true; }, null);
                Check.True(r.Cancelled);
                Check.True(!r.Ok);
            }
            finally { Tmp.Delete(src); Tmp.Delete(Path.GetDirectoryName(dst)); }
        }
    }
}
