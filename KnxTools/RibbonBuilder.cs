using System;
using System.Reflection;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Autodesk.Windows;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace KnxTools
{
    internal static class RibbonBuilder
    {
        private const string TabId = "KNXTOOLS_TAB";

        public static void Build()
        {
            var ribbon = ComponentManager.Ribbon;
            if (ribbon == null) return;
            if (ribbon.FindTab(TabId) != null) return;   // уже построена — не дублируем

            var tab = new RibbonTab { Title = "KNX", Id = TabId };

            var panelSource = new RibbonPanelSource { Title = "Проект" };
            var panel = new RibbonPanel { Source = panelSource };
            tab.Panels.Add(panel);

            var large = Icons.Load("IconMain32.png");
            var small = Icons.Load("IconMain16.png") ?? large;

            var helloButton = new RibbonButton
            {
                Text = "EL_KNX",
                ShowText = true,
                ShowImage = large != null,
                Image = small,
                LargeImage = large,
                Size = RibbonItemSize.Large,
                Orientation = System.Windows.Controls.Orientation.Vertical,
                ToolTip = "Система Электрика и KNX",
                CommandParameter = "SDK_CHECK_PROJECT",
                CommandHandler = new CommandRelay()
            };
            panelSource.Items.Add(helloButton);

            //panelSource.Items.Add(new RibbonButton
            //{
            //    Text = "Считать\nв Excel",
            //    ShowText = true,
            //    ShowImage = false,
            //    Size = RibbonItemSize.Large,
            //    Orientation = System.Windows.Controls.Orientation.Vertical,
            //    ToolTip = "Считать базу с чертежа и выгрузить в Excel",
            //    CommandParameter = "KNXREAD ",
            //    CommandHandler = new CommandRelay()
            //});


            ribbon.Tabs.Add(tab);
        }

        // В Revit кнопка ссылается на класс IExternalCommand.
        // В AutoCAD кнопка — WPF-элемент, и по нажатию она вызывает ICommand.
        // Мы просто отправляем текст команды в командную строку.
        private class CommandRelay : ICommand
        {
            public event EventHandler CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object parameter) => true;

            public void Execute(object parameter)
            {
                // AutoCAD передаёт сюда саму кнопку; на всякий случай принимаем и строку.
                string cmd = parameter as string
                          ?? (parameter as RibbonButton)?.CommandParameter as string;
                if (string.IsNullOrWhiteSpace(cmd)) return;

                AcApp.DocumentManager.MdiActiveDocument?
                     .SendStringToExecute(cmd +" " , true, false, true);
            }
        }
    }

    internal static class Icons
    {
        public static BitmapImage Load(string fileName)
        {
            var asm = Assembly.GetExecutingAssembly();
            string resName = "KnxTools.Resources." + fileName;

            using (var stream = asm.GetManifestResourceStream(resName))
            {
                if (stream == null) return null;      // имя не совпало — см. ниже, как проверить

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = stream;
                bmp.CacheOption = BitmapCacheOption.OnLoad;   // прочитать целиком сейчас, поток можно закрыть
                bmp.EndInit();
                bmp.Freeze();                                 // безопасно для использования в UI-потоке ленты
                return bmp;
            }
        }
    }
}
