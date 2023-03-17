using TelegramApp.Helpers;
using TelegramApp.Models;

namespace TelegramApp.Services.Interfaces
{
    public interface ICurrencyRateService
    {
        Task<BankResponse> GetRatesAsync(HttpClient? client, UserState? state, string? url);
    }
}