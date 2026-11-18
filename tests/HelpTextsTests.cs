using System;
using CopiaSegura.Core;

namespace CopiaSegura.Tests
{
    public static class HelpTextsTests
    {
        [Test]
        public static void EveryOptionHasSpanishTextAndFlagWhereApplicable()
        {
            string[] keys = { "subfolders", "dates", "junctions", "restartable", "retries", "threads", "log", "mirror", "move", "doubtful" };
            foreach (string k in keys)
            {
                OptionHelp h = HelpTexts.Get(k);
                Check.True(h != null, "falta " + k);
                Check.True(h.Title.Length > 3, k + " titulo");
                Check.True(h.Explanation.Length > 20, k + " explicacion");
                if (k != "doubtful") Check.True(h.Flag.Length > 0, k + " parametro");
            }
        }

        [Test]
        public static void DangerousOptionsWarnAboutDeleting()
        {
            Check.True(HelpTexts.Get("mirror").Explanation.Contains("BORRA"));
            Check.True(HelpTexts.Get("move").Explanation.Contains("BORRA"));
            Check.Equal(10, HelpTexts.All.Count);
        }

        [Test]
        public static void Get_UnknownKey_ReturnsNull()
        {
            Check.True(HelpTexts.Get("nada") == null);
        }
    }
}
