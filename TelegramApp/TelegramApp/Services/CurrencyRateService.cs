using Newtonsoft.Json;
using TelegramApp.Helpers;
using TelegramApp.Models;
using TelegramApp.Services.Interfaces;

namespace TelegramApp.Services
{
    public class CurrencyRateService : ICurrencyRateService
    {
        public async Task<BankResponse> GetRatesAsync(HttpClient? client, UserState? state, string? url)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(url);

            var response = await client.GetAsync(url + state.Date.ToString("dd.MM.yyyy"));
            var content = await response.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<BankResponse>(content);
        }
    }
}
