using System.Globalization;
using System.Text;

namespace HomePharma.Core;

public sealed record CsvImport(List<Medication> Medications, List<string> Errors, List<string> Warnings);

public static class CsvCodec
{
    public static readonly string[] Headers = ["nombre_medicamento", "dosis_medicamento", "fecha_caducidad", "stock_botiquin", "descripcion_uso", "requiere_receta", "categorias", "cantidad_envase", "tipo_producto", "tratamiento_activo", "cantidad_toma", "periodicidad_dias", "horarios_toma", "inicio_tratamiento", "fin_tratamiento", "notas", "fuente_uso", "fecha_revision_uso", "observaciones_revision", "id", "recordatorio_compra"];

    public static CsvImport Import(string text)
    {
        List<Medication> medications = []; List<string> errors = []; List<string> warnings = [];
        try
        {
            var rows = Parse(text.TrimStart('\uFEFF'));
            if (rows.Count == 0) return new([], ["El archivo está vacío."], []);
            var first = rows[0].Cells.Select(Normalize).ToArray();
            bool header = first.Contains("nombre_medicamento");
            var keys = header ? first : Headers[..5];
            if (header)
            {
                if (keys.Distinct().Count() != keys.Length) errors.Add("La cabecera contiene columnas repetidas.");
                foreach (var required in Headers[..5]) if (!keys.Contains(required)) errors.Add($"Falta la columna {required}.");
                foreach (var key in keys) if (!Headers.Contains(key)) warnings.Add($"Se ignorará la columna {key}.");
                if (errors.Count > 0) return new([], errors, warnings);
            }
            else warnings.Add("CSV sin cabecera: se ha usado el formato original de cinco columnas.");
            foreach (var row in rows.Skip(header ? 1 : 0))
            {
                var cells = row.Cells.ToList();
                try
                {
                    if (!header && cells.Count == 4 && LooksLikeDate(cells[1]))
                    {
                        cells.Insert(1, ""); warnings.Add($"Línea {row.Line}: dosis ausente; se ha recuperado la caducidad.");
                    }
                    if (cells.Count != keys.Length) throw new FormatException($"Se esperaban {keys.Length} columnas y hay {cells.Count}.");
                    string Get(string key, string fallback = "") { var i = Array.IndexOf(keys, key); return i < 0 ? fallback : cells[i].Trim(); }
                    var med = new Medication
                    {
                        Id = Get("id") is { Length: > 0 } id ? id : Guid.NewGuid().ToString(),
                        PurchaseReminder = Get("recordatorio_compra"), Name = Get("nombre_medicamento"), Dose = Get("dosis_medicamento"), Expiry = Get("fecha_caducidad"),
                        InStock = Boolean(Get("stock_botiquin")), Description = Get("descripcion_uso"), Prescription = NormalizeRx(Get("requiere_receta", "desconocido")),
                        Categories = Split(Get("categorias")), PackageQuantity = Get("cantidad_envase"), ProductType = Get("tipo_producto", "medicamento"),
                        Active = Boolean(Get("tratamiento_activo", "no")), Amount = Get("cantidad_toma"),
                        PeriodDays = Get("periodicidad_dias") is { Length: > 0 } days ? int.Parse(days, CultureInfo.InvariantCulture) : 1,
                        Times = Split(Get("horarios_toma")), StartDate = Get("inicio_tratamiento"), EndDate = Get("fin_tratamiento"),
                        Notes = Get("notas"), SourceUrl = Get("fuente_uso"), ReviewedOn = Get("fecha_revision_uso"), ReviewNotes = Get("observaciones_revision")
                    };
                    med.Validate(); medications.Add(med);
                }
                catch (Exception ex) when (ex is FormatException or OverflowException) { errors.Add($"Línea {row.Line}: {ex.Message}"); }
            }
        }
        catch (FormatException ex) { errors.Add(ex.Message); }
        if (medications.Count == 0 && errors.Count == 0) errors.Add("No hay medicamentos en el archivo.");
        return new(medications, errors, warnings);
    }

    public static string Export(IEnumerable<Medication> medications, bool headerOnly = false)
    {
        var rows = new List<IEnumerable<string>> { Headers };
        if (!headerOnly) rows.AddRange(medications.Select(m => new[] {
            m.Name,m.Dose,m.Expiry,m.InStock ? "si" : "no",m.Description,m.Prescription,string.Join('|',m.Categories),m.PackageQuantity,m.ProductType,m.Active ? "si" : "no",m.Amount,m.PeriodDays.ToString(CultureInfo.InvariantCulture),string.Join('|',m.Times),m.StartDate,m.EndDate,m.Notes,m.SourceUrl,m.ReviewedOn,m.ReviewNotes,m.Id,m.PurchaseReminder }));
        return "\uFEFF" + string.Join("\r\n", rows.Select(row => string.Join(',', row.Select(Quote)))) + "\r\n";
    }
    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    private static List<string> Split(string value) => value.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
    private static string Normalize(string value) => string.Concat(value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).Normalize(NormalizationForm.FormC);
    private static string NormalizeRx(string value) => Normalize(value) switch { "si" or "yes" or "true" or "1" => "si", "no" or "false" or "0" => "no", "" or "desconocido" => "desconocido", "no_aplica" => "no_aplica", _ => throw new FormatException("Valor de receta no válido.") };
    private static bool Boolean(string value) => Normalize(value) switch { "si" or "yes" or "true" or "1" => true, "no" or "false" or "0" => false, _ => throw new FormatException($"Valor sí/no no válido: {value}.") };
    private static bool LooksLikeDate(string value) { try { return Dates.NormalizeExpiry(value).Length > 0; } catch (FormatException) { return false; } }

    // RFC 4180. Delimiter detection ignores punctuation inside quoted fields.
    public static List<(int Line, List<string> Cells)> Parse(string text)
    {
        char delimiter = ','; bool inQuote = false; int commas = 0, semicolons = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i]; if (c == '"') inQuote = !inQuote;
            if (!inQuote && (c == '\r' || c == '\n')) break;
            if (!inQuote && c == ',') commas++; if (!inQuote && c == ';') semicolons++;
        }
        if (semicolons > commas) delimiter = ';';
        List<(int, List<string>)> rows = []; List<string> cells = []; var field = new StringBuilder();
        bool quoted = false, closed = false; int line = 1, rowLine = 1;
        void EndField() { cells.Add(field.ToString().Trim()); field.Clear(); closed = false; }
        void EndRow() { EndField(); if (cells.Any(c => c.Length > 0)) rows.Add((rowLine, cells)); cells = []; }
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; } else { quoted = false; closed = true; } }
                else { field.Append(c); if (c == '\n') line++; }
            }
            else if (c == delimiter) EndField();
            else if (c is '\r' or '\n') { EndRow(); if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; line++; rowLine = line; }
            else if (c == '"') { if (field.ToString().Trim().Length != 0 || closed) throw new FormatException($"Comillas inesperadas en línea {line}."); field.Clear(); quoted = true; }
            else if (closed && !char.IsWhiteSpace(c)) throw new FormatException($"Texto tras comillas en línea {line}.");
            else if (!closed) field.Append(c);
        }
        if (quoted) throw new FormatException($"Campo entre comillas sin cerrar en línea {rowLine}.");
        if (field.Length > 0 || cells.Count > 0 || closed) EndRow();
        return rows;
    }
}
