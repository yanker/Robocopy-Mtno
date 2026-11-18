using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CopiaSegura.Core
{
    public enum LineKind { Other, FileStart, Percent, Error }

    public sealed class ParsedLine
    {
        public LineKind Kind;
        public long FileSize;
        public string Path;
        public double Percent;
        public string Text;
    }

    // Interpreta la salida de robocopy sin depender del idioma de Windows:
    // una linea de archivo es "<etiqueta><tabs><tamano en bytes><tab><ruta>" (con /BYTES).
    public static class ProgressParser
    {
        static readonly Regex FileRx = new Regex(@"^\s*(?<tag>[^\t\d][^\t]*?)\s*\t+\s*(?<size>\d+)\t(?<path>.+)$", RegexOptions.Compiled);
        static readonly Regex PercentRx = new Regex(@"^\s*(?<n>\d{1,3}(?:[.,]\d+)?)%\s*$", RegexOptions.Compiled);
        static readonly Regex ErrorRx = new Regex(@"ERROR \d+ \(0x", RegexOptions.Compiled);

        static readonly Regex TimestampRx = new Regex(@"^\s*\d{4}/\d{2}/\d{2} \d{2}:\d{2}:\d{2}\s+", RegexOptions.Compiled);

        // Identifica un error sin la hora, para no contar dos veces el mismo error reintentado.
        public static string ErrorKey(string line)
        {
            return TimestampRx.Replace(line ?? "", "").Trim();
        }

        public static ParsedLine Parse(string line)
        {
            ParsedLine p = new ParsedLine();
            p.Kind = LineKind.Other;
            p.Text = line ?? "";
            if (string.IsNullOrEmpty(line)) return p;

            Match m = PercentRx.Match(line);
            if (m.Success)
            {
                p.Kind = LineKind.Percent;
                p.Percent = double.Parse(m.Groups["n"].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
                return p;
            }
            if (ErrorRx.IsMatch(line))
            {
                p.Kind = LineKind.Error;
                return p;
            }
            m = FileRx.Match(line);
            if (m.Success)
            {
                long size;
                if (long.TryParse(m.Groups["size"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out size))
                {
                    p.Kind = LineKind.FileStart;
                    p.FileSize = size;
                    p.Path = m.Groups["path"].Value.TrimEnd();
                }
            }
            return p;
        }
    }

    public sealed class ProgressTracker
    {
        readonly long totalBytes;
        long completed;
        long curSize;
        double curPct;
        bool hasCurrent;
        readonly System.Collections.Generic.HashSet<string> seenErrors = new System.Collections.Generic.HashSet<string>();

        public ProgressTracker(long totalBytes)
        {
            this.totalBytes = totalBytes;
        }

        public string CurrentFile { get; private set; }
        public int ErrorCount { get; private set; }
        public long CurrentFileSize { get { return curSize; } }
        public double CurrentPercent { get { return curPct; } }
        public bool NothingToCopy { get { return totalBytes == 0; } }

        public long ProcessedBytes
        {
            get { return completed + (long)(curSize * (curPct / 100.0)); }
        }

        public double TotalPercent
        {
            get
            {
                if (totalBytes <= 0) return 0;
                double v = ProcessedBytes * 100.0 / totalBytes;
                return v > 100 ? 100 : v;
            }
        }

        public void Feed(ParsedLine l)
        {
            if (l.Kind == LineKind.FileStart)
            {
                if (hasCurrent) completed += curSize;
                hasCurrent = true;
                curSize = l.FileSize;
                curPct = 0;
                CurrentFile = l.Path;
            }
            else if (l.Kind == LineKind.Percent)
            {
                curPct = l.Percent > 100 ? 100 : l.Percent;
            }
            else if (l.Kind == LineKind.Error)
            {
                if (seenErrors.Add(ProgressParser.ErrorKey(l.Text))) ErrorCount++;
            }
        }

        public void Finish()
        {
            if (hasCurrent)
            {
                completed += curSize;
                curSize = 0;
                curPct = 100;
                hasCurrent = false;
            }
        }
    }
}
