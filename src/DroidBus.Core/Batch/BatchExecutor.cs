using System.Collections.Concurrent;

namespace DroidBus.Core.Batch;

public sealed record BatchResult(
    IReadOnlyList<string> Succeeded,
    IReadOnlyDictionary<string, string> Failed);

public static class BatchExecutor
{
    public static async Task<BatchResult> RunAsync(
        IEnumerable<string> serials,
        Func<string, CancellationToken, Task> action,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        var ok = new ConcurrentBag<string>();
        var fail = new ConcurrentDictionary<string, string>();

        var tasks = serials.Select(async serial =>
        {
            try
            {
                await action(serial, ct);
                ok.Add(serial);
            }
            catch (Exception ex)
            {
                fail[serial] = ex.Message;
            }
            finally
            {
                onProgress?.Invoke(serial);
            }
        });

        await Task.WhenAll(tasks);
        return new BatchResult(ok.ToArray(), fail);
    }
}
