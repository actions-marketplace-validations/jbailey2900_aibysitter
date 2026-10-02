using System.Text;

namespace Aibysitter.Rules.Browser;

/// <summary>Usage: Aibysitter.Rules.Browser &lt;output.mjs&gt;. Writes the file only when its content changes.</summary>
internal static class Exporter
{
    public static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: Aibysitter.Rules.Browser <output.mjs>");
            return 2;
        }

        try
        {
            Write(Path.GetFullPath(args[0]), PatternExport.Build());
            return 0;
        }
        catch (NotSupportedException ex)
        {
            Console.Error.WriteLine($"error AIB001: {ex.Message}");
            return 1;
        }
    }

    private static void Write(string output, string js)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        if (!File.Exists(output) || File.ReadAllText(output) != js)
        {
            File.WriteAllText(output, js, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }
}
