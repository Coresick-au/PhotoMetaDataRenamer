using System.Windows;
using PhotoRenamer.Services;

namespace PhotoRenamer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService;

    public MainWindow(SettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        LoadWindowPosition();
        Closing += MainWindow_Closing;
    }

    private void LoadWindowPosition()
    {
        var settings = _settingsService.Settings;
        
        // Validate position is on screen
        var left = settings.WindowLeft;
        var top = settings.WindowTop;
        var width = settings.WindowWidth;
        var height = settings.WindowHeight;
        
        // Ensure window is at least partially visible
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualWidth = SystemParameters.VirtualScreenWidth;
        var virtualHeight = SystemParameters.VirtualScreenHeight;
        
        if (left < virtualLeft) left = virtualLeft;
        if (top < virtualTop) top = virtualTop;
        if (left + width > virtualLeft + virtualWidth) left = virtualLeft + virtualWidth - width;
        if (top + height > virtualTop + virtualHeight) top = virtualTop + virtualHeight - height;
        
        Left = left;
        Top = top;
        Width = width;
        Height = height;
        
        if (settings.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Save position only if not minimized
        if (WindowState != WindowState.Minimized)
        {
            var isMaximized = WindowState == WindowState.Maximized;
            
            // If maximized, save the restore bounds
            var bounds = isMaximized ? RestoreBounds : new Rect(Left, Top, Width, Height);
            
            _settingsService.UpdateWindowBounds(
                bounds.Left,
                bounds.Top,
                bounds.Width,
                bounds.Height,
                isMaximized);
        }
    }
}