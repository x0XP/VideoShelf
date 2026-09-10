using System.Reflection;

namespace VideoShelf.TransferHost;

internal static class EntryPoint
{
    [STAThread]
    static void Main(string[] args)
    {
        string command = args.FirstOrDefault()?.ToLowerInvariant() ?? "";
        if (command == "files")
        {
            ApplicationConfiguration.Initialize();
            try
            {
                var values = Arguments.Parse(args.Skip(1).ToArray());
                string source = values.Required("source");
                string title = values.Get("title") ?? "Torrent";
                Application.Run(new FileListForm(source, title));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "VideoShelf", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.ExitCode = 1;
            }
            return;
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
}
