using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using TelegramApp.Helpers;
using TelegramApp.Properties;
using TelegramApp.Services.Interfaces;
using TelegramApp.Services;

namespace TelegramApp
{
    public class Bot
    {
        private readonly IConfiguration _config;
        private readonly ICurrencyRateService _rateService;
        private readonly DateTime _startDate;
        private ConcurrentDictionary<long, UserState> _userStates;

        public Bot(IConfiguration config)
        {
            ArgumentNullException.ThrowIfNull(config);

            _config = config;
            _rateService = new CurrencyRateService();
            _startDate = DateTime.Parse(config["StartDate"]);
            _userStates = new();
        }

        public async Task StartAsync()
        {
            ITelegramBotClient bot = new TelegramBotClient(_config["BotToken"]);
            CancellationTokenSource cts = new();
            var cancellationToken = cts.Token;

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = { }
            };
            bot.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken
            );
            var me = await bot.GetMeAsync();
            Console.WriteLine($"Start listening for @{me.Username}");

            Console.ReadLine();
            cts.Cancel();
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.Message)
            {
                var message = update.Message;
                if (message.Text is not { } messageText)
                {
                    return;
                }
                var chatId = message.Chat.Id;

                DateTime inputDate;

                await Console.Out.WriteLineAsync($"Received a '{message.Text}' message in chat {chatId}.");

                if (message.Text.ToLower() == "/start")
                {
                    await botClient.SendTextMessageAsync(chatId: chatId,
                                                         text: Resources.WelcomeMessage + Resources.InputDate,
                                                         parseMode: ParseMode.Markdown);
                    return;
                }

                if (DateTime.TryParse(message.Text, out inputDate))
                {
                    if (inputDate > DateTime.Today || inputDate < _startDate)
                    {
                        await botClient.SendTextMessageAsync(chatId: message.Chat,
                                                             text: Resources.InvalidDate + Resources.InputDate,
                                                             parseMode: ParseMode.Markdown);
                        return;
                    }

                    await botClient.SendTextMessageAsync(chatId: chatId,
                                                         text: Resources.InputCurrencyCode,
                                                         parseMode: ParseMode.Markdown,
                                                         replyMarkup: new InlineKeyboardMarkup(
                                                             InlineKeyboardButton.WithUrl(text: Resources.CurrencyDictionary,
                                                                                          url: _config["CurrencyDictionary"])));

                    UserState newState = new()
                    {
                        Date = inputDate
                    };
                    _userStates[chatId] = newState;

                    return;
                }

                if (_userStates.TryGetValue(chatId, out var state))
                {
                    if (message.Text.ToLower() == Resources.Yes.ToLower())
                    {
                        state.CurrencyCode = string.Empty;
                        await botClient.SendTextMessageAsync(chatId: chatId,
                                                             text: Resources.InputCurrencyCode,
                                                             parseMode: ParseMode.Markdown,
                                                             replyMarkup: new InlineKeyboardMarkup(
                                                                                InlineKeyboardButton.WithUrl(text: Resources.CurrencyDictionary,
                                                                                                             url: _config["CurrencyDictionary"])));
                        return;
                    }

                    if (string.IsNullOrEmpty(state.CurrencyCode))
                    {
                        state.CurrencyCode = message.Text.ToUpper();

                        using (var httpClient = new HttpClient())
                        {
                            try
                            {
                                var result = await _rateService.GetRatesAsync(httpClient, state, _config["PrivatbankAPI"]);
                                if (result != null)
                                {
                                    var rate = result.ExchangeRate.FirstOrDefault(r => r.Currency == state.CurrencyCode);
                                    if (rate != null)
                                    {
                                        var rateString = string.Format(Resources.RateStringBase, rate.BaseCurrency, rate.Currency, result.Date);
                                        rateString += string.Format(Resources.Rates, rate.PurchaseRate, rate.SaleRate, rate.PurchaseRateNB, rate.SaleRateNB);

                                        await botClient.SendTextMessageAsync(message.Chat, rateString, ParseMode.Markdown);
                                    }
                                    else
                                    {
                                        await botClient.SendTextMessageAsync(message.Chat,
                                                                             string.Format(Resources.NotFoundRate, state.CurrencyCode, result.Date),
                                                                             ParseMode.Markdown);
                                    }

                                    ReplyKeyboardMarkup replyKeyboardMarkup = new(new[]
                                    {
                                        new KeyboardButton[] { Resources.Yes, Resources.No },
                                    })
                                    {
                                        ResizeKeyboard = true,
                                        OneTimeKeyboard = true
                                    };
                                    await botClient.SendTextMessageAsync(chatId: message.Chat,
                                                                         text: string.Format(Resources.AnotherRates, result.Date),
                                                                         parseMode: ParseMode.Markdown,
                                                                         replyMarkup: replyKeyboardMarkup);
                                }
                            }
                            catch (Exception ex)
                            {
                                await botClient.SendTextMessageAsync(message.Chat, Resources.ExceptionMessage);
                                await Console.Out.WriteLineAsync($"An error '{ex.Message}' occured in chat {chatId}.");

                                _userStates.Remove(chatId, out state);
                            }
                        }
                        return;
                    }
                }
                _userStates.Remove(chatId, out state);
                await botClient.SendTextMessageAsync(message.Chat, Resources.InputDate, ParseMode.Markdown);
            }
        }

        private async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine(JsonConvert.SerializeObject(exception));
        }

    }
}
