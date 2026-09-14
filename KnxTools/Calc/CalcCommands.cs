using System;
using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using KnxTools.Calc.Diag;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace KnxTools.Calc
{
    public class CalcCommands
    {
        [CommandMethod("SDK_CALC_1_COS", CommandFlags.Modal)] public void C1() { CalcRunner.Run("SDK_CALC_1_COS", 1); }
        [CommandMethod("SDK_CALC_2_NUMBERING", CommandFlags.Modal)] public void C2() { CalcRunner.Run("SDK_CALC_2_NUMBERING", 2); }
        [CommandMethod("SDK_CALC_3_LOADS", CommandFlags.Modal)] public void C3() { CalcRunner.Run("SDK_CALC_3_LOADS", 3); }
        [CommandMethod("SDK_CALC_4_PANEL", CommandFlags.Modal)] public void C4() { CalcRunner.Run("SDK_CALC_4_PANEL", 4); }
        [CommandMethod("SDK_CALC_5_CHECK", CommandFlags.Modal)] public void C5() { CalcRunner.Run("SDK_CALC_5_CHECK", 5); }
        [CommandMethod("SDK_CALC_6_NAME", CommandFlags.Modal)] public void C6() { CalcRunner.Run("SDK_CALC_6_NAME", 6); }
        [CommandMethod("SDK_CALC_7_PROPAGATE", CommandFlags.Modal)] public void C7() { CalcRunner.Run("SDK_CALC_7_PROPAGATE", 7); }
        [CommandMethod("SDK_CALC_8_CHANNELS", CommandFlags.Modal)] public void C8() { CalcRunner.Run("SDK_CALC_8_CHANNELS", 8); }

        /// Полный расчёт. Порядок: нумерация каналов (8) до проверки (5); имя щита (6) до раздачи (7).
        [CommandMethod("SDK_CALC_PANEL", CommandFlags.Modal)]
        public void All() { CalcRunner.Run("SDK_CALC_PANEL", 1, 2, 3, 4, 8, 5, 6, 7); }

        /// Переход к объекту по handle из отчёта.
       
        
    }
}
