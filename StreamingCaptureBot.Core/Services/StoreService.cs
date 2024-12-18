using System.IO.IsolatedStorage;
using System.Text.Json;
using Lagrange.Core.Common;
using Microsoft.Extensions.Options;
using StreamingCaptureBot.Core.Configs;

namespace StreamingCaptureBot.Core.Services;
public class StoreService(ILogger<StoreService> logger, IOptions<LagrangeImplOption> implOptions) : IDisposable
{
    private readonly IsolatedStorageFile _storageFile = IsolatedStorageFile.GetStore(
        IsolatedStorageScope.User | IsolatedStorageScope.Application, null, null));
    private readonly Random _generator = new();

    private async Task SaveAsJson<T>(string filename, T content) where T : new()
    {
        await using var keyFileStream = _storageFile.OpenFile(filename, FileMode.Create, FileAccess.Write);
        await JsonSerializer.SerializeAsync(keyFileStream, content);
    }

    private T? ReadJsonOrDelete<T>(string filename) where T : new()
    {
        if (!_storageFile.FileExists(filename))
        {
            logger.LogWarning("File {name} not exists, return new.", filename);
            return default;
        }

        try
        {
            using var stream = _storageFile.OpenFile(filename, FileMode.Open, FileAccess.Read);
            return JsonSerializer.Deserialize<T>(stream) ?? new();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Read failed, delete invalid file.");
            _storageFile.DeleteFile(filename);
            return default;
        }
    }

    private async Task<T?> ReadJsonOrDeleteAsync<T>(string filename) where T : new()
    {
        if (!_storageFile.FileExists(filename))
        {
            logger.LogWarning("File {name} not exists, return new.", filename);
            return default;
        }

        try
        {
            await using var stream = _storageFile.OpenFile(filename, FileMode.Open, FileAccess.Read);
            return await JsonSerializer.DeserializeAsync<T>(stream) ?? new();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Read failed, delete invalid file.");
            _storageFile.DeleteFile(filename);
            return default;
        }
    }

    public BotKeystore ReadKeyStore()
        => ReadJsonOrDelete<BotKeystore>(implOptions.Value.KeyStoreFile)
           ?? new();

    public async Task<BotKeystore> ReadKeyStoreAsync()
        => await ReadJsonOrDeleteAsync<BotKeystore>(implOptions.Value.KeyStoreFile)
           ?? new();

    public BotDeviceInfo ReadDeviceInfo()
        => ReadJsonOrDelete<BotDeviceInfo>(implOptions.Value.DeviceInfoFile)
           ?? GenerateInfo();

    public async Task<BotDeviceInfo> ReadDeviceInfoAsync()
        => await ReadJsonOrDeleteAsync<BotDeviceInfo>(implOptions.Value.DeviceInfoFile)
           ?? GenerateInfo();

    public async Task SaveKeyStoreAsync(BotKeystore keyStore)
        => await SaveAsJson(implOptions.Value.KeyStoreFile, keyStore);

    public async Task SaveDeviceInfoAsync(BotDeviceInfo deviceInfo)
        => await SaveAsJson(implOptions.Value.DeviceInfoFile, deviceInfo);

    private BotDeviceInfo GenerateInfo()
    {
        var macAddress = new byte[6];
        _generator.NextBytes(macAddress);

        return new()
        {
            Guid = Guid.NewGuid(),
            MacAddress = macAddress,
            DeviceName = "linux-capture",
            SystemKernel = "Ubuntu 24.04.1 LTS",
            KernelVersion = "6.8.0-48-generic"
        };
    }

    public void Dispose()
    {
        _storageFile.Dispose();
        GC.SuppressFinalize(this);
    }
}
