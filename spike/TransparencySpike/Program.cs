using System;
using System.Windows;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        string mode = "orb";
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--mode" && i + 1 < args.Length)
            {
                mode = args[i + 1].ToLowerInvariant();
                break;
            }
        }

        if (mode != "orb" && mode != "particles" && mode != "waveform")
        {
            Console.WriteLine($"Unknown mode '{mode}'. Use: orb, particles, waveform");
            return;
        }

        var app = new Application();
        var window = new SpikeWindow(mode);
        app.Run(window);
    }
}
