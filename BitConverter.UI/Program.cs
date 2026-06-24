using Avalonia;
using System;

namespace BitConverter.UI;

internal class Program
{
    // Стандартна точка входу для .NET додатків
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Метод, який використовує вашу існуючу конфігурацію App
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}