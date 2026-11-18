using System;
using System.Collections.Generic;
using System.IO;
using CopiaSegura.Core;

namespace CopiaSegura.Tests
{
    public static class RobocopyRunnerTests
    {
        [Test]
        public static void Run_CopiesTree_SourceUntouched()
        {
            string src = Tmp.Create();
            string outer = Tmp.Create();
            string dst = Path.Combine(outer, "destino");
            try
            {
                Tmp.WriteFile(Path.Combine(src, @"Ñandú & Co áé\documento con espacios.txt"), 1234);
                Tmp.WriteFile(Path.Combine(src, "grande.bin"), 5 * 1024 * 1024);
                Tmp.WriteFile(Path.Combine(src, @"sub\otro.dat"), 777);
                string before = Tmp.HashTree(src);
                long total = TreeScanner.Scan(src, true, null, null).Bytes;

                CopyOptions o = CopyOptions.ForProfile(Profile.Segura, src, dst);
                RobocopyRunner runner = new RobocopyRunner(null);
                double last = 0;
                runner.ProgressChanged += delegate(ProgressTracker t) { last = t.TotalPercent; };
                RunResult r = runner.Run(o, total);

                Check.True(r.Success, r.Describe());
                Check.Equal(1, r.ExitCode);
                Check.True(last >= 99.9, "progreso final " + last);
                Check.Equal(before, Tmp.HashTree(dst));
                Check.Equal(before, Tmp.HashTree(src));
                Check.Equal(3, Directory.GetFiles(src, "*", SearchOption.AllDirectories).Length);
            }
            finally { Tmp.Delete(src); Tmp.Delete(outer); }
        }

        [Test]
        public static void Run_MissingSource_IsFailureNotSuccess()
        {
            string outer = Tmp.Create();
            try
            {
                CopyOptions o = CopyOptions.ForProfile(Profile.Segura, Path.Combine(outer, "no_existe"), Path.Combine(outer, "dst"));
                RunResult r = new RobocopyRunner(null).Run(o, 100);
                Check.True(r.ExitCode >= 8, "exit " + r.ExitCode);
                Check.True(!r.Success);
                Check.True(r.Describe().Contains("FALLÓ"), r.Describe());
            }
            finally { Tmp.Delete(outer); }
        }

        [Test]
        public static void Run_EmptySource_NothingToCopy()
        {
            string src = Tmp.Create();
            string outer = Tmp.Create();
            try
            {
                CopyOptions o = CopyOptions.ForProfile(Profile.Segura, src, Path.Combine(outer, "dst"));
                RunResult r = new RobocopyRunner(null).Run(o, 0);
                Check.True(r.Success, r.Describe());
            }
            finally { Tmp.Delete(src); Tmp.Delete(outer); }
        }

        [Test]
        public static void Stop_BeforeRun_DoesNotThrow()
        {
            RobocopyRunner r = new RobocopyRunner(null);
            r.Stop();
            r.Stop();
        }

        [Test]
        public static void IsClean_OnlyForCodes0And1()
        {
            RunResult r = new RunResult();
            r.ExitCode = 0; Check.True(r.IsClean);
            r.ExitCode = 1; Check.True(r.IsClean);
            r.ExitCode = 2; Check.True(!r.IsClean);
            r.ExitCode = 5; Check.True(!r.IsClean && r.Success);
            r.ExitCode = 8; Check.True(!r.IsClean);
            r.ExitCode = 1; r.StoppedByUser = true; Check.True(!r.IsClean);
        }

        [Test]
        public static void AddErrors_AccumulatesAcrossRunsWithoutDuplicates()
        {
            RunResult acc = new RunResult();
            List<string> first = new List<string>();
            first.Add("2026/11/18 10:00:00 ERROR 5 (0x00000005) Copying File E:\\a.tmp");
            acc.AddErrors(first);
            List<string> second = new List<string>();
            second.Add("2026/11/18 11:00:00 ERROR 5 (0x00000005) Copying File E:\\a.tmp");
            second.Add("2026/11/18 11:00:01 ERROR 32 (0x00000020) Copying File E:\\b.tmp");
            acc.AddErrors(second);
            Check.Equal(2, acc.ErrorLines.Count);
            Check.Equal(2, acc.ErrorCount);
        }

        [Test]
        public static void Run_AppendsToLog_NotOverwrites()
        {
            string src = Tmp.Create();
            string outer = Tmp.Create();
            try
            {
                Tmp.WriteFile(Path.Combine(src, "ñandú.bin"), 500);
                CopyOptions o = CopyOptions.ForProfile(Profile.Segura, src, Path.Combine(outer, "dst"));
                new RobocopyRunner(null).Run(o, 500);
                long first = new FileInfo(o.LogPath).Length;
                Tmp.WriteFile(Path.Combine(src, "otro.bin"), 600);
                new RobocopyRunner(null).Run(o, 1100);
                Check.True(new FileInfo(o.LogPath).Length > first, "el registro debe crecer, no reiniciarse");
                Check.True(File.ReadAllText(o.LogPath).Contains("ñandú.bin"), "el registro Unicode conserva la ñ");
            }
            finally { Tmp.Delete(src); Tmp.Delete(outer); }
        }

        [Test]
        public static void NegativeExitCode_IsFailure()
        {
            RunResult r = new RunResult();
            r.ExitCode = -1;
            Check.True(!r.Success);
            Check.True(r.Describe().Contains("FALLÓ"), r.Describe());
        }

        [Test]
        public static void Stop_BeforeRun_RunCopiesNothingAndReportsStopped()
        {
            string src = Tmp.Create();
            string outer = Tmp.Create();
            try
            {
                Tmp.WriteFile(Path.Combine(src, "a.bin"), 1000);
                string dst = Path.Combine(outer, "dst");
                RobocopyRunner runner = new RobocopyRunner(null);
                runner.Stop();
                RunResult r = runner.Run(CopyOptions.ForProfile(Profile.Segura, src, dst), 1000);
                Check.True(r.StoppedByUser, "la orden de parar no debe perderse");
                Check.True(!r.Success);
            }
            finally { Tmp.Delete(src); Tmp.Delete(outer); }
        }

        [Test]
        public static void ClearHiddenSystem_RemovesAttributesFromFolder()
        {
            string dir = Tmp.Create();
            try
            {
                File.SetAttributes(dir, File.GetAttributes(dir) | FileAttributes.Hidden | FileAttributes.System);
                RobocopyRunner.ClearHiddenSystem(dir);
                FileAttributes a = File.GetAttributes(dir);
                Check.True((a & FileAttributes.Hidden) == 0 && (a & FileAttributes.System) == 0, a.ToString());
                RobocopyRunner.ClearHiddenSystem(Path.Combine(dir, "no_existe"));
            }
            finally { Tmp.Delete(dir); }
        }

        [Test]
        public static void Describe_MapsExitCodes()
        {
            RunResult r = new RunResult();
            r.ExitCode = 0; Check.True(r.Describe().Contains("Nada que copiar"));
            r.ExitCode = 1; Check.True(r.Describe().Contains("Copia completada"));
            r.ExitCode = 3; Check.True(r.Describe().Contains("extra"));
            r.ExitCode = 5; Check.True(r.Describe().Contains("diferencias"));
            r.ExitCode = 9; Check.True(r.Describe().Contains("FALLÓ"));
            r.ExitCode = 1; r.StoppedByUser = true;
            Check.True(r.Describe().Contains("Detenida") && !r.Success);
        }
    }
}
