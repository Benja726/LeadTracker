namespace LeadTracker.Web.Models;

public enum Temperatura { Frio, Tibio, Caliente }

public record Message(string From, string Text, string Time);

public record Lead(
    Guid Id,
    string Name,
    string Phone,
    Temperatura Temp,
    string Intent,
    int DaysAgo,
    string Time,
    bool Unread,
    List<Message> Messages
)
{
    // Mutable so the bot toggle can update optimistically without rebuilding the list.
    public bool BotEnabled { get; set; } = true;
    public string? BotDisabledReason { get; set; }
    public DateTime? BotDisabledAt { get; set; }
    public bool ReadyForHandoff { get; set; }
}
