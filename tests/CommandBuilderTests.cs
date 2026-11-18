using System;
using CopiaSegura.Core;

namespace CopiaSegura.Tests
{
    public static class CommandBuilderTests
    {
        [Test]
        public static void Segura_ExactCommand()
        {
            CopyOptions o = CopyOptions.ForProfile(Profile.Segura, @"E:\Usuarios\Alejandro", @"D:\Respaldo_Alejandro");
            Check.Equal(
                "\"E:\\Usuarios\\Alejandro\" \"D:\\Respaldo_Alejandro\" /E /Z /COPY:DAT /DCOPY:DAT /XJ /R:1 /W:2 /MT:2 /TEE /BYTES /NDL /UNILOG+:\"D:\\Respaldo_Alejandro_log.txt\"",
                CommandBuilder.BuildArguments(o));
        }

        [Test]
        public static void Rapida_NoRestartable_8Threads_R0()
        {
            CopyOptions o = CopyOptions.ForProfile(Profile.Rapida, @"E:\a", @"D:\b");
            string s = CommandBuilder.BuildArguments(o);
            Check.True(s.Contains("/R:0 /W:0 /MT:8"), s);
            Check.True(!s.Contains("/Z"), s);
        }

        [Test]
        public static void DoubtfulDisk_1Thread_1Retry()
        {
            CopyOptions o = CopyOptions.ForProfile(Profile.Rapida, @"E:\a", @"D:\b");
            o.ApplyDoubtfulDisk();
            string s = CommandBuilder.BuildArguments(o);
            Check.True(s.Contains("/R:1") && s.Contains("/MT:1") && s.Contains("/Z"), s);
            Check.True(o.DoubtfulDisk);
        }

        [Test]
        public static void DriveRootSource_QuotedWithDot_AndExcludesSystemDirs()
        {
            CopyOptions o = CopyOptions.ForProfile(Profile.Segura, @"E:\", @"D:\Copia");
            string s = CommandBuilder.BuildArguments(o);
            Check.True(s.StartsWith("\"E:\\.\" "), s);
            Check.True(s.Contains("/XJ /XD \"System Volume Information\" \"$RECYCLE.BIN\" /R:1"), s);
            Check.Equal("\"D:\\a b\"", CommandBuilder.Quote(@"D:\a b"));
            Check.Throws<ArgumentException>(delegate { CommandBuilder.Quote("D:\\a\"b"); });
            Check.True(CommandBuilder.IsDriveRoot(@"E:\"));
            Check.True(!CommandBuilder.IsDriveRoot(@"E:\x"));
        }

        [Test]
        public static void Dangerous_LockedByDefault()
        {
            CopyOptions o = CopyOptions.ForProfile(Profile.Segura, @"E:\a", @"D:\b");
            Check.True(!o.Mirror && !o.Move && !o.DangerousUnlocked);
            Check.Throws<InvalidOperationException>(delegate { o.SetMirror(true); });
            Check.True(!o.TryUnlockDangerous("borrar"));
            Check.True(o.TryUnlockDangerous("BORRAR"));
            o.SetMirror(true);
            o.SetMove(true);
            string s = CommandBuilder.BuildArguments(o);
            Check.True(s.Contains("/MIR") && s.Contains("/MOVE"), s);
            Check.True(s.IndexOf("/MOVE") < s.IndexOf("/UNILOG"), s);
            Check.True(!CopyOptions.ForProfile(Profile.Rapida, @"E:\a", @"D:\b").Mirror);
        }

        [Test]
        public static void MirrorOrMove_BlockedWhenSourceInsideDestination()
        {
            CopyOptions o = CopyOptions.ForProfile(Profile.Segura, @"F:\Fotos", @"F:\");
            Check.True(o.TryUnlockDangerous("BORRAR"));
            Check.Throws<InvalidOperationException>(delegate { o.SetMirror(true); });
            Check.Throws<InvalidOperationException>(delegate { o.SetMove(true); });

            CopyOptions ok = CopyOptions.ForProfile(Profile.Segura, @"E:\a", @"D:\b");
            ok.TryUnlockDangerous("BORRAR");
            ok.SetMirror(true);
            ok.Destination = @"E:\";
            Check.Throws<InvalidOperationException>(delegate { CommandBuilder.BuildArguments(ok); });
        }

        [Test]
        public static void Paths_AreNormalized_SoSlashPathCannotBecomeASwitch()
        {
            CopyOptions o = CopyOptions.ForProfile(Profile.Segura, @"E:\a\..\b", "/MIR");
            string s = CommandBuilder.BuildArguments(o);
            Check.True(!s.Contains("\"/MIR\""), "la ruta /MIR no debe llegar tal cual: " + s);
            Check.True(s.StartsWith("\"E:\\b\" "), "ruta del origen normalizada: " + s);
        }

        [Test]
        public static void LogPath_RootDestination()
        {
            CopyOptions o = CopyOptions.ForProfile(Profile.Segura, @"E:\a", @"D:\");
            Check.Equal(@"D:\CopiaSegura_log.txt", o.LogPath);
        }
    }
}
