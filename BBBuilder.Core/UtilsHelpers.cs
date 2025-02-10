using BBBuilder;
using System;
using System.Collections.Generic;

public static class UtilsHelpers
{

    public static void PrintHelp(Dictionary<string, Command> _commands)
    {
        foreach (KeyValuePair<string, Command> entry in _commands)
        {
            Console.WriteLine("\n------------------------------------------------------------\n");
            entry.Value.PrintHelp();
        }
        return;
    }
    public static void PrintShortHelp(Dictionary<string, Command> _commands)
    {
        Console.WriteLine("Use -help for full command list.");
        foreach (KeyValuePair<string, Command> entry in _commands)
        {
            Console.WriteLine("\n------------------------------------------------------------\n");
            entry.Value.PrintShortHelp();
        }
        return;
    }
}