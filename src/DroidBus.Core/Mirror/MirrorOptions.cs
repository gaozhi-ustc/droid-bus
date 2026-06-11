namespace DroidBus.Core.Mirror;

public sealed record MirrorOptions
{
    public int BitRateMbps { get; set; } = 4;
    public int MaxSize { get; set; } = 1080;
    public bool TurnScreenOff { get; set; }
    public bool StayAwake { get; set; }
    public bool ShowTouches { get; set; }
    public int? LockOrientation { get; set; }   // 0/1/2/3 = 旋转锁定;null=不锁
    public bool Record { get; set; }
    public string RecordDir { get; set; } = "";
    public bool NoAudio { get; set; } = true;    // A10 默认关音频(走 sndcpy)

    /// SDL 渲染驱动(scrcpy --render-driver)。默认 software:整合显卡 + 远程/虚拟显示器
    /// (本机检出 Oray/Todesk 虚拟显示器)下,6 路并发 Direct3D 抢不到 GPU 渲染资源会黑屏;
    /// software 走 CPU 位图呈现,无 GPU 共享上限,且只影响呈现不影响解码,开销极小。
    /// 置空则用 scrcpy 默认(direct3d)。可选 opengl 等。
    public string RenderDriver { get; set; } = "software";
}
