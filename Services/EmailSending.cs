using System.Net;
using System.Net.Mail;
using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace Entry.Services;

public sealed record EmailMessage(string To, string Subject, string Body);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>Sends through the SMTP server in the "Smtp" config section.</summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;

    public SmtpEmailSender(IOptions<SmtpOptions> options) => _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        using var mail = new MailMessage(_options.From!, message.To, message.Subject, message.Body);

        // SmtpClient is not thread-safe, so one per message.
        using var client = new SmtpClient(_options.Host!, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

        if (!string.IsNullOrWhiteSpace(_options.User))
            client.Credentials = new NetworkCredential(_options.User, _options.Password);

        await client.SendMailAsync(mail, cancellationToken);
    }
}

/// <summary>
/// Used when SMTP is not configured. In Development it logs the whole message,
/// reset link included, so the flow can be tested without a mail server.
/// Anywhere else it logs only that nothing was sent: a live reset link in
/// production logs would let anyone with log access take over the account.
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(IHostEnvironment environment, ILogger<LoggingEmailSender> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (_environment.IsDevelopment())
        {
            _logger.LogWarning(
                "SMTP is not configured; email NOT sent. To: {To}\nSubject: {Subject}\n{Body}",
                message.To, message.Subject, message.Body);
        }
        else
        {
            _logger.LogError(
                "SMTP is not configured; an email with subject {Subject} was not sent. Set the Smtp section.",
                message.Subject);
        }

        return Task.CompletedTask;
    }
}

public interface IEmailQueue
{
    /// <summary>Queues the message for background sending. False if the queue is full.</summary>
    bool TryEnqueue(EmailMessage message);
}

/// <summary>
/// Emails are sent off the request path. Besides keeping the response fast,
/// this keeps POST /api/auth/forgot-password taking about the same time
/// whether or not the address has an account; awaiting SMTP inline would make
/// the difference measurable and reveal which emails are registered.
/// </summary>
public sealed class ChannelEmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public ChannelReader<EmailMessage> Reader => _channel.Reader;

    public bool TryEnqueue(EmailMessage message) => _channel.Writer.TryWrite(message);
}

public sealed class EmailDispatchService : BackgroundService
{
    private readonly ChannelEmailQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailDispatchService> _logger;

    public EmailDispatchService(
        ChannelEmailQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<EmailDispatchService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                    await sender.SendAsync(message, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
                {
                    // One bad message (SMTP down, rejected address) must not
                    // stop the loop for everyone after it.
                    _logger.LogError(ex, "Failed to send email with subject {Subject}.", message.Subject);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }
}
