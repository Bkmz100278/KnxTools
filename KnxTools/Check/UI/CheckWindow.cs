using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace KnxTools.Check.Ui
{
    /// Немодальное окно проверки проекта. Одно на сессию: повторный вызов команды поднимает его и обновляет.
    public sealed class CheckWindow : Window
    {
        // ---------------------------------------------------------------- команды AutoCAD, которые окно умеет запускать
        public const string CmdCalc = "SDK_CALC_PANEL";   // расчёт щитов; после него таблица обновляется сама
        public const string CmdJson = "KNXJSON";          // выгрузка JSON
        public const string CmdSpec = "SDK_SPEC";                 // резерв: спецификация оборудования
        public const string CmdCable = "";                 // резерв: кабельный журнал

        // ---------------------------------------------------------------- единственный экземпляр
        static CheckWindow _instance;
        static Rect? _savedBounds;                       // размер и положение — помним до перезапуска AutoCAD

        public static void ShowOrActivate()
        {
            if (_instance != null)
            {
                if (_instance.WindowState == WindowState.Minimized) _instance.WindowState = WindowState.Normal;
                _instance.Activate();
                _instance.Refresh();
                return;
            }
            _instance = new CheckWindow();
            AcApp.ShowModelessWindow(_instance);         // владелец — главное окно AutoCAD, окно не модальное
            _instance.Refresh();
        }

        // ---------------------------------------------------------------- состояние
        List<CheckRow> _rows = new List<CheckRow>();
        ICollectionView _view;
        CheckResult _last;
        bool _busy;

        Document _cmdDoc;                                // документ, на чей CommandEnded мы подписаны
        bool _refreshAfterCommand;                       // ждём завершения SDK_CALC_PANEL, чтобы обновиться

        // ---------------------------------------------------------------- контролы
        TextBlock _title, _status;
        CheckBox _cbFatal, _cbError, _cbNotice;
        TextBox _search;
        TextBlock _searchHint;
        DataGrid _grid;
        Button _btnCalc, _btnJson, _btnSpec, _btnCable;              // группа ПРОЕКТ
        Button _btnShow, _btnRefresh, _btnExcel, _btnClose;          // группа ТАБЛИЦА

        static readonly Brush BrFatal = Freeze("#F8D0D0"), BrError = Freeze("#FBE1C8"), BrNotice = Freeze("#FFF5C2");
        static readonly Brush DotFatal = Freeze("#D64545"), DotError = Freeze("#E8862B"), DotNotice = Freeze("#D9B400");

        CheckWindow()
        {
            Title = "Проверка проекта KNX";
            Width = 1200; Height = 640; MinWidth = 1080; MinHeight = 440;
            FontFamily = new FontFamily("Segoe UI"); FontSize = 12;
            Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));
            ShowInTaskbar = false;
            if (_savedBounds.HasValue)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = _savedBounds.Value.Left; Top = _savedBounds.Value.Top;
                Width = _savedBounds.Value.Width; Height = _savedBounds.Value.Height;
            }
            else WindowStartupLocation = WindowStartupLocation.CenterScreen;

            Content = BuildLayout();

            PreviewKeyDown += OnKey;
            Closing += (s, e) =>
            {
                _savedBounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
                AcApp.DocumentManager.DocumentActivated -= OnDocActivated;
                UnhookCommandEvents();
                _instance = null;
            };
            AcApp.DocumentManager.DocumentActivated += OnDocActivated;
        }

        // ================================================================ разметка
        UIElement BuildLayout()
        {
            var root = new Grid { Margin = new Thickness(10) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // шапка + фильтр
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // таблица
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // кнопки
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // статус

            // --- шапка
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            _title = new TextBlock { FontSize = 15, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Text = "Проверка проекта" };
            DockPanel.SetDock(_title, Dock.Left);
            head.Children.Add(_title);

            var filter = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            filter.Children.Add(new TextBlock { Text = "Показывать:", Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.DimGray });
            _cbFatal = MakeLevelBox("Фатально", DotFatal);
            _cbError = MakeLevelBox("Ошибки", DotError);
            _cbNotice = MakeLevelBox("Замечания", DotNotice);
            filter.Children.Add(_cbFatal); filter.Children.Add(_cbError); filter.Children.Add(_cbNotice);

            var searchBox = new Grid { Width = 220, Margin = new Thickness(16, 0, 0, 0) };
            _search = new TextBox { Padding = new Thickness(4, 3, 4, 3), VerticalContentAlignment = VerticalAlignment.Center };
            _searchHint = new TextBlock { Text = "Поиск по любому тексту…", Foreground = Brushes.Gray, Margin = new Thickness(7, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
            _search.TextChanged += (s, e) => { _searchHint.Visibility = _search.Text.Length == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed; ApplyFilter(); };
            searchBox.Children.Add(_search); searchBox.Children.Add(_searchHint);
            filter.Children.Add(searchBox);
            head.Children.Add(filter);
            Grid.SetRow(head, 0); root.Children.Add(head);

            // --- таблица
            _grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
                CanUserResizeRows = false,
                RowHeaderWidth = 0,
                Background = Brushes.White,
                BorderBrush = Brushes.LightGray,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            _grid.Columns.Add(Col("№", "N", 44, false));
            _grid.Columns.Add(Col("Уровень", "LevelText", 84, false));
            _grid.Columns.Add(Col("Код", "Code", 56, false));
            _grid.Columns.Add(Col("Щит", "Panel", 70, false));
            _grid.Columns.Add(Col("Объект", "Where", 3, true));
            _grid.Columns.Add(Col("Подробности", "Details", 2, true));
            _grid.Columns.Add(Col("Что делать", "Action", 2, true));
            _grid.LoadingRow += (s, e) =>
            {
                var r = e.Row.Item as CheckRow;
                if (r == null) return;
                e.Row.Background = BrushFor(r.Level);
                e.Row.ToolTip = r.Code + " — " + r.Title;
            };
            _grid.SelectionChanged += (s, e) => _btnShow.IsEnabled = _grid.SelectedItem != null;
            _grid.MouseDoubleClick += (s, e) =>
            {
                var dep = e.OriginalSource as DependencyObject;
                while (dep != null && !(dep is DataGridRow) && !(dep is DataGridColumnHeader)) dep = VisualTreeHelper.GetParent(dep);
                if (dep is DataGridRow) ShowInModel();
            };
            Grid.SetRow(_grid, 1); root.Children.Add(_grid);

            // --- кнопки
            var foot = BuildFooter();
            Grid.SetRow(foot, 2); root.Children.Add(foot);

            // --- статус
            _status = new TextBlock { Foreground = Brushes.DimGray, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetRow(_status, 3); root.Children.Add(_status);

            return root;
        }

        /// Нижняя панель: слева группа ПРОЕКТ (команды AutoCAD), справа группа ТАБЛИЦА (работа с этим окном).
        UIElement BuildFooter()
        {
            var grid = new Grid { Margin = new Thickness(0, 10, 0, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // ---- ПРОЕКТ
            _btnCalc = MakeBtn("▶  Расчёт щитов", 140,
                "Команда " + CmdCalc + ": токи, нумерация автоматов, итоги щитов.\nПосле выполнения таблица проверок обновится сама.",
                (s, e) => RunAcadCommand(CmdCalc, refreshWhenDone: true));
            _btnJson = MakeBtn("{ }  JSON", 90,
                "Команда " + CmdJson + ": выгрузка модели проекта в JSON.",
                (s, e) => RunAcadCommand(CmdJson, refreshWhenDone: false));
            _btnSpec = MakeBtn("Спецификация", 120,
                string.IsNullOrEmpty(CmdSpec) ? "Зарезервировано: спецификация оборудования (Excel). Команда ещё не подключена."
                                              : "Команда " + CmdSpec,
                (s, e) => RunAcadCommand(CmdSpec, refreshWhenDone: false),
                enabled: !string.IsNullOrEmpty(CmdSpec));
            _btnCable = MakeBtn("Кабельный журнал", 140,
                string.IsNullOrEmpty(CmdCable) ? "Зарезервировано: кабельный журнал (Excel). Команда ещё не подключена."
                                               : "Команда " + CmdCable,
                (s, e) => RunAcadCommand(CmdCable, refreshWhenDone: false),
                enabled: !string.IsNullOrEmpty(CmdCable));
            var project = MakeGroup("ПРОЕКТ", _btnCalc, _btnJson, _btnSpec, _btnCable);
            Grid.SetColumn(project, 0);
            grid.Children.Add(project);

            // ---- ТАБЛИЦА
            _btnShow = MakeBtn("Показать в модели", 150,
                "Подъехать к объекту выбранной строки и выделить его на чертеже (Enter, двойной щелчок).",
                (s, e) => ShowInModel(), enabled: false);
            _btnRefresh = MakeBtn("Обновить  (F5)", 120,
                "Перечитать чертёж и выполнить проверки заново; фильтр и выделение сохраняются.",
                (s, e) => Refresh());
            _btnExcel = MakeBtn("В Excel…", 100,
                "Выгрузить результат и справочник проверок в Excel.",
                (s, e) => ExportExcel(), enabled: false);
            _btnClose = MakeBtn("Закрыть", 90, "Закрыть окно (Esc).", (s, e) => Close());
            _btnClose.Margin = new Thickness(0);
            var table = MakeGroup("ТАБЛИЦА", _btnShow, _btnRefresh, _btnExcel, _btnClose);
            Grid.SetColumn(table, 2);
            grid.Children.Add(table);

            return grid;
        }

        static DataGridTextColumn Col(string header, string path, double width, bool star)
        {
            var c = new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(path),
                Width = star ? new DataGridLength(width, DataGridLengthUnitType.Star) : new DataGridLength(width)
            };
            var st = new Style(typeof(TextBlock));
            st.Setters.Add(new Setter(TextBlock.TextWrappingProperty, star ? TextWrapping.Wrap : TextWrapping.NoWrap));
            st.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(4, 2, 4, 2)));
            c.ElementStyle = st;
            return c;
        }

        CheckBox MakeLevelBox(string text, Brush dot)
        {
            var p = new StackPanel { Orientation = Orientation.Horizontal };
            p.Children.Add(new Border { Width = 10, Height = 10, Background = dot, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center });
            p.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
            var cb = new CheckBox { Content = p, IsChecked = true, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
            cb.Checked += (s, e) => ApplyFilter();
            cb.Unchecked += (s, e) => ApplyFilter();
            return cb;
        }

        static Button MakeBtn(string text, double width, string tip, RoutedEventHandler onClick, bool enabled = true)
        {
            var b = new Button
            {
                Content = text,
                ToolTip = tip,
                MinWidth = width,
                Height = 30,
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(10, 0, 10, 0),
                IsEnabled = enabled
            };
            if (onClick != null) b.Click += onClick;
            ToolTipService.SetShowOnDisabled(b, true);      // подсказка видна и на серой кнопке
            return b;
        }

        static UIElement MakeGroup(string caption, params UIElement[] buttons)
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical };
            panel.Children.Add(new TextBlock { Text = caption, FontSize = 11, Foreground = Brushes.Gray, Margin = new Thickness(2, 0, 0, 4) });
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var b in buttons) row.Children.Add(b);
            panel.Children.Add(row);
            return panel;
        }

        static Brush Freeze(string hex) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex); b.Freeze(); return b; }
        static Brush BrushFor(CheckLevel l) => l == CheckLevel.Fatal ? BrFatal : l == CheckLevel.Error ? BrError : BrNotice;

        // ================================================================ команды AutoCAD из окна
        /// Запуск команды через очередь ввода активного документа. Два Escape впереди снимают
        /// незавершённую команду, если пользователь что-то начал в модели.
        void RunAcadCommand(string cmd, bool refreshWhenDone)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return;
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) { SetStatus("Нет активного чертежа."); return; }

            try
            {
                if (refreshWhenDone)
                {
                    UnhookCommandEvents();                    // если подписка висела на другом документе
                    _cmdDoc = doc;
                    _refreshAfterCommand = true;
                    doc.CommandEnded += OnAcadCommandEnded;
                    doc.CommandCancelled += OnAcadCommandEnded;
                    doc.CommandFailed += OnAcadCommandEnded;
                }
                doc.SendStringToExecute("\x1B\x1B" + cmd + " ", true, false, true);
                try { Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView(); } catch { }
                SetStatus("Запущена команда " + cmd + " — смотрите командную строку AutoCAD.");
            }
            catch (System.Exception ex)
            {
                UnhookCommandEvents();
                SetStatus("Не удалось запустить " + cmd + ": " + ex.Message);
                doc.Editor.WriteMessage("\nНе удалось запустить {0}: {1}\n", cmd, ex.Message);
            }
        }

        /// После расчёта таблица проверок устарела — перечитываем чертёж сами.
        void OnAcadCommandEnded(object sender, CommandEventArgs e)
        {
            var name = (e.GlobalCommandName ?? "").Trim().ToUpperInvariant();
            if (name != CmdCalc.ToUpperInvariant()) return;      // чужие команды (ZOOM, REGEN…) пропускаем

            UnhookCommandEvents();
            // Мы в контексте AutoCAD; обновление окна — через его диспетчер, после того как команда полностью отработает.
            Dispatcher.BeginInvoke(new Action(() => Refresh()));
        }

        void UnhookCommandEvents()
        {
            if (_cmdDoc != null)
            {
                try
                {
                    _cmdDoc.CommandEnded -= OnAcadCommandEnded;
                    _cmdDoc.CommandCancelled -= OnAcadCommandEnded;
                    _cmdDoc.CommandFailed -= OnAcadCommandEnded;
                }
                catch { }
                _cmdDoc = null;
            }
            _refreshAfterCommand = false;
        }

        // ================================================================ обновление
        public void Refresh()
        {
            if (_busy) return;
            _busy = true;
            var selKey = (_grid.SelectedItem as CheckRow)?.Key;
            Cursor = Cursors.Wait; SetStatus("Проверка…");
            try
            {
                _last = CheckRunner.Collect();

                var rows = new List<CheckRow>();
                if (_last.Log != null)
                {
                    int n = 0;
                    foreach (var it in _last.Log.Sorted) rows.Add(CheckRow.From(++n, it));
                }
                _rows = rows;
                _view = CollectionViewSource.GetDefaultView(_rows);
                _view.Filter = FilterRow;
                _grid.ItemsSource = _view;

                _title.Text = "Проверка проекта — " + (_last.DwgName ?? "");
                UpdateCounts();
                _btnExcel.IsEnabled = _last.Ctx != null && _rows.Count > 0;      // без контекста выгружать нечего
                _btnShow.IsEnabled = _grid.SelectedItem != null;

                if (selKey != null)
                {
                    var again = _rows.FirstOrDefault(r => r.Key == selKey);
                    if (again != null && _view.Contains(again)) { _grid.SelectedItem = again; _grid.ScrollIntoView(again); }
                }

                string summary = Summary();
                SetStatus(_last.Error != null ? "Проверка прервана: " + _last.Error + "   ·   " + summary : summary);

                // след в командной строке: статистика чертежа + итог (или причина остановки)
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc != null)
                    doc.Editor.WriteMessage("\n{0}: {1}\n{2}\n", CheckRunner.CMD, _last.Stats ?? "", _last.Error ?? summary);
            }
            catch (System.Exception ex) { SetStatus("Ошибка окна: " + ex.Message); }
            finally { Cursor = null; _busy = false; }
        }

        bool FilterRow(object o)
        {
            var r = o as CheckRow;
            if (r == null) return false;
            bool levelOk = r.Level == CheckLevel.Fatal ? _cbFatal.IsChecked == true
                         : r.Level == CheckLevel.Error ? _cbError.IsChecked == true
                                                       : _cbNotice.IsChecked == true;
            return levelOk && r.Matches(_search.Text);
        }

        void ApplyFilter()
        {
            if (_view == null) return;
            _view.Refresh();
            if (_last != null) SetStatus(Summary());
        }

        void UpdateCounts()
        {
            SetCount(_cbFatal, "Фатально", _rows.Count(r => r.Level == CheckLevel.Fatal));
            SetCount(_cbError, "Ошибки", _rows.Count(r => r.Level == CheckLevel.Error));
            SetCount(_cbNotice, "Замечания", _rows.Count(r => r.Level == CheckLevel.Notice));
        }

        static void SetCount(CheckBox cb, string text, int n)
        {
            if (cb.Content is StackPanel p && p.Children.Count > 1 && p.Children[1] is TextBlock tb)
                tb.Text = text + " (" + n + ")";
        }

        string Summary()
        {
            int shown = _view == null ? 0 : _view.Cast<object>().Count();
            return "Проверено " + _last.When.ToString("HH:mm:ss") +
                   " · фатально " + _rows.Count(r => r.Level == CheckLevel.Fatal) +
                   ", ошибок " + _rows.Count(r => r.Level == CheckLevel.Error) +
                   ", замечаний " + _rows.Count(r => r.Level == CheckLevel.Notice) +
                   " · показано " + shown + " из " + _rows.Count;
        }

        void SetStatus(string text) => _status.Text = text;

        // ================================================================ показать в модели
        void ShowInModel()
        {
            var r = _grid.SelectedItem as CheckRow;
            if (r == null) { SetStatus("Выберите строку."); return; }
            if (string.IsNullOrEmpty(r.Handle))
            {
                SetStatus("У этой записи нет объекта на чертеже — она относится к " + (string.IsNullOrEmpty(r.Panel) ? "чертежу" : "щиту " + r.Panel) + " целиком.");
                return;
            }
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) { SetStatus("Нет активного чертежа."); return; }
            if (_last != null && !string.Equals(doc.Name, _last.DwgPath, StringComparison.OrdinalIgnoreCase))
            { SetStatus("Активен другой чертёж — нажмите «Обновить» (F5)."); return; }

            long h;
            if (!long.TryParse(r.Handle.Trim('[', ']'), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out h))
            { SetStatus("«" + r.Handle + "» — это не handle."); return; }

            ObjectId id;
            if (!doc.Database.TryGetObjectId(new Handle(h), out id) || id.IsErased)
            { SetStatus("Объект " + r.Handle + " не найден: удалён или чертёж изменился — F5."); return; }

            try
            {
                using (doc.LockDocument())
                {
                    Extents3d? ext = null;
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent != null) { try { ext = ent.GeometricExtents; } catch { } }
                        tr.Commit();
                    }
                    if (!ext.HasValue) { SetStatus("У объекта " + r.Handle + " нет геометрии."); return; }

                    var e = ext.Value;
                    double w = e.MaxPoint.X - e.MinPoint.X, hh = e.MaxPoint.Y - e.MinPoint.Y;
                    double pad = Math.Max(Math.Max(w, hh) * 2.0, 20.0);          // объект ~ треть экрана, соседи видны
                    var ed = doc.Editor;
                    using (var v = ed.GetCurrentView())
                    {
                        v.CenterPoint = new Point2d((e.MinPoint.X + e.MaxPoint.X) / 2, (e.MinPoint.Y + e.MaxPoint.Y) / 2);
                        v.Width = w + pad; v.Height = hh + pad;
                        ed.SetCurrentView(v);
                    }
                    ed.SetImpliedSelection(new[] { id });
                    ed.UpdateScreen();
                }
                SetStatus("Показан: " + r.Where);
            }
            catch (System.Exception ex) { SetStatus("Не удалось показать объект: " + ex.Message); }
        }

        // ================================================================ Excel
        void ExportExcel()
        {
            if (_last == null || _last.Log == null || _rows.Count == 0) return;
            if (_last.Ctx == null) { SetStatus("Проверки не выполнялись — выгружать нечего."); return; }
            try
            {
                string note;
                string path = CheckExcel.Export(_last.Log, _last.Ctx, _last.DwgPath, out note);
                if (string.IsNullOrEmpty(path)) { SetStatus(note ?? "Выгрузка отменена."); return; }
                SetStatus(string.IsNullOrEmpty(note) ? "Excel: " + path : note + "  " + path);
                try { Process.Start(path); } catch { SetStatus("Файл записан, откройте вручную: " + path); }
            }
            catch (System.Exception ex) { SetStatus("Excel: " + ex.Message); }
        }

        // ================================================================ клавиши и смена чертежа
        void OnKey(object s, KeyEventArgs e)
        {
            if (e.Key == Key.F5) { Refresh(); e.Handled = true; }
            else if (e.Key == Key.Escape) { Close(); e.Handled = true; }
            else if (e.Key == Key.Enter && _grid.IsKeyboardFocusWithin) { ShowInModel(); e.Handled = true; }
        }

        void OnDocActivated(object s, DocumentCollectionEventArgs e)
        {
            if (_last == null || e.Document == null) return;
            if (!string.Equals(e.Document.Name, _last.DwgPath, StringComparison.OrdinalIgnoreCase))
                SetStatus("Активен другой чертёж — нажмите «Обновить» (F5), чтобы проверить его.");
        }
    }
}
