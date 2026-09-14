using System.Collections.Generic;

namespace KnxTools.Json
{
    public sealed class KnxExport
    {
        public string Drawing { get; set; }
        public string Generated { get; set; }
        public List<DeviceDto> Devices { get; set; } = new List<DeviceDto>();
        public List<GroupAddressDto> GroupAddresses { get; set; } = new List<GroupAddressDto>();
        public List<ErrorDto> Errors { get; set; } = new List<ErrorDto>();
    }

    public sealed class PaDto
    {
        public int Area { get; set; }
        public int Line { get; set; }
        public int Device { get; set; }
        public string Text => $"{Area}.{Line}.{Device}";
    }

    public sealed class DeviceDto
    {
        public PaDto Pa { get; set; }
        public string Manufacturer { get; set; }
        public string Article { get; set; }
        public string Name { get; set; }
        public string DeviceType { get; set; }
        public string Panel { get; set; }
        public string Position { get; set; }
        public List<OutputDto> Outputs { get; set; } = new List<OutputDto>();
        public List<InputDto> Inputs { get; set; } = new List<InputDto>();
        public List<DaliDto> Dali { get; set; } = new List<DaliDto>();
        public string Handle { get; set; }
    }

    /// Ссылка на ГА внутри канала.
    public sealed class GaRefDto
    {
        public string Text { get; set; }
        public string Dpt { get; set; }
    }

    public sealed class OutputDto
    {
        public int? No { get; set; }
        public string LoadType { get; set; }
        public string Load { get; set; }
        public string Room { get; set; }
        public List<GaRefDto> Ga { get; set; } = new List<GaRefDto>();
    }

    public sealed class InputDto
    {
        public int? No { get; set; }
        public string ControlDevice { get; set; }
        public string Room { get; set; }
        public List<GaRefDto> GaControls { get; set; } = new List<GaRefDto>();
        public List<GaRefDto> GaStatus { get; set; } = new List<GaRefDto>();
    }

    public sealed class DaliDto
    {
        public int Group { get; set; }
        public List<int> Ballasts { get; set; } = new List<int>();
        public string Load { get; set; }
        public string Room { get; set; }
        public List<GaRefDto> Ga { get; set; } = new List<GaRefDto>();
    }

    /// Запись книги групповых адресов (то, что импортируется в ETS).
    public sealed class GroupAddressDto
    {
        public string Text => $"{Main}/{Middle}/{Sub}";
        public int Main { get; set; }
        public int Middle { get; set; }
        public int Sub { get; set; }
        public string Name { get; set; }
        public string Dpt { get; set; }
    }

    public sealed class ErrorDto
    {
        public string Severity { get; set; }
        public string Handle { get; set; }
        public string Message { get; set; }
    }
}