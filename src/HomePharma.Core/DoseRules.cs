namespace HomePharma.Core;
public static class DoseRules
{
    public static DateTime? Next(Medication med, string time, DateTime now)
    {
        if (!med.Active || !med.Times.Contains(time) || Dates.Day(med.StartDate) is not { } start) return null;
        var today = DateOnly.FromDateTime(now);
        var days = Math.Max(0, today.DayNumber - start.DayNumber);
        var offset = (int)Math.Ceiling(days / (double)med.PeriodDays) * med.PeriodDays;
        var day = start.AddDays(offset);
        var result = day.ToDateTime(TimeOnly.ParseExact(time, "HH:mm", System.Globalization.CultureInfo.InvariantCulture));
        if (result <= now) { day = day.AddDays(med.PeriodDays); result = day.ToDateTime(TimeOnly.ParseExact(time, "HH:mm", System.Globalization.CultureInfo.InvariantCulture)); }
        return Dates.Day(med.EndDate) is { } end && day > end ? null : result;
    }
}
