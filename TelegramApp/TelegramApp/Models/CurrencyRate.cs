namespace TelegramApp.Models
{
    public class CurrencyRate
    {
        public string? Currency { get; set; }

        public string? BaseCurrency { get; set; }

        public string? SaleRateNB { get; set; }

        public string? PurchaseRateNB { get; set; }

        public string PurchaseRate { get; set; } = "N/A";

        public string SaleRate { get; set; } = "N/A";
    }
}
