using System.Windows;

namespace Te.Citadel.Extensions
{
    public static class ApplicationExtensions
    {
        public static void Shutdown(this Application app, ExitCodes code)
        {
            app.Shutdown((int)code);
        }
    }
}