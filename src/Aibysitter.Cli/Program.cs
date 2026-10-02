namespace Aibysitter.Cli;

internal static class Program
{
    private static int Main(string[] args) => CliApp.Run(args, Console.In, Console.Out, Console.Error);
}
