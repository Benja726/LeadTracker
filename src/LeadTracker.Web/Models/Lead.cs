namespace LeadTracker.Web.Models;

public enum Temperatura { Frio, Tibio, Caliente }

public record Message(string From, string Text, string Time);

public record Lead(
    int Id,
    string Name,
    string Phone,
    Temperatura Temp,
    string Intent,
    int DaysAgo,
    string Time,
    bool Unread,
    List<Message> Messages
);
