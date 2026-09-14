using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KnxTools
{
    public enum Severity { Info, Warning, Error }

    public class Issue
    {
        public Severity Severity;
        public string Handle;
        public string Where;
        public string Message;
        public override string ToString() =>
            $"[{Severity}] {(string.IsNullOrEmpty(Handle) ? "" : "<" + Handle + "> ")}{Where}: {Message}";
    }

    /// Накопитель проблем. Инструмент никогда не падает — он пишет сюда.
    public class Report
    {
        public List<Issue> Issues { get; } = new List<Issue>();

        public void Info(string where, string message, string handle = null) => Add(Severity.Info, where, message, handle);
        public void Warn(string where, string message, string handle = null) => Add(Severity.Warning, where, message, handle);
        public void Error(string where, string message, string handle = null) => Add(Severity.Error, where, message, handle);

        private void Add(Severity s, string where, string message, string handle)
            => Issues.Add(new Issue { Severity = s, Where = where ?? "", Message = message ?? "", Handle = handle ?? "" });

        public int Errors => Issues.Count(i => i.Severity == Severity.Error);
        public int Warnings => Issues.Count(i => i.Severity == Severity.Warning);
        public int Infos => Issues.Count(i => i.Severity == Severity.Info);

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Ошибок: {Errors}, предупреждений: {Warnings}, заметок: {Infos}");
            foreach (var i in Issues) sb.AppendLine(i.ToString());
            return sb.ToString();
        }
    }
}