using Microsoft.Extensions.Configuration;
using Moq;
using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramApp;
using TelegramApp.Helpers;
using TelegramApp.Models;
using TelegramApp.Properties;
using TelegramApp.Services.Interfaces;
using TelegramApp.Services;

namespace TelegramAppTests
{
    public class BotTests
    {
        CancellationTokenSource cts = new();
        StubTelegramBotClient stabTelegramBotClient = new StubTelegramBotClient();
        DateTime startDate;
        ConcurrentDictionary<long, UserState> userStates = new();
        long chatId = 123;
        BankResponse expectedBankResponse = new()
        {
            Date = DateTime.Now.ToString("dd.MM.yyyy"),
            ExchangeRate = new CurrencyRate[]
                {
                    new CurrencyRate()
                        {
                            Currency = "USD",
                            BaseCurrency = "UAH",
                            SaleRateNB = "36.5686000",
                            PurchaseRateNB = "36.5686000",
                            SaleRate = "38.4000000",
                            PurchaseRate = "37.9000000"
                        }
                }
        };

        [Fact]
        public async Task HandleUpdateAsync_StartCommand_ProperResponse()
        {
            var cancellationToken = cts.Token;
            var mockConfiguration = new Mock<IConfiguration>();
            mockConfiguration.Setup(c => c["StartDate"]).Returns("01.12.2014");

            Bot bot = new(mockConfiguration.Object, stabTelegramBotClient, new CurrencyRateService());
            var message = new Message
            {
                Chat = new Chat { Id = chatId },
                Text = "/start",
                From = new User { Id = 456 }
            };
            string expectedResult = Resources.WelcomeMessage + Resources.InputDate;

            await bot.HandleUpdateAsync(stabTelegramBotClient, new Update { Message = message }, cancellationToken);
            string actualResult = stabTelegramBotClient.LastMessageText;

            Assert.Equal(expectedResult, actualResult);
        }

        [Fact]
        public async Task HandleUpdateAsync_InconsiderableMessage_ProperResponseAndEmptyDictionary()
        {
            var cancellationToken = cts.Token;
            var mockConfiguration = new Mock<IConfiguration>();
            mockConfiguration.Setup(c => c["StartDate"]).Returns("01.12.2014");

            Bot bot = new(mockConfiguration.Object, stabTelegramBotClient, new CurrencyRateService());
            var message = new Message
            {
                Chat = new Chat { Id = chatId },
                Text = "whateverInconsiderable",
                From = new User { Id = 456 }
            };
            string expectedResult = Resources.InputDate;

            await bot.HandleUpdateAsync(stabTelegramBotClient, new Update { Message = message }, cancellationToken);
            string actualResult = stabTelegramBotClient.LastMessageText;

            Assert.Equal(expectedResult, actualResult);
            Assert.Empty(bot.UserStates);
        }

        [Fact]
        public async Task HandleUpdateAsync_InvalidEarlierDate_ProperResponse()
        {
            var cancellationToken = cts.Token;
            var mockConfiguration = new Mock<IConfiguration>();
            mockConfiguration.Setup(c => c["StartDate"]).Returns("01.12.2014");

            Bot bot = new(mockConfiguration.Object, stabTelegramBotClient, new CurrencyRateService());
            DateTime.TryParse(mockConfiguration.Object["StartDate"], out startDate);
            string earlierDate = (startDate.AddDays(-1)).ToString("dd.MM.yy");
            var message = new Message
            {
                Chat = new Chat { Id = chatId },
                Text = earlierDate,
                From = new User { Id = 456 }
            };
            string expectedResult = Resources.InvalidDate + Resources.InputDate;

            await bot.HandleUpdateAsync(stabTelegramBotClient, new Update { Message = message }, cancellationToken);
            string actualResult = stabTelegramBotClient.LastMessageText;

            Assert.Equal(expectedResult, actualResult);
        }

        [Fact]
        public async Task HandleUpdateAsync_InvalidLaterDate_ProperResponse()
        {
            var cancellationToken = cts.Token;
            var mockConfiguration = new Mock<IConfiguration>();
            mockConfiguration.Setup(c => c["StartDate"]).Returns("01.12.2014");

            Bot bot = new(mockConfiguration.Object, stabTelegramBotClient, new CurrencyRateService());
            DateTime.TryParse(mockConfiguration.Object["StartDate"], out startDate);
            string laterDate = (DateTime.Now.AddDays(1)).ToString("dd.MM.yy");
            var message = new Message
            {
                Chat = new Chat { Id = chatId },
                Text = laterDate,
                From = new User { Id = 456 }
            };
            string expectedResult = Resources.InvalidDate + Resources.InputDate;

            await bot.HandleUpdateAsync(stabTelegramBotClient, new Update { Message = message }, cancellationToken);
            string actualResult = stabTelegramBotClient.LastMessageText;

            Assert.Equal(expectedResult, actualResult);
        }

