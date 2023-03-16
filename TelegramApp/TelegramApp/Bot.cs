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

namespace TelegramApp
{
    public class Bot
    {
        private readonly IConfiguration _config;
        private readonly ICurrencyRateService _rateService;
        private readonly DateTime _startDate;
        private ConcurrentDictionary<long, UserState> _userStates;
        private readonly ITelegramBotClient _bot;
        private string _rateString;

        public ConcurrentDictionary<long, UserState> UserStates => _userStates;
        public string RateString => _rateString;

        public Bot(IConfiguration config, ITelegramBotClient bot, ICurrencyRateService rateService)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(bot);
            ArgumentNullException.ThrowIfNull(rateService);

            _config = config;
            _bot = bot;
            _rateService = rateService;
            _startDate = DateTime.Parse(config["StartDate"]);
            _userStates = new();
        }

        public async Task StartAsync()
        {
            CancellationTokenSource cts = new();
            var cancellationToken = cts.Token;

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = { }
            };
            _bot.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken
            );
            var me = await _bot.GetMeAsync();
            Console.WriteLine($"Start listening for @{me.Username}");

            Console.ReadLine();
            cts.Cancel();
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
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
                                        _rateString = string.Format(Resources.RateStringBase, rate.BaseCurrency, rate.Currency, result.Date);
                                        _rateString += string.Format(Resources.Rates, rate.PurchaseRate, rate.SaleRate, rate.PurchaseRateNB, rate.SaleRateNB);

                                        await botClient.SendTextMessageAsync(message.Chat, _rateString, ParseMode.Markdown);
                                    }
                                    else
                                    {
                                        _rateString = string.Format(Resources.NotFoundRate, state.CurrencyCode, result.Date);
                                        await botClient.SendTextMessageAsync(message.Chat,
                                                                             _rateString,
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
