using System;

namespace CopiaSegura.Core
{
    public enum Profile { Segura, Rapida }

    public sealed class CopyOptions
    {
        public const string UnlockPhrase = "BORRAR";

        public string Source;
        public string Destination;
        public string LogPath;
        public bool IncludeSubfolders;
        public bool PreserveDates;
        public bool SkipJunctions;
        public bool Restartable;
        public bool WriteLog;
        public bool DoubtfulDisk;
        public int Retries;
        public int WaitSeconds;
        public int Threads;

        public bool Mirror { get; private set; }
        public bool Move { get; private set; }
        public bool DangerousUnlocked { get; private set; }

        public static CopyOptions ForProfile(Profile p, string source, string destination)
        {
            CopyOptions o = new CopyOptions();
            o.Source = source;
            o.Destination = destination;
            o.LogPath = DefaultLogPath(destination);
            o.IncludeSubfolders = true;
            o.PreserveDates = true;
            o.SkipJunctions = true;
            o.WriteLog = true;
            if (p == Profile.Segura)
            {
                o.Restartable = true;
                o.Retries = 1;
                o.WaitSeconds = 2;
                o.Threads = 2;
            }
            else
            {
                o.Restartable = false;
                o.Retries = 0;
                o.WaitSeconds = 0;
                o.Threads = 8;
            }
            return o;
        }

        public static string DefaultLogPath(string destination)
        {
            if (CommandBuilder.IsDriveRoot(destination))
                return destination.Substring(0, 2) + "\\CopiaSegura_log.txt";
            return destination.TrimEnd('\\', '/') + "_log.txt";
        }

        public bool TryUnlockDangerous(string phrase)
        {
            if (phrase == UnlockPhrase) { DangerousUnlocked = true; return true; }
            return false;
        }

        public void SetMirror(bool value)
        {
            if (!DangerousUnlocked) throw new InvalidOperationException("Opcion peligrosa bloqueada");
            if (value) EnsureSourceNotInsideDestination();
            Mirror = value;
        }

        public void SetMove(bool value)
        {
            if (!DangerousUnlocked) throw new InvalidOperationException("Opcion peligrosa bloqueada");
            if (value) EnsureSourceNotInsideDestination();
            Move = value;
        }

        // Con espejo o mover, un origen dentro del destino acabaria borrando el propio origen.
        public void EnsureSourceNotInsideDestination()
        {
            if (PathValidator.IsInside(Source, Destination))
                throw new InvalidOperationException("Espejo y Mover no se permiten cuando el origen está dentro del destino: se borraría el propio origen.");
        }

        public void ApplyDoubtfulDisk()
        {
            Threads = 1;
            Retries = 1;
            WaitSeconds = 2;
            Restartable = true;
            DoubtfulDisk = true;
        }
    }
}
