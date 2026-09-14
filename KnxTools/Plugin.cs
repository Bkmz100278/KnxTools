using Autodesk.AutoCAD.Runtime;
using System;
using System.Reflection;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using System.IO;



[assembly: ExtensionApplication(typeof(KnxTools.PluginApp))]

namespace KnxTools
{
    public class PluginApp : IExtensionApplication
    {
        // Лента отключена: пока отлаживаем загрузку, кнопку не строим.
        private const bool BuildRibbon = true;

        public void Initialize()
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nKnxTools загружен\n  файл: {0}\n  собран: {1:dd.MM.yyyy HH:mm:ss}\n  команда: KNXHELLO\n",
                asm.Location,
                System.IO.File.GetLastWriteTime(asm.Location));

            if (BuildRibbon) AcApp.Idle += OnIdle;
        }

        private void OnIdle(object sender, System.EventArgs e)
        {
            AcApp.Idle -= OnIdle;
            try { RibbonBuilder.Build(); }
            catch (System.Exception ex)
            {
                AcApp.DocumentManager.MdiActiveDocument?.Editor
                    .WriteMessage("\nKnxTools: лента не построена: {0}\n", ex.Message);
            }
        }

        public void Terminate()
        {
            AppDomain.CurrentDomain.AssemblyResolve -= ResolveFromPluginFolder;
        }

        private static Assembly ResolveFromPluginFolder(object sender, ResolveEventArgs args)
        {
            try
            {
                var requested = new AssemblyName(args.Name);
                string dir = Path.GetDirectoryName(typeof(PluginApp).Assembly.Location);
                string candidate = Path.Combine(dir ?? "", requested.Name + ".dll");
                if (!File.Exists(candidate)) return null;

                var found = AssemblyName.GetAssemblyName(candidate);
                if (found.Version != requested.Version) return null;   // просят не нашу версию — не вмешиваемся

                return Assembly.LoadFrom(candidate);
            }
            catch { return null; }
        }



    }
}
