using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Win11Optimization.Core.Interfaces;

namespace Win11Optimization.Services.System;

public sealed class StateStorageService : IStateStorage
{
    private readonly string _filePath;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public StateStorageService()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var folder = Path.Combine(localAppData, "Win11Optimizer");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "state.json");
    }

    public async Task SetStateAsync<T>(string optimizationId, string key, T value, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var data = await ReadDataAsync(ct);
            if (!data.ContainsKey(optimizationId))
            {
                data[optimizationId] = new Dictionary<string, JsonElement>();
            }

            data[optimizationId][key] = JsonSerializer.SerializeToElement(value);
            await WriteDataAsync(data, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<T?> GetStateAsync<T>(string optimizationId, string key, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var data = await ReadDataAsync(ct);
            if (data.TryGetValue(optimizationId, out var optData) && optData.TryGetValue(key, out var element))
            {
                return element.Deserialize<T>();
            }
            return default;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearStateAsync(string optimizationId, string key, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var data = await ReadDataAsync(ct);
            if (data.TryGetValue(optimizationId, out var optData))
            {
                optData.Remove(key);
                if (optData.Count == 0)
                {
                    data.Remove(optimizationId);
                }
                await WriteDataAsync(data, ct);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<Dictionary<string, Dictionary<string, JsonElement>>> ReadDataAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath)) return new();

        try
        {
            using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<Dictionary<string, Dictionary<string, JsonElement>>>(stream, cancellationToken: ct) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private async Task WriteDataAsync(Dictionary<string, Dictionary<string, JsonElement>> data, CancellationToken ct)
    {
        using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, data, cancellationToken: ct);
    }
}
