using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace GenZStore.Services
{
    public class TelegramBotService : BackgroundService
    {
        private readonly TelegramBotClient _botClient;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TelegramBotService> _logger;

        public TelegramBotService(IServiceProvider serviceProvider, ILogger<TelegramBotService> logger, IConfiguration config)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            var token = config["Telegram:BotToken"];
            _botClient = new TelegramBotClient(token);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 Telegram Bot Started!");

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>() // Receive all update types
            };

            // FIX: In v22, 'pollingErrorHandler' is renamed to 'errorHandler'
            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandlePollingErrorAsync, // <--- Renamed parameter
                receiverOptions: receiverOptions,
                cancellationToken: stoppingToken
            );

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Message is not { } message) return;

            using var scope = _serviceProvider.CreateScope();
            var chatService = scope.ServiceProvider.GetRequiredService<ChatService>();
            var geminiService = scope.ServiceProvider.GetRequiredService<GeminiService>();

            try
            {
                string userQuestion = "";

                // 🎤 CASE 1: USER SENT VOICE
                if (message.Type == MessageType.Voice)
                {
                    // FIX: SendTextMessageAsync -> SendMessage
                    await botClient.SendMessage(message.Chat.Id, "👂 Listening to your beautiful Khmer voice...", cancellationToken: cancellationToken);

                    var fileId = message.Voice!.FileId;

                    // FIX: GetFileAsync -> GetFile
                    var fileInfo = await botClient.GetFile(fileId, cancellationToken);

                    using var memoryStream = new MemoryStream();

                    // FIX: DownloadFileAsync -> DownloadFile
                    await botClient.DownloadFile(fileInfo.FilePath!, memoryStream, cancellationToken);

                    var audioBytes = memoryStream.ToArray();

                    userQuestion = await geminiService.TranscribeAudioAsync(audioBytes);

                    // FIX: SendTextMessageAsync -> SendMessage
                    await botClient.SendMessage(message.Chat.Id, $"📝 You said: \"{userQuestion}\"", cancellationToken: cancellationToken);
                }
                // 📝 CASE 2: USER SENT TEXT
                else if (message.Type == MessageType.Text)
                {
                    userQuestion = message.Text!;
                }
                else
                {
                    return;
                }

                // 🤖 PROCESS
                // FIX: SendChatActionAsync -> SendChatAction
                await botClient.SendChatAction(message.Chat.Id, ChatAction.Typing, cancellationToken: cancellationToken);

                var answer = await chatService.GetBotResponseAsync(userQuestion);

                // FIX: SendTextMessageAsync -> SendMessage
                await botClient.SendMessage(message.Chat.Id, answer, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Telegram Error: {ex.Message}");
                // FIX: SendTextMessageAsync -> SendMessage
                await botClient.SendMessage(message.Chat.Id, "Oop! Something broke. 😭 Try again!", cancellationToken: cancellationToken);
            }
        }

        private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            _logger.LogError($"Telegram API Error: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}