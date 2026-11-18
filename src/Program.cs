using System;
using System.Windows.Forms;
using CopiaSegura.UI;

namespace CopiaSegura
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--smoke")
            {
                string error = SmokeTest.Run();
                if (error != null)
                {
                    Console.Error.WriteLine("SMOKE FALLO: " + error);
                    return 1;
                }
                return 0;
            }

            if (args.Length > 1 && args[0] == "--shots")
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                string err = SmokeTest.Shots(args[1]);
                if (err != null) { Console.Error.WriteLine(err); return 1; }
                return 0;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }
    }
}
