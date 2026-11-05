namespace App.Consoler;

/// <summary>命令行参数 → Job 参数的 AsyncLocal 传递容器</summary>
public static class JobExtras
{
    public static readonly AsyncLocal<Dictionary<string, string>?> _current = new();

    public static Dictionary<string, string>? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}