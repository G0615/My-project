using System;
using CelebrationDemo;

public static class Runner
{
    public static int Main()
    {
        Console.WriteLine("Core smoke checks: " + CoreSmokeTests.RunAll());
        return 0;
    }
}
