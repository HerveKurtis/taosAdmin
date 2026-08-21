using AdminTaos.Models;

namespace AdminTaos.Services;

public static class ViewHelpers
{
    public static string Fmt(this TimeSpan? d) =>
        d is { } v ? $"{(int)v.TotalHours} h {v.Minutes:00}" : "—";
    // Surcharges non-nullables : C# ne convertit pas implicitement le receveur d'une
    // méthode d'extension vers son équivalent nullable.
    public static string Fmt(this TimeSpan d) => ((TimeSpan?)d).Fmt();
    public static string Fmt(this DateTime dt) => ((DateTime?)dt).Fmt();

    /// <summary>Une pause se lit en minutes tant qu'elle n'atteint pas l'heure.</summary>
    public static string FmtShort(this TimeSpan d) =>
        d.TotalMinutes < 60 ? $"{(int)d.TotalMinutes} min" : $"{(int)d.TotalHours} h {d.Minutes:00}";

    public static string Fmt(this DateOnly d) => d.ToString("ddd d MMM",
        new System.Globalization.CultureInfo("fr-FR"));
    public static string Fmt(this TimeOnly t) => t.ToString("HH:mm");
    public static string Fmt(this DateTime? dt) => dt?.ToString("HH:mm") ?? "—";

    public static string StatusFr(this TimesheetStatus s) => s switch
    {
        TimesheetStatus.NotStarted => "À démarrer",
        TimesheetStatus.InProgress => "En cours",
        TimesheetStatus.ToSend     => "À envoyer",
        TimesheetStatus.Sent       => "Envoyée",
        TimesheetStatus.Validated  => "Validée",
        TimesheetStatus.Rejected   => "Refusée",
        _ => s.ToString()
    };
    public static string PillClass(this TimesheetStatus s) => s switch
    {
        TimesheetStatus.Validated => "pill ok",
        TimesheetStatus.Rejected  => "pill bad",
        TimesheetStatus.Sent      => "pill",
        _ => "pill wait"
    };
}
