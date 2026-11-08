namespace HomePharma.Core;
public sealed record DueReminder(string Key, string Title, string Message);
public static class ReminderRules
{
    public static IEnumerable<DueReminder> Due(Medication med, DateOnly today)
    {
        if (med.ExpiryDate is { } expiry && today >= expiry.AddDays(-10) && today <= expiry)
            yield return new($"{med.Id}:expiry:{expiry:yyyy-MM-dd}", "Revisar caducidad", $"{med.Name} caduca el {expiry:dd/MM/yyyy}.");
        if (Dates.Day(med.PurchaseReminder) is { } purchase && today >= purchase.AddDays(-2) && today <= purchase)
            yield return new($"{med.Id}:purchase:{purchase:yyyy-MM-dd}", "Recordatorio de compra", $"Compra de {med.Name} prevista para el {purchase:dd/MM/yyyy}.");
    }
}

