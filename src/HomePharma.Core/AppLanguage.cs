using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace HomePharma.Core;

public sealed class AppLanguage
{
    private static readonly Dictionary<string,string> English = Load();
    private readonly string? path;
    public string Code { get; private set; } = "es";
    public string CultureName => Code == "en" ? "en-GB" : "es-ES";
    public AppLanguage(string? path = null)
    {
        this.path = path;
        try { if (path != null && File.Exists(path) && File.ReadAllText(path).Trim() == "en") Code = "en"; }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
    private static Dictionary<string,string> Load()
    {
        using var stream = typeof(AppLanguage).Assembly.GetManifestResourceStream("HomePharma.Core.Translations.en.json")!;
        return JsonSerializer.Deserialize(stream, PharmaJson.Default.DictionaryStringString)!;
    }
    public string T(string text) => Code == "en" && English.TryGetValue(text, out var translated) ? translated : text;
    public string Format(string text, params object[] values) => string.Format(CultureInfo.GetCultureInfo(CultureName), T(text), values);
    public string Date(string value) => T(Dates.Display(value));
    public string ProductType(string value) => T(value switch { "medicamento" => "Medicamento", "cosmetico" => "Cosmético / higiene", "biocida" => "Biocida / repelente", "alimento" => "Alimento / rehidratación", _ => "Otro" });
    public void Set(string code)
    {
        if (code is not ("es" or "en")) throw new ArgumentException("Unsupported language.");
        if (path != null) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); File.WriteAllText(path, code); }
        Code = code;
    }
    public string Error(string text)
    {
        if (Code != "en") return text;
        if (English.TryGetValue(text, out var exact)) return exact;
        // System messages include CSV line numbers and user values; preserve those values.
        foreach (var (key, translated) in English.Where(k => k.Key.Contains("{0}")))
        {
            var pattern = Regex.Escape(key);
            pattern = Regex.Replace(pattern, @"\\\{(\d+)\}", m => $"(?<v{m.Groups[1].Value}>.+?)");
            var match = Regex.Match(text, "^" + pattern + "$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (!match.Success) continue;
            return Regex.Replace(translated, @"\{(\d+)\}", m => Error(match.Groups["v" + m.Groups[1].Value].Value));
        }
        return text;
    }
}
