namespace Hive_Movie.Infrastructure.Messaging;

public record EmailNotificationEvent(
    string RecipientEmail,
    string Subject,
    string TemplateCode,
    Dictionary<string, string> Variables
);