using Autodesk.AutoCAD.Runtime;
using KnxTools.Check.Ui;

namespace KnxTools.Check
{
    public class CheckCommands
    {
        /// Окно проверки проекта. Повторный вызов — поднимает окно и обновляет.
        [CommandMethod(CheckRunner.CMD, CommandFlags.Modal)]
        public void CheckProject() { CheckWindow.ShowOrActivate(); }

        /// Справочник проверок и правила флага «НЕТ» — в командную строку.
        [CommandMethod("SDK_CHECK_HELP", CommandFlags.Modal)]
        public void CheckHelp() { CheckRunner.PrintCatalog(); }




        /// Отладка: состав групп EL_KNX* глазами проверки.
        [CommandMethod("SDK_CHECK_GROUPS", CommandFlags.Modal)]
        public void CheckGroups() { CheckDebug.Run(); }
    }
}