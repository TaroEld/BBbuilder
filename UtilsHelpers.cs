using BBbuilder;
using System;
using System.Collections.Generic;

internal static class UtilsHelpers
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
}