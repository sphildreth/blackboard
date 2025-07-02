using System;
using Terminal.Gui;

class Program
{
    static void Main()
    {
        Console.WriteLine("Testing Terminal.Gui v2 alpha...");
        // These lines will show us what's available
        Application.Init();
        var win = new Window();
        Application.Run(win);
        Application.Shutdown();
    }
}