        [Fact]
        public async Task HandleUpdateAsync_ValidDate_ProperResponseAndUserStateAddedToDictionary()
        {
            var cancellationToken = cts.Token;
            var mockConfiguration = new Mock<IConfiguration>();
            mockConfiguration.Setup(c => c["StartDate"]).Returns("01.12.2014");

            Bot bot = new(mockConfiguration.Object, stabTelegramBotClient, new CurrencyRateService());
            DateTime.TryParse(mockConfiguration.Object["StartDate"], out startDate);
            string currentDate = DateTime.Now.ToString("dd.MM.yy");

            var message = new Message
            {
                Chat = new Chat { Id = chatId },
                Text = currentDate,
                From = new User { Id = 456 }
            };
            string expectedResult = Resources.InputCurrencyCode;
            UserState expectedUserState = new()
            {
                Date = DateTime.Parse(currentDate)
            };
            userStates[chatId] = expectedUserState;

            await bot.HandleUpdateAsync(stabTelegramBotClient, new Update { Message = message }, cancellationToken);
            string actualResult = stabTelegramBotClient.LastMessageText;

            Assert.Equal(expectedResult, actualResult);
            Assert.Equal(userStates.Keys, bot.UserStates.Keys);
            Assert.Equal(userStates.Values, bot.UserStates.Values);
        }

        [Fact]
        public async Task HandleUpdateAsync_ValidCurrencyCodeAfterValidDate_ProperResponse()
        {
            var cancellationToken = cts.Token;
            var mockConfiguration = new Mock<IConfiguration>();
            mockConfiguration.Setup(c => c["StartDate"]).Returns("01.12.2014");
            var mockCurrencyRateService = new Mock<ICurrencyRateService>();
            mockCurrencyRateService.Setup(s => s.GetRatesAsync(It.IsAny<HttpClient>(), It.IsAny<UserState>(), It.IsAny<string>()))
                                   .ReturnsAsync(expectedBankResponse);

            Bot bot = new(mockConfiguration.Object, stabTelegramBotClient, mockCurrencyRateService.Object);
            DateTime.TryParse(mockConfiguration.Object["StartDate"], out startDate);
            string currentDate = DateTime.Now.ToString("dd.MM.yy");
            var message = new Message
            {
                Chat = new Chat { Id = chatId },
                Text = currentDate,
                From = new User { Id = 456 }
            };
            var expectedRateString = string.Format(Resources.RateStringBase, expectedBankResponse.ExchangeRate[0].BaseCurrency, expectedBankResponse.ExchangeRate[0].Currency, expectedBankResponse.Date);
            expectedRateString += string.Format(Resources.Rates, expectedBankResponse.ExchangeRate[0].PurchaseRate, expectedBankResponse.ExchangeRate[0].SaleRate, expectedBankResponse.ExchangeRate[0].PurchaseRateNB, expectedBankResponse.ExchangeRate[0].SaleRateNB);
            string expectedMessage = string.Format(Resources.AnotherRates, expectedBankResponse.Date);


            await bot.HandleUpdateAsync(stabTelegramBotClient, new Update { Message = message }, cancellationToken);
            message = new Message
            {
                Chat = new Chat { Id = chatId },
                Text = "usd",
                From = new User { Id = 456 }
            };
            await bot.HandleUpdateAsync(stabTelegramBotClient, new Update { Message = message }, cancellationToken);
            string actualRateString = bot.RateString;
            string actualMessage = stabTelegramBotClient.LastMessageText;

            Assert.Equal(expectedRateString, actualRateString);
            Assert.Equal(expectedMessage, actualMessage);
        }

