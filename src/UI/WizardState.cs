using System;
using System.Collections.Generic;
using CopiaSegura.Core;

namespace CopiaSegura.UI
{
    public interface IWizardPage
    {
        string StepTitle { get; }
        bool CanAdvance(out string reason);
        void OnEnter(WizardState s);
        void OnLeave(WizardState s);
    }

    public sealed class WizardState
    {
        public string SourcePath = "";
        public string DestPath = "";
        public CopyOptions Options;
        public Profile Profile = Profile.Segura;
        public ScanResult Scan;
        public List<CheckResult> Checks;
        public RunResult Run;
        public VerifyResult Verify;
        public VerifyMode VerifyMode = VerifyMode.Quick;
        public Settings Settings = new Settings();
        public string DataDir;
        public DateTime Start;
        public DateTime End;
    }
}
