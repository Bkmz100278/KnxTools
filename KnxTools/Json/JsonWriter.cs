using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace KnxTools.Json
{
    public static class JsonWriter
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Include,       // null — тоже данные: «поле есть, значение не задано»
            Culture = CultureInfo.InvariantCulture,
            ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() }
        };

        public static string Serialize(KnxExport export) => JsonConvert.SerializeObject(export, Settings);

        /// Какая именно Newtonsoft.Json загрузилась и откуда — для диагностики и для отчёта на защите.
        public static string LibraryInfo()
        {
            var a = typeof(JsonConvert).Assembly;
            return $"{a.GetName().Name} {a.GetName().Version} ({a.Location})";
        }
    }
}
