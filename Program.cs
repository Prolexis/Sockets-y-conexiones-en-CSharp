namespace SERVIDORES_SOCKETS
{
    internal static class Program
    {
        /// <summary>
        /// Punto de entrada de la aplicación.
        /// Abre MainForm (pantalla de selección Servidor / Cliente).
        /// </summary>
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
