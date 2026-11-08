using System.Text.Json.Serialization;
namespace HomePharma.Core;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Medication))]
[JsonSerializable(typeof(Backup))]
[JsonSerializable(typeof(Dictionary<string,string>))]
public partial class PharmaJson : JsonSerializerContext { }
