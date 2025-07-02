using System;
using Terminal.Gui;
using Terminal.Gui.Views;

class Program
{
    static void Main()
    {
        try
        {
            // Try to find what type is used for Application in v2 alpha
            var typeName = typeof(Terminal.Gui.Application).FullName;
            Console.WriteLine($"Application type: {typeName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            try
            {
                // Try to find what namespaces are available
                var types = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .Where(t => t.Namespace != null && t.Namespace.StartsWith("Terminal.Gui"))
                    .Select(t => t.FullName)
                    .ToList();
                    
                Console.WriteLine("Available Terminal.Gui types:");
                foreach (var type in types)
                {
                    Console.WriteLine($" - {type}");
                }
            }
            catch (Exception ex2)
            {
                Console.WriteLine($"Error listing types: {ex2.Message}");
            }
        }
    }
}
