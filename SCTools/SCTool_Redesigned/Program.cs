using SCTool_Redesigned.Update;

namespace SCTool_Redesigned
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (UpdateProcessHelper.IsUpdateHelperInvocation(args))
                return UpdateProcessHelper.RunUpdateHelper(args);

            UpdateProcessHelper.HandleApplicationStartup(args);

            var app = new App();
            return app.Run();
        }
    }
}
