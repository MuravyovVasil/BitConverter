using Avalonia.Controls;
using Avalonia.Input;
using System.Linq;
using BitConverter.UI.ViewModels;
using Avalonia.Platform.Storage;

namespace BitConverter.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Реєстрація події Drop
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        // Використовуємо актуальний API DataTransfer, як ти і зазначав
        var files = e.DataTransfer.TryGetFiles();

        if (files is not null)
        {
            var firstFile = files.FirstOrDefault();

            if (firstFile is not null && DataContext is MainWindowViewModel vm)
            {
                // Отримуємо локальний шлях через IStorageItem.TryGetLocalPath()
                if (firstFile.TryGetLocalPath() is string localPath)
                {
                    await vm.ProcessDroppedFileAsync(localPath);
                }
            }
        }
    }
}