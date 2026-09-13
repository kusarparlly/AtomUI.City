using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AtomUICityApplication;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
