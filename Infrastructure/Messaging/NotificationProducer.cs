using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
namespace Hive_Movie.Infrastructure.Messaging;

public class NotificationProducer(IConfiguration configuration) : INotificationProducer
{
    private const string ExchangeName = "hive.notifications";
    private const string RoutingKey = "identity.email";

    private readonly static JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ConnectionFactory _connectionFactory = new()
    {
        HostName = configuration["RabbitMQ:Host"] ?? "localhost",
        Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
        UserName = configuration["RabbitMQ:Username"] ?? "publisher",
        Password = configuration["RabbitMQ:Password"] ?? "pub@2020",
        VirtualHost = configuration["RabbitMQ:VirtualHost"] ?? "/"
    };

    public async Task SendEmailNotificationAsync(EmailNotificationEvent emailEvent, Guid messageId)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Direct, false);

        var messageJson = JsonSerializer.Serialize(emailEvent, JsonOptions);
        var body = Encoding.UTF8.GetBytes(messageJson);

        var properties = new BasicProperties
        {
            ContentType = "application/json", MessageId = messageId.ToString() // Enables idempotency downstream
        };

        await channel.BasicPublishAsync(
            ExchangeName,
            RoutingKey,
            false,
            properties,
            body);
    }
}