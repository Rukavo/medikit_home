using Microsoft.JSInterop;
using HomePharma.Core;
using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace HomePharma.Components.Pages;

public partial class Home
{
    [Inject] public PharmaStore Store { get; set; } = default!;
    [Inject] public IDeviceFiles Files { get; set; } = default!;
    [Inject] public Microsoft.JSInterop.IJSRuntime Js { get; set; } = default!;
    private string renderedLanguage = "";
    private List<Medication> meds = [];
    private List<string> categories = [];
    private string view = "inventory", query = "", category = "", filter = "all", sort = "name";
    private bool cards, loading = true, busy;
    private string error = "", message = "", modal = "", categoryName = "", originalCategory = "";
    private Medication? selected, draft, deleting;
    private string draftTimes = "", formError = "";
    private CsvImport? batch;
    private Backup? restore;
    private DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
    private async Task ChangeLanguage(ChangeEventArgs args)
    {
        await Run(async () => { L.Set(args.Value?.ToString() ?? "es"); message = L.T("Idioma guardado."); await Files.RefreshRemindersAsync(); });
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (renderedLanguage == L.Code) return;
        renderedLanguage = L.Code;
        await Js.InvokeVoidAsync("medikitLanguage", L.Code, L.T("Ha ocurrido un error. Reinicia Medikit Home para volver a abrir tu botiquín. Los datos guardados se conservan."));
    }
    private string Title => view switch { "treatments" => L.T("Tus tratamientos"), "categories" => L.T("Tus categorías"), "data" => L.T("Tus datos, contigo"), _ => L.T("Tu botiquín") };
    private string Subtitle => view switch { "treatments" => L.T("Las pautas que has registrado, organizadas por hora."), "categories" => L.T("Organiza los medicamentos a tu manera."), "data" => L.T("Importa, exporta y conserva una copia de tu botiquín."), _ => L.T("Todo lo que necesitas, en su sitio.") };
    private IEnumerable<Medication> Filtered
    {
        get
        {
            var result = meds.Where(m => (category.Length == 0 || m.Categories.Contains(category, StringComparer.OrdinalIgnoreCase)) &&
                (query.Length == 0 || CultureInfo.CurrentCulture.CompareInfo.IndexOf(m.Name + " " + m.Dose + " " + m.Description + " " + string.Join(' ', m.Categories), query, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0));
            result = filter switch { "expired" => result.Where(m => m.IsExpired(Today)), "soon" => result.Where(m => m.IsSoon(Today)), "empty" => result.Where(m => !m.InStock), "stock" => result.Where(m => m.InStock), "active" => result.Where(m => m.Active), _ => result };
            return sort switch { "expiry" => result.OrderBy(m => m.ExpiryDate ?? DateOnly.MaxValue).ThenBy(m => m.Name), "recent" => result.Reverse(), _ => result.OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase) };
        }
    }
    private IEnumerable<(Medication Med, string Time)> Agenda => meds.Where(m => m.IsScheduled(Today) && (category.Length == 0 || m.Categories.Contains(category, StringComparer.OrdinalIgnoreCase)))
        .SelectMany(m => m.Times.Select(t => (Med: m, Time: t))).OrderBy(t => t.Time).ThenBy(t => t.Med.Name);
    private string Status(Medication m) => m.IsExpired(Today) ? L.T("Caducado") : m.IsSoon(Today) ? L.T("Caduca pronto") : m.Expiry.Length == 0 ? L.T("Sin fecha") : L.T("En fecha");
    private string StatusClass(Medication m) => m.IsExpired(Today) ? "danger" : m.IsSoon(Today) ? "amber" : m.Expiry.Length == 0 ? "neutral" : "green";
    private string Rx(string value) => value switch { "si" => L.T("Con receta"), "no" => L.T("Sin receta"), "no_aplica" => L.T("No aplica"), _ => L.T("Receta por verificar") };
    protected override async Task OnInitializedAsync()
    {
        try { await Store.InitializeAsync(Files.ReadSeedAsync); await Reload(); }
        catch (Exception ex) { error = L.T("No se pudo abrir el botiquín. ") + L.Error(ex.Message); }
        finally { loading = false; }
    }
    private async Task Reload() { meds = await Store.AllAsync(); categories = await Store.CategoriesAsync(); await Files.RefreshRemindersAsync(); selected = selected == null ? null : meds.FirstOrDefault(m => m.Id == selected.Id); }
    private async Task Run(Func<Task> action)
    {
        if (busy) return; busy = true; error = "";
        try { await action(); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { error = L.Error(ex.Message); }
        finally { busy = false; }
    }
    private void Navigate(string target) { view = target; selected = null; query = ""; filter = "all"; message = ""; }
    private void SetFilter(string target) { view = "inventory"; filter = target; selected = null; }
    private void Edit(Medication? med = null)
    {
        draft = med?.Copy() ?? new Medication { StartDate = Today.ToString("yyyy-MM-dd") };
        draftTimes = string.Join(" | ", draft.Times); formError = ""; modal = "edit";
    }
    private async Task SaveMedication()
    {
        if (draft == null || busy) return; formError = "";
        try
        {
            draft.Times = draftTimes.Split(new[] { '|', ',', ';' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
            draft.Validate(); busy = true; await Store.SaveAsync(draft); await Reload(); selected = meds.First(m => m.Id == draft.Id); modal = ""; message = L.T("Medicamento guardado.");
        }
        catch (Exception ex) { formError = L.Error(ex.Message); }
        finally { busy = false; }
    }
    private Task ToggleStock(Medication med) => Run(async () => { var copy = med.Copy(); copy.InStock = !copy.InStock; await Store.SaveAsync(copy); await Reload(); message = copy.InStock ? L.T("Marcado como disponible.") : L.T("Marcado como agotado."); });
    private Task ToggleActive(Medication med) => Run(async () => { var copy = med.Copy(); copy.Active = false; await Store.SaveAsync(copy); await Reload(); message = L.T("Tratamiento desactivado. La pauta se conserva en la ficha."); });
    private Task DeleteMedication() => Run(async () => { if (deleting == null) return; await Store.DeleteAsync(deleting.Id); await Reload(); modal = ""; deleting = null; message = L.T("Medicamento eliminado."); });
    private void OpenCategory(string? name = null) { originalCategory = name ?? ""; categoryName = name ?? ""; formError = ""; modal = "category"; }
    private async Task SaveCategory()
    {
        if (busy) return; formError = "";
        try { busy = true; if (originalCategory.Length == 0) await Store.AddCategoryAsync(categoryName); else await Store.ChangeCategoryAsync(originalCategory, categoryName); if (category == originalCategory) category = categoryName.Trim(); await Reload(); modal = ""; message = L.T("Categoría guardada."); }
        catch (Exception ex) { formError = L.Error(ex.Message); } finally { busy = false; }
    }
    private Task RemoveCategory() => Run(async () => { await Store.ChangeCategoryAsync(originalCategory, null); if (category == originalCategory) category = ""; await Reload(); modal = ""; message = L.T("Categoría eliminada. Los medicamentos se conservan."); });
    private Task PickCsv() => Run(async () => { var text = await Files.ReadTextAsync("csv"); if (text == null) return; batch = CsvCodec.Import(text); modal = "import"; });
    private int NewCount
    {
        get
        {
            if (batch == null) return 0;
            var ids = meds.Select(m => m.Id).ToHashSet(); var keys = meds.Select(m => m.Fingerprint).ToHashSet(); int count = 0;
            foreach (var med in batch.Medications) { if (ids.Contains(med.Id) || keys.Contains(med.Fingerprint)) continue; ids.Add(med.Id); keys.Add(med.Fingerprint); count++; }
            return count;
        }
    }
    private Task ConfirmImport() => Run(async () => { if (batch == null) return; var count = await Store.ImportAsync(batch); await Reload(); modal = ""; message = L.Format("Importación completada: {0} añadidos. Los duplicados se han omitido.", count); });
    private Task ExportCsv(bool template = false) => Run(async () => { await Files.WriteTextAsync(template ? "plantilla_medikit_home.csv" : "medicamentos_medikit_home.csv", CsvCodec.Export(meds, template)); message = L.T("Exportación preparada."); });
    private Task ExportBackup() => Run(async () => { await Files.WriteTextAsync($"medikit_home_{Today:yyyy-MM-dd}.json", await Store.BackupAsync()); message = L.T("Copia preparada."); });
    private Task PickBackup() => Run(async () => { var text = await Files.ReadTextAsync("json"); if (text == null) return; restore = PharmaStore.ReadBackup(text); modal = "restore"; });
    private Task ConfirmRestore() => Run(async () => { if (restore == null) return; await Store.RestoreAsync(restore); selected = null; category = ""; await Reload(); modal = ""; message = L.T("Copia restaurada."); });
    private Task OpenSource(Medication med) => Run(() => Files.OpenSourceAsync(med.SourceUrl));
    private void CloseModal() { if (!busy) { modal = ""; formError = ""; } }
}

