using HomePharma.Core;
using System.Text;
namespace HomePharma.Services;
public sealed class DeviceFiles(AppLanguage language) : IDeviceFiles
{
    public async Task<string?> ReadTextAsync(string kind)
    {
        var types = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.WinUI] = [kind == "csv" ? ".csv" : ".json"],
        });
        var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = kind == "csv" ? language.T("Importar medicamentos") : language.T("Restaurar copia de Medikit Home"), FileTypes = types });
        if (file == null) return null;
        using var stream = await file.OpenReadAsync(); using var memory = new MemoryStream();
        var buffer = new byte[8192]; int read;
        while ((read = await stream.ReadAsync(buffer)) > 0) { if (memory.Length + read > 5_000_000) throw new FormatException(language.T("El archivo supera el límite de 5 MB.")); memory.Write(buffer, 0, read); }
        var bytes = memory.ToArray();
        try { return new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException) { return Encoding.Latin1.GetString(bytes); }
    }
    public async Task WriteTextAsync(string name, string text)
    {
var picker = new Windows.Storage.Pickers.FileSavePicker();
        var window = (Microsoft.UI.Xaml.Window)Application.Current!.Windows[0].Handler!.PlatformView!;
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
        picker.SuggestedFileName = Path.GetFileNameWithoutExtension(name);
        picker.FileTypeChoices.Add(name.EndsWith(".csv") ? "CSV UTF-8" : language.T("Copia Medikit Home JSON"), new List<string> { Path.GetExtension(name) });
        var file = await picker.PickSaveFileAsync();
        if (file == null) throw new OperationCanceledException();
        await Windows.Storage.FileIO.WriteTextAsync(file, text, Windows.Storage.Streams.UnicodeEncoding.Utf8);

    }
    public async Task<string> ReadSeedAsync()
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync("medicamentos.csv"); using var reader = new StreamReader(stream, Encoding.UTF8); return await reader.ReadToEndAsync();
    }
    public async Task RefreshRemindersAsync()
    {

        await Task.CompletedTask;
    }
    public async Task ConfigureDoseNotificationsAsync()
    {

        await RefreshRemindersAsync();
    }
    public Task OpenSourceAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https") throw new FormatException(language.T("Fuente no válida."));
        return Browser.Default.OpenAsync(uri, BrowserLaunchMode.External);
    }
}
