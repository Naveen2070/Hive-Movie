namespace Hive_Movie.Infrastructure.Messaging;

public interface INotificationProducer
{
    Task SendEmailNotificationAsync(EmailNotificationEvent emailEvent, Guid messageId);
}