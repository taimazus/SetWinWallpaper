using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BingWallpaperPro;

public sealed class ThumbnailConverter : IValueConverter
{
    static readonly ConcurrentDictionary<string, BitmapImage> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static void ClearCache() => Cache.Clear();

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            if (Cache.Count > 400) Cache.Clear();
            return Cache.GetOrAdd(path, p => Store.LoadImage(p, 260));
        }
        catch { return null; }
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public static class AppDialog
{
    public static bool Show(Window owner, string message, string title, bool confirm = false)
    {
        var dialog = new Window
        {
            Owner = owner, Title = Brand.Name + " — " + title, Width = 610, SizeToContent = SizeToContent.Height,
            MaxHeight = Math.Max(350, SystemParameters.WorkArea.Height - 80), ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, FlowDirection = FlowDirection.RightToLeft,
            FontFamily = owner.FontFamily, FontSize = 14, Background = new SolidColorBrush(Color.FromRgb(12, 26, 38)),
            Foreground = Brushes.White, ShowInTaskbar = false, Icon = owner.Icon
        };
        dialog.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/BingWallpaperPro;component/Theme.xaml", UriKind.Relative) });
        var panel = new StackPanel { Margin = new Thickness(26) };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 23, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 14) });
        panel.Children.Add(new TextBox { Text = message, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = Math.Min(400, SystemParameters.WorkArea.Height - 240), BorderThickness = new Thickness(0) });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 0) };
        var ok = new Button { Content = confirm ? "تأیید و ادامه" : "متوجه شدم", IsDefault = !confirm, MinWidth = 125 };
        ok.Click += (_, _) => dialog.DialogResult = true; buttons.Children.Add(ok);
        if (confirm)
        {
            var cancel = new Button { Content = "انصراف", IsCancel = true, IsDefault = true, MinWidth = 95 };
            cancel.Click += (_, _) => dialog.DialogResult = false; buttons.Children.Add(cancel);
        }
        panel.Children.Add(buttons); dialog.Content = panel; return dialog.ShowDialog() == true;
    }
}
