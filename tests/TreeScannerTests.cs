using System;
using System.IO;
using CopiaSegura.Core;

namespace CopiaSegura.Tests
{
    public static class TreeScannerTests
    {
        [Test]
        public static void CountsFilesAndBytes()
        {
            string root = Tmp.Create();
            try
            {
                Tmp.WriteFile(Path.Combine(root, "a.bin"), 10);
                Tmp.WriteFile(Path.Combine(root, @"s1\b.bin"), 20);
                Tmp.WriteFile(Path.Combine(root, @"s1\s2\c.bin"), 30);
                ScanResult r = TreeScanner.Scan(root, true, null, null);
                Check.Equal(3L, r.Files);
                Check.Equal(60L, r.Bytes);
                Check.Equal(0, r.Unreadable.Count);
            }
            finally { Tmp.Delete(root); }
        }

        [Test]
        public static void UnreadableFolder_ReportedNotThrown()
        {
            string root = Tmp.Create();
            string locked = Path.Combine(root, "cerrada");
            try
            {
                Tmp.WriteFile(Path.Combine(root, "ok.bin"), 5);
                Tmp.WriteFile(Path.Combine(locked, "x.bin"), 7);
                Tmp.Cmd("icacls \"" + locked + "\" /deny \"" + Environment.UserName + ":(R)\"");
                ScanResult r = TreeScanner.Scan(root, true, null, null);
                Check.True(r.Files >= 1);
                Check.True(r.Unreadable.Count >= 1, "la carpeta cerrada debe constar como ilegible");
            }
            finally
            {
                Tmp.Cmd("icacls \"" + locked + "\" /remove:d \"" + Environment.UserName + "\"");
                Tmp.Delete(root);
            }
        }

        [Test]
        public static void LongPath_ReportedNotThrown()
        {
            string root = Tmp.Create();
            try
            {
                Tmp.WriteFile(Path.Combine(root, "corto.bin"), 4);
                string deep = root;
                for (int i = 0; i < 12; i++) deep = deep + "\\" + new string('x', 30);
                Tmp.Cmd("mkdir \"\\\\?\\" + deep + "\"");
                Tmp.Cmd("echo hola> \"\\\\?\\" + deep + "\\f.txt\"");
                ScanResult r = TreeScanner.Scan(root, true, null, null);
                Check.True(r.Files >= 1, "el archivo corto se cuenta");
            }
            finally { Tmp.Delete(root); }
        }

        [Test]
        public static void SkipJunctions_ExcludesLinkedTree()
        {
            string root = Tmp.Create();
            string other = Tmp.Create();
            try
            {
                Tmp.WriteFile(Path.Combine(root, "real.bin"), 10);
                Tmp.WriteFile(Path.Combine(other, "enlazado.bin"), 100);
                Tmp.Cmd("mklink /J \"" + Path.Combine(root, "enlace") + "\" \"" + other + "\"");
                Check.Equal(10L, TreeScanner.Scan(root, true, null, null).Bytes);
                Check.Equal(110L, TreeScanner.Scan(root, false, null, null).Bytes);
            }
            finally
            {
                Tmp.Cmd("rmdir \"" + Path.Combine(root, "enlace") + "\"");
                Tmp.Delete(root);
                Tmp.Delete(other);
            }
        }

        [Test]
        public static void Cancel_StopsEarly()
        {
            string root = Tmp.Create();
            try
            {
                Tmp.WriteFile(Path.Combine(root, "a.bin"), 10);
                Tmp.WriteFile(Path.Combine(root, @"s\b.bin"), 10);
                ScanResult r = TreeScanner.Scan(root, true, delegate { return true; }, null);
                Check.Equal(0L, r.Files);
            }
            finally { Tmp.Delete(root); }
        }
    }
}
