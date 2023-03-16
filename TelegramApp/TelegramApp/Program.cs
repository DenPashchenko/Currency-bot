using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using TelegramApp;
using Telegram.Bot;
using TelegramApp.Services;

using IHost host = Host.CreateDefaultBuilder().Build();
IConfiguration config = host.Services.GetRequiredService<IConfiguration>();
ITelegramBotClient telagramBotClient = new TelegramBotClient(config["BotToken"]);

Bot bot = new(config, telagramBotClient, new CurrencyRateService());
await bot.StartAsync();