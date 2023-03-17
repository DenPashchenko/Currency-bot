namespace TelegramApp.Models
{
    public class BankResponse
    {
        public string? Date { get; set; }

        public string? Bank { get; set; }

        public string? BaseCurrency { get; set; }

        public string? BaseCurrencyLit { get; set; }

        public CurrencyRate[]? ExchangeRate { get; set; }
    }
}
