using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using TelegramApp;

using IHost host = Host.CreateDefaultBuilder().Build();
IConfiguration config = host.Services.GetRequiredService<IConfiguration>();

Bot bot = new(config);
await bot.StartAsync();