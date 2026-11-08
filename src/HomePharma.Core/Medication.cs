using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomePharma.Core;

public sealed class Medication
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string PurchaseReminder { get; set; } = "";
    public string Dose { get; set; } = "";
    public string Expiry { get; set; } = "";
    public bool InStock { get; set; } = true;
    public string Description { get; set; } = "";
    public string Prescription { get; set; } = "desconocido";
    public string PackageQuantity { get; set; } = "";
    public string ProductType { get; set; } = "medicamento";
    public List<string> Categories { get; set; } = [];
    public bool Active { get; set; }
    public string Amount { get; set; } = "";
    public int PeriodDays { get; set; } = 1;
    public List<string> Times { get; set; } = [];
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public string Notes { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string ReviewedOn { get; set; } = "";
    public string ReviewNotes { get; set; } = "";

    public Medication Copy() => JsonSerializer.Deserialize(JsonSerializer.Serialize(this, PharmaJson.Default.Medication), PharmaJson.Default.Medication)!;
    [JsonIgnore] public string Fingerprint => $"{Name.Trim().ToUpperInvariant()}\u001f{Dose.Trim().ToUpperInvariant()}\u001f{Expiry}";
    [JsonIgnore] public DateOnly? ExpiryDate => Dates.ExpiryDate(Expiry);
    public bool IsExpired(DateOnly today) => ExpiryDate is { } date && date < today;
    public bool IsSoon(DateOnly today) => ExpiryDate is { } date && date >= today && date <= today.AddDays(90);
    public bool IsScheduled(DateOnly day) => Active && Dates.Day(StartDate) is { } start && day >= start
        && (string.IsNullOrEmpty(EndDate) || day <= Dates.Day(EndDate)) && (day.DayNumber - start.DayNumber) % PeriodDays == 0;

    public void Validate()
    {
        if (!Guid.TryParse(Id, out _)) throw new FormatException("El identificador no es válido.");
        Name = Name.Trim(); Dose = Dose.Trim();
        if (PurchaseReminder.Length > 0 && Dates.Day(PurchaseReminder) is null) throw new FormatException("La fecha de compra debe ser AAAA-MM-DD.");
        if (Name.Length is 0 or > 200) throw new FormatException("Escribe un nombre de hasta 200 caracteres.");
        Expiry = Dates.NormalizeExpiry(Expiry);
        if (!new[] { "si", "no", "desconocido", "no_aplica" }.Contains(Prescription)) throw new FormatException("El valor de receta no es válido.");
        if (!new[] { "medicamento", "cosmetico", "biocida", "alimento", "otro" }.Contains(ProductType)) throw new FormatException("El tipo de producto no es válido.");
        Categories = Categories.Select(c => c.Trim()).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (Categories.Any(c => c.Length > 80 || c.Contains('|'))) throw new FormatException("Las categorías deben tener hasta 80 caracteres y no contener |.");
        Times = Times.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct().Order().ToList();
        if (Times.Any(t => !TimeOnly.TryParseExact(t, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))) throw new FormatException("Usa horarios HH:mm, por ejemplo 08:00.");
        if (PeriodDays < 1 || PeriodDays > 365) throw new FormatException("La periodicidad debe ser de 1 a 365 días.");
        if (StartDate.Length > 0 && Dates.Day(StartDate) is null || EndDate.Length > 0 && Dates.Day(EndDate) is null) throw new FormatException("Las fechas de tratamiento deben ser AAAA-MM-DD.");
        if (Active && (string.IsNullOrWhiteSpace(Amount) || Times.Count == 0 || Dates.Day(StartDate) is null)) throw new FormatException("Un tratamiento activo necesita cantidad, fecha de inicio y al menos una hora.");
        if (Dates.Day(StartDate) is { } start && Dates.Day(EndDate) is { } end && end < start) throw new FormatException("El fin del tratamiento no puede ser anterior al inicio.");
        if (SourceUrl.Length > 0 && (!Uri.TryCreate(SourceUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https")) throw new FormatException("La fuente debe ser una dirección HTTPS.");
        if (ReviewedOn.Length > 0 && Dates.Day(ReviewedOn) is null) throw new FormatException("La fecha de consulta debe ser AAAA-MM-DD.");
    }
}

public static class Dates
{
    public static DateOnly? Day(string value) => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;
    public static string NormalizeExpiry(string value)
    {
        value = value.Trim();
        if (value.Length == 0) return "";
        foreach (var format in new[] { "yyyy-MM", "MM-yyyy", "MM/yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy" })
            if (DateOnly.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return date.ToString(format.Contains("dd") ? "yyyy-MM-dd" : "yyyy-MM", CultureInfo.InvariantCulture);
        throw new FormatException($"Caducidad no válida: {value}. Usa AAAA-MM o AAAA-MM-DD.");
    }
    public static DateOnly? ExpiryDate(string value)
    {
        if (value.Length == 0) return null;
        var normalized = NormalizeExpiry(value);
        if (normalized.Length == 7)
        {
            var first = DateOnly.ParseExact(normalized + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
            return new DateOnly(first.Year, first.Month, DateTime.DaysInMonth(first.Year, first.Month));
        }
        return Day(normalized);
    }
    public static string Display(string value) => value.Length == 7 ? $"{value[5..7]}/{value[..4]}" : Day(value)?.ToString("dd/MM/yyyy") ?? "Sin fecha";
}
