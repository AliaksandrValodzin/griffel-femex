using System;

namespace griffel_femex.Cli
{
    /// <summary>
    /// The process. Four lines, deliberately: everything worth testing is in
    /// <see cref="Cli.Run"/>, which takes its writers as arguments.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            return Cli.Run(args, Console.Out, Console.Error);
        }
    }
}
