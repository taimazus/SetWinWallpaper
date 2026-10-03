using System.Reflection;

namespace SahandNama;

public static class Brand
{
    public const string Name = "سهند نما";
    public const string EnglishName = "Sahand Nama";
    public const string Slogan = "هر روز یک افق تازه؛ مدیریت هوشمند پس‌زمینه و قفل صفحه";
    public const string Company = "شرکت راهکار الکترونیک سهند";
    public const string Website = "https://irres.ir";
    public static string Version => typeof(Brand).Assembly.GetName().Version?.ToString(3) ?? "1.8.1";
    public static string VersionLabel => "نسخه " + Version;
    public static string Title => $"{Name} | {EnglishName} — {Version}";
    public static string Credit => "طراحی و اجرا توسط " + Company;
}
