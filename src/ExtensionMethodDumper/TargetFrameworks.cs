using System.Collections.Frozen;

namespace ExtensionMethodDumper;

internal static class TargetFrameworks
{
    public static readonly FrozenSet<string> Known = new HashSet<string>(StringComparer.Ordinal)
    {
        "net462",
        "net47",
        "net472",
        "net6.0",
        "net8.0",
        "net8.0-browser",
        "net8.0-unix",
        "net8.0-windows",
        "net9.0",
        "net9.0-android",
        "net9.0-browser",
        "net9.0-freebsd",
        "net9.0-haiku",
        "net9.0-illumos",
        "net9.0-ios",
        "net9.0-linux",
        "net9.0-maccatalyst",
        "net9.0-osx",
        "net9.0-solaris",
        "net9.0-tvos",
        "net9.0-unix",
        "net9.0-wasi",
        "net9.0-windows",
        "netcoreapp2.1",
        "netstandard2.0",
        "netstandard2.1",
    }.ToFrozenSet();
}
