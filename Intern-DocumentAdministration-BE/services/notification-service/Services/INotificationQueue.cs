using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace NotificationService.Services
{
    public record NotificationTask(
        Guid? RecipientUserId,
        string RecipientEmail,
        string Subject,
        string Body,
        Guid? RelatedDocumentId = null,
        string NotificationType = "Info",
        string? ActionUrl = null
    );

    public interface INotificationQueue
    {
        ValueTask QueueNotificationAsync(NotificationTask task);
        ValueTask<NotificationTask> DequeueNotificationAsync(CancellationToken cancellationToken);
    }

    public class InMemoryNotificationQueue : INotificationQueue
    {
        private readonly Channel<NotificationTask> _channel;

        public InMemoryNotificationQueue()
        {
            var options = new BoundedChannelOptions(10000)
            {
                FullMode = BoundedChannelFullMode.Wait
            };
            _channel = Channel.CreateBounded<NotificationTask>(options);
        }

        public async ValueTask QueueNotificationAsync(NotificationTask task)
        {
            ArgumentNullException.ThrowIfNull(task);
            await _channel.Writer.WriteAsync(task);
        }

        public async ValueTask<NotificationTask> DequeueNotificationAsync(CancellationToken cancellationToken)
        {
            return await _channel.Reader.ReadAsync(cancellationToken);
        }
    }
}
