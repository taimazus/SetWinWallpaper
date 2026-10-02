using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SahandNama;

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
        dialog.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/SahandNama;component/Theme.xaml", UriKind.Absolute) });
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

public static class PersianDateHelper
{
    static readonly PersianCalendar Pc = new();
    static readonly string[] MonthNames = ["فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"];
    static readonly string[] DayNames = ["یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنج‌شنبه", "جمعه", "شنبه"];

    public static string GetFormattedPersianDate(DateTime dt)
    {
        var year = Pc.GetYear(dt);
        var month = Pc.GetMonth(dt);
        var day = Pc.GetDayOfMonth(dt);
        var dayOfWeek = DayNames[(int)dt.DayOfWeek];
        return $"{dayOfWeek}، {day} {MonthNames[month - 1]} {year}";
    }
}

public static class CardGenerator
{
    public static string GenerateShareableCard(Photo photo, string? destinationPath = null)
    {
        var root = Store.Root;
        destinationPath ??= Path.Combine(root, "Cards", $"card-{photo.Id}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        const int cardWidth = 1080;
        const int cardHeight = 1350;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // Background base
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(10, 20, 30)), null, new Rect(0, 0, cardWidth, cardHeight));

            // Load and draw photo
            if (File.Exists(photo.FilePath))
            {
                try
                {
                    var img = Store.LoadImage(photo.FilePath, 1080);
                    // Draw centered / uniform to fill
                    double imgAspect = (double)img.PixelWidth / img.PixelHeight;
                    double targetAspect = (double)cardWidth / cardHeight;
                    double drawW = cardWidth;
                    double drawH = cardHeight;
                    double drawX = 0;
                    double drawY = 0;

                    if (imgAspect > targetAspect)
                    {
                        drawW = cardHeight * imgAspect;
                        drawX = (cardWidth - drawW) / 2;
                    }
                    else
                    {
                        drawH = cardWidth / imgAspect;
                        drawY = (cardHeight - drawH) / 2;
                    }

                    dc.DrawImage(img, new Rect(drawX, drawY, drawW, drawH));
                }
                catch { }
            }

            // Dark gradient overlay at bottom
            var gradient = new LinearGradientBrush(
                Color.FromArgb(0, 10, 20, 30),
                Color.FromArgb(235, 8, 16, 25),
                new Point(0.5, 0.45),
                new Point(0.5, 1.0));
            dc.DrawRectangle(gradient, null, new Rect(0, 0, cardWidth, cardHeight));

            // Top subtle header bar
            var topGradient = new LinearGradientBrush(
                Color.FromArgb(200, 8, 16, 25),
                Color.FromArgb(0, 8, 16, 25),
                new Point(0.5, 0.0),
                new Point(0.5, 0.2));
            dc.DrawRectangle(topGradient, null, new Rect(0, 0, cardWidth, 260));

            // Brand pill badge at top right
            var badgeBrush = new SolidColorBrush(Color.FromArgb(220, 18, 94, 83));
            dc.DrawRoundedRectangle(badgeBrush, null, new Rect(cardWidth - 320, 48, 260, 52), 26, 26);

            var typeface = new Typeface(new FontFamily("Segoe UI, Tahoma"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            var titleTypeface = new Typeface(new FontFamily("Segoe UI, Tahoma"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            var normalTypeface = new Typeface(new FontFamily("Segoe UI, Tahoma"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

            var brandText = new FormattedText("سهند نما • والپیپر برگزیده",
                CultureInfo.CurrentCulture,
                FlowDirection.RightToLeft,
                typeface, 18, Brushes.White, 1.0);
            dc.DrawText(brandText, new Point(cardWidth - 80, 60));

            // Persian Date at top left
            var dateStr = PersianDateHelper.GetFormattedPersianDate(DateTime.Now);
            var dateText = new FormattedText(dateStr,
                CultureInfo.CurrentCulture,
                FlowDirection.RightToLeft,
                normalTypeface, 18, new SolidColorBrush(Color.FromRgb(200, 225, 235)), 1.0);
            dc.DrawText(dateText, new Point(320, 60));

            // Bottom texts (Title + Credit + App name)
            var title = string.IsNullOrWhiteSpace(photo.Title) ? "منظره طبیعی و چشم‌انداز روز" : photo.Title;
            var formattedTitle = new FormattedText(title,
                CultureInfo.CurrentCulture,
                FlowDirection.RightToLeft,
                titleTypeface, 34, Brushes.White, 1.0)
            {
                MaxTextWidth = cardWidth - 120,
                MaxTextHeight = 160
            };
            dc.DrawText(formattedTitle, new Point(cardWidth - 60, cardHeight - 280));

            var credit = string.IsNullOrWhiteSpace(photo.Copyright) ? $"{photo.Source} • {photo.Market}" : photo.Copyright;
            var formattedCredit = new FormattedText(credit,
                CultureInfo.CurrentCulture,
                FlowDirection.RightToLeft,
                normalTypeface, 20, new SolidColorBrush(Color.FromRgb(165, 195, 215)), 1.0)
            {
                MaxTextWidth = cardWidth - 120,
                MaxTextHeight = 80
            };
            dc.DrawText(formattedCredit, new Point(cardWidth - 60, cardHeight - 160));

            // Footer branding
            var footerText = new FormattedText("Sahand Nama — مدیریت و تغییر خودکار والپیپر ویندوز",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                normalTypeface, 16, new SolidColorBrush(Color.FromRgb(85, 185, 165)), 1.0);
            dc.DrawText(footerText, new Point(60, cardHeight - 65));
        }

        var rtb = new RenderTargetBitmap(cardWidth, cardHeight, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using (var stream = File.Create(destinationPath))
        {
            encoder.Save(stream);
        }

        Store.Log($"Shareable social card created: {destinationPath}");
        return destinationPath;
    }
}

