using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace BingWallpaperPro;

public sealed class WeatherInfo
{
    public string City { get; set; } = "تهران";
    public double TemperatureC { get; set; }
    public string ConditionText { get; set; } = "در حال دریافت…";
    public string ConditionIcon { get; set; } = "🌤️";
    public int RelativeHumidity { get; set; }
    public double WindSpeedKmh { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.MinValue;
    public bool IsSuccess { get; set; }

    public string FormattedTemperature => $"{Math.Round(TemperatureC, 1)}°C";
    public string SummaryBadge => $"{ConditionIcon} {City}: {FormattedTemperature} ({ConditionText})";
    public string DetailedTooltip => $"شهر: {City}\nدما: {FormattedTemperature}\nوضعیت: {ConditionText}\nرطوبت: {RelativeHumidity}%\nسرعت باد: {WindSpeedKmh} کیلومتر/ساعت\nبه‌روزرسانی: {LastUpdated:HH:mm:ss}";
}

public static class WeatherService
{
    public sealed record CityLocation(string PersianName, string EnglishName, double Latitude, double Longitude);

    public static readonly IReadOnlyList<CityLocation> Cities = new List<CityLocation>
    {
        new("تهران", "Tehran", 35.6892, 51.3890),
        new("مشهد", "Mashhad", 36.2972, 59.6067),
        new("اصفهان", "Isfahan", 32.6546, 51.6680),
        new("تبریز", "Tabriz", 38.0800, 46.2919),
        new("شیراز", "Shiraz", 29.5918, 52.5837),
        new("اهواز", "Ahvaz", 31.3183, 48.6706),
        new("رشت", "Rasht", 37.2808, 49.5832),
        new("کرمانشاه", "Kermanshah", 34.3277, 47.0778),
        new("یزد", "Yazd", 31.8974, 54.3569),
        new("ارومیه", "Urmia", 37.5527, 45.0761),
        new("کرج", "Karaj", 35.8400, 50.9391),
        new("قم", "Qom", 34.6401, 50.8764),
        new("کیش", "Kish Island", 26.5578, 53.9890),
        new("قزوین", "Qazvin", 36.2797, 50.0049),
        new("زنجان", "Zanjan", 36.6736, 48.4787),
        new("سنندج", "Sanandaj", 35.3144, 46.9961),
        new("گرگان", "Gorgan", 36.8456, 54.4393),
        new("ساری", "Sari", 36.5659, 53.0586),
        new("همدان", "Hamedan", 34.7983, 48.5146),
        new("خرم‌آباد", "Khorramabad", 33.4878, 48.3558),
        new("بوشهر", "Bushehr", 28.9234, 50.8203),
        new("بندرعباس", "Bandar Abbas", 27.1832, 56.2666),
        new("زاهدان", "Zahedan", 29.4963, 60.8629),
        new("کرمان", "Kerman", 30.2839, 57.0788),
        new("بیرجند", "Birjand", 32.8663, 59.2211),
        new("بجنورد", "Bojnord", 37.4761, 57.3283),
        new("سمنان", "Semnan", 35.5769, 53.3970),
        new("یاسوج", "Yasuj", 30.6684, 51.5876),
        new("ایلام", "Ilam", 33.6374, 46.4227),
        new("شهرکرد", "Shahrekord", 32.3256, 50.8644),
        new("اردبیل", "Ardabil", 38.2498, 48.2933),
        new("لندن", "London", 51.5074, -0.1278),
        new("پاریس", "Paris", 48.8566, 2.3522),
        new("استانبول", "Istanbul", 41.0082, 28.9784),
        new("دبی", "Dubai", 25.2048, 55.2708),
        new("توکیو", "Tokyo", 35.6762, 139.6503),
        new("نیویورک", "New York", 40.7128, -74.0060)
    };

    static WeatherInfo? cachedWeather;
    static string? cachedCity;
    static DateTime lastFetchTime = DateTime.MinValue;
    static readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static CityLocation FindCity(string cityName)
    {
        if (string.IsNullOrWhiteSpace(cityName)) return Cities[0];
        var found = Cities.FirstOrDefault(c =>
            string.Equals(c.PersianName, cityName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c.EnglishName, cityName, StringComparison.OrdinalIgnoreCase) ||
            cityName.Contains(c.PersianName, StringComparison.OrdinalIgnoreCase) ||
            cityName.Contains(c.EnglishName, StringComparison.OrdinalIgnoreCase));

        return found ?? Cities[0];
    }

    public static async Task<WeatherInfo> GetWeatherAsync(string cityName, bool forceRefresh = false)
    {
        var city = FindCity(cityName);
        if (!forceRefresh && cachedWeather != null && cachedCity == city.PersianName && (DateTime.UtcNow - lastFetchTime).TotalMinutes < 15)
        {
            return cachedWeather;
        }

        var info = new WeatherInfo { City = city.PersianName };

        try
        {
            var url = string.Format(CultureInfo.InvariantCulture,
                "https://api.open-meteo.com/v1/forecast?latitude={0:F4}&longitude={1:F4}&current=temperature_2m,relative_humidity_2m,weather_code,wind_speed_10m&timezone=auto",
                city.Latitude, city.Longitude);

            using var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("current", out var current))
            {
                if (current.TryGetProperty("temperature_2m", out var temp))
                    info.TemperatureC = temp.GetDouble();

                if (current.TryGetProperty("relative_humidity_2m", out var hum))
                    info.RelativeHumidity = hum.GetInt32();

                if (current.TryGetProperty("wind_speed_10m", out var wind))
                    info.WindSpeedKmh = wind.GetDouble();

                if (current.TryGetProperty("weather_code", out var codeProp))
                {
                    var code = codeProp.GetInt32();
                    var (text, icon) = MapWeatherCode(code);
                    info.ConditionText = text;
                    info.ConditionIcon = icon;
                }

                info.LastUpdated = DateTime.Now;
                info.IsSuccess = true;
                cachedWeather = info;
                cachedCity = city.PersianName;
                lastFetchTime = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            info.ConditionText = "آفلاین / بدون پاسخ";
            info.ConditionIcon = "☁️";
            info.IsSuccess = false;
            Store.Log($"Weather fetch error ({city.PersianName}): {ex.Message}");
        }

        return info;
    }

    public static (string Text, string Icon) MapWeatherCode(int code) => code switch
    {
        0 => ("صاف و آفتابی", "☀️"),
        1 => ("غالباً صاف", "🌤️"),
        2 => ("نیمه ابری", "⛅"),
        3 => ("ابری", "☁️"),
        45 or 48 => ("مه‌آلود", "🌫️"),
        51 or 53 or 55 => ("نم‌نم باران", "🌦️"),
        56 or 57 => ("باران یخی", "🌧️"),
        61 or 63 => ("بارانی", "🌧️"),
        65 => ("باران شدید", "🌧️"),
        66 or 67 => ("باران و تگرگ", "🌨️"),
        71 or 73 or 75 => ("برف", "❄️"),
        77 => ("دانه‌های برف", "🌨️"),
        80 or 81 or 82 => ("رگبار باران", "🌧️"),
        85 or 86 => ("رگبار برف", "🌨️"),
        95 => ("رعد و برق", "⛈️"),
        96 or 99 => ("رعد و برق و تگرگ", "⛈️"),
        _ => ("معتدل", "🌤️")
    };
}