        [Fact]
        public async Task HandleUpdateAsync_InvalidCurrencyCodeAfterValidDate_ProperResponse()
        {
            var cancellationToken = cts.Token;
            var mockConfiguration = new Mock<IConfiguration>();
            mockConfiguration.Setup(c => c["StartDate"]).Returns("01.12.2014");
            var mockCurrencyRateService = new Mock<ICurrencyRateService>();
            mockCurrencyRateService.Setup(s => s.GetRatesAsync(It.IsAny<HttpClient>(), It.IsAny<UserState>(), It.IsAny<string>()))
                                   .ReturnsAsync(expectedBankResponse);

            Bot bot = new(mockConfiguration.Object, stabTelegramBotClient, mockCurrencyRateService.Object);
            DateTime.TryParse(mockConfiguration.Object["StartDate"], out startDate);
            string currentDate = DateTime.Now.ToString("dd.MM.yy");
            var message = new Message
            {
                Chat = new Chat { Id = chatId },
                Text = currentDate,
                From = new User { Id = 456 }
            };
            string wrongCode = "wrongCode";
            var expectedResultString = string.Format(Resources.NotFoundRate, wrongCode.ToUpper(), expectedBankResponse.Date);
            string expectedMessage = string.Format(Resources.AnotherRates, expectedBankResponse.Date);


            await bot.HandleUpdateAsync(stabTelegramBotClient, new Update { Message = message }, cancellationToken);
            message = new Message
            {
                Chat = new Chat { Id = chatId },
                Text = wrongCode,
                From = new User { Id = 456 }
            };
            await bot.HandleUpdateAsync(stabTelegramBotClient, new Update { Message = message }, cancellationToken);
            string actualResultString = bot.RateString;
            string actualMessage = stabTelegramBotClient.LastMessageText;

            Assert.Equal(expectedResultString, actualResultString);
            Assert.Equal(expectedMessage, actualMessage);
        }

        [Fact]
        public async Task HandleUpdateAsync_CurrencyRateServiceReturnsException_ProperResponseAndEmptyDictionary()
        {
            var cancellationToken = cts.Token;
            var mockConfiguration = new Mock<IConfiguration>();
            mockConfiguration.Setup(c => c["StartDate"]).Returns("01.12.2014");
            var mockCurrencyRateService = new Mock<ICurrencyRateService>();
            mockCurrencyRateService.Setup(s => s.GetRatesAsync(It.IsAny<HttpClient>(), It.IsAny<UserState>(), It.IsAny<string>()))
                                   .ReturnsAsync(() => throw new Exception());

            Bot bot = new(mockConfiguration.Object, stabTelegramBotClient, mockCurrencyRateService.Object);
            DateTime.TryParse(mockConfiguration.Object["StartDate"], out startDate);
            string currentDate = DateTime.Now.ToString("dd.MM.yy");
            var message = new Message
            {
                Chat = new Chat { Id = chatId },
                Text = currentDate,
                From = new User { Id = 456 }
            };
            string expectedMessage = Resources.ExceptionMessage;


            await bot.HandleUpdateAsync(stabTelegramBotClient, new Update { Message = message }, cancellationToken);
            message = new Message
            {
                Chat = new Chat { Id = chatId },
                Text = "usd",
                From = new User { Id = 456 }
            };
            await bot.HandleUpdateAsync(stabTelegramBotClient, new Update { Message = message }, cancellationToken);
            string actualMessage = stabTelegramBotClient.LastMessageText;

            Assert.Equal(expectedMessage, actualMessage);
            Assert.Empty(bot.UserStates);
        }
    }
}

class StubTelegramBotClient : ITelegramBotClient
{
    public bool LocalBotServer => throw new NotImplementedException();

    public long? BotId => throw new NotImplementedException();

    public TimeSpan Timeout { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public IExceptionParser ExceptionsParser { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public event AsyncEventHandler<ApiRequestEventArgs>? OnMakingApiRequest;
    public event AsyncEventHandler<ApiResponseEventArgs>? OnApiResponseReceived;

    public string LastMessageText { get; private set; }

    public Task<Message> SendTextMessageAsync(long chatId, string text, ParseMode parseMode = ParseMode.Markdown,
        bool disableWebPagePreview = false, bool disableNotification = false, int replyToMessageId = 0,
        bool allowSendingWithoutReply = false, int? replyMarkup = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        // Do nothing
    }

    public Task<User> GetMeAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task SetWebhookAsync(string url, InputFileStream certificate = null, int maxConnections = 40, IEnumerable<UpdateType> allowedUpdates = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task DeleteWebhookAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<WebhookInfo> GetWebhookInfoAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<Telegram.Bot.Types.File> GetInfoAndDownloadFileAsync(string fileId, string destinationFilePath, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<Telegram.Bot.Types.File> GetFileAsync(string fileId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<Message> SendPhotoAsync(long chatId, InputOnlineFile photo, string caption = null, ParseMode parseMode = ParseMode.Markdown, bool disableNotification = false, int replyToMessageId = 0, IReplyMarkup replyMarkup = null)
    {
        throw new NotImplementedException();
    }

    public Task<TResponse> MakeRequestAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request is SendMessageRequest sendMessageRequest)
        {
            var message = new Message
            {
                Chat = new Chat { Id = (long)sendMessageRequest.ChatId.Identifier },
                Text = sendMessageRequest.Text
            };

            LastMessageText = message.Text;
            return Task.FromResult((TResponse)(object)message);
        }

        throw new NotImplementedException();
    }

    public Task<bool> TestApiAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task DownloadFileAsync(string filePath, Stream destination, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
