using System.Text;

namespace DroidBus.Core.Batch;

public static class BatchReport
{
    public static string Summarize(BatchResult r)
    {
        var sb = new StringBuilder();
        sb.Append($"成功 {r.Succeeded.Count} 台,失败 {r.Failed.Count} 台。");
        if (r.Failed.Count > 0)
        {
            sb.Append("\n失败设备:");
            foreach (var kv in r.Failed)
                sb.Append($"\n  {kv.Key}: {kv.Value}");
        }
        return sb.ToString();
    }
}
