namespace TcfOss.DatabaseManager.Core.IO;

public static class ConsoleFormattingExtensions
{
    private const char Escape = (char)0x1B;

    // public static string Bold(this string text)
    // {
    //     return $"{Escape}[1m{text}{Escape}[22m";
    // }

    public static string Italic(this string text)
    {
        return $"{Escape}[3m{text}{Escape}[23m";
    }

    // public static string Underline(this string text)
    // {
    //     return $"{Escape}[4m{text}{Escape}[24m";
    // }

    // public static string Blinking(this string text)
    // {
    //     return $"{Escape}[5m{text}{Escape}[25m";
    // }

    // public static string Reverse(this string text)
    // {
    //     return $"{Escape}[7m{text}{Escape}[27m";
    // }

    // public static string Strikethrough(this string text)
    // {
    //     return $"{Escape}[9m{text}{Escape}[29m";
    // }

    // public static string Red(this string text)
    // {
    //     return $"{Escape}[31m{text}{Escape}[39m";
    // }

    // public static string RedBackground(this string text)
    // {
    //     return $"{Escape}[41m{text}{Escape}[49m";
    // }

    // public static string Green(this string text)
    // {
    //     return $"{Escape}[32m{text}{Escape}[39m";
    // }

    // public static string GreenBackground(this string text)
    // {
    //     return $"{Escape}[42m{text}{Escape}[49m";
    // }

    // public static string Blue(this string text)
    // {
    //     return $"{Escape}[34m{text}{Escape}[39m";
    // }

    // public static string BlueBackground(this string text)
    // {
    //     return $"{Escape}[44m{text}{Escape}[49m";
    // }

    // public static string Magenta(this string text)
    // {
    //     return $"{Escape}[35m{text}{Escape}[39m";
    // }

    // public static string MagentaBackground(this string text)
    // {
    //     return $"{Escape}[45m{text}{Escape}[49m";
    // }

    // public static string Cyan(this string text)
    // {
    //     return $"{Escape}[36m{text}{Escape}[39m";
    // }

    // public static string CyanBackground(this string text)
    // {
    //     return $"{Escape}[46m{text}{Escape}[49m";
    // }
}
