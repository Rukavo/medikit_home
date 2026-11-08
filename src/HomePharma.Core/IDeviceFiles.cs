namespace HomePharma.Core;
public interface IDeviceFiles
{
    Task<string?> ReadTextAsync(string kind);
    Task WriteTextAsync(string name, string text);
    Task<string> ReadSeedAsync();
    Task OpenSourceAsync(string url);
    Task RefreshRemindersAsync() => Task.CompletedTask;
    Task ConfigureDoseNotificationsAsync() => Task.CompletedTask;
}
