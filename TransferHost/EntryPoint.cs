using System.Reflection;

namespace VideoShelf.TransferHost;

internal static class EntryPoint
{
    [STAThread]
    static void Main(string[] args)
    {
        string command = args.FirstOrDefault()?.ToLowerInvariant() ?? "";
        if (command == "files" || command == "download" || command == "stream")
        {
            try
            {
                var values = Arguments.Parse(args.Skip(1).ToArray());
                string source = values.Required("source");
                string? page = values.Get("page");
                string resolved = TransferSourceResolver.ResolveAsync(source, page, CancellationToken.None).GetAwaiter().GetResult();
                if (string.IsNullOrWhiteSpace(resolved)) throw new InvalidOperationException("This result does not contain usable torrent metadata.");
                string title = values.Get("title") ?? "Torrent";

                if (command == "files")
                {
                    ApplicationConfiguration.Initialize();
                    Application.Run(new FileListForm(resolved, title));
                    return;
                }

                if (command == "stream")
                {
                    ApplicationConfiguration.Initialize();
                    Application.Run(new OptimizedStreamForm(resolved, title));
                    return;
                }

                args = ReplaceSource(args, resolved);
            }
            catch (Exception ex)
            {
                ApplicationConfiguration.Initialize();
                MessageBox.Show(ex.Message, "VideoShelf", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.ExitCode = 1;
                return;
            }
        }

        MethodInfo? original = typeof(Program).GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic);
        if (original == null) throw new MissingMethodException("VideoShelf transfer runtime entry point was not found.");
        try
        {
            original.Invoke(null, new object[] { args });
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }

    static string[] ReplaceSource(string[] args, string source)
    {
        string[] rewritten = (string[])args.Clone();
        for (int i = 0; i < rewritten.Length - 1; i++)
        {
            if (!rewritten[i].Equals("--source", StringComparison.OrdinalIgnoreCase)) continue;
            rewritten[i + 1] = source;
            return rewritten;
        }
        return rewritten;
    }
}
