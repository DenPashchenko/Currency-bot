using Microsoft.Extensions.Configuration;
using Moq;
using RichardSzalay.MockHttp;
using System.Net;
using System.Text;
using TelegramApp.Helpers;
using TelegramApp.Models;
using TelegramApp.Services;
using TelegramApp.Services.Interfaces;

namespace TelegramAppTests.Services
{
    public class CurrencyRateServiceTests
    {
        ICurrencyRateService currencyRateService = new CurrencyRateService();

        [Fact]
        public async Task GetRatesAsync_BaseLinkToBinkApi_ReturnsRates()
        {
            var expectedBankResponse = new BankResponse
            {
                Date = "14.03.2023",
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
            var mockConfiguration = new Mock<IConfiguration>();
            mockConfiguration.Setup(c => c["PrivatbankAPI"]).Returns("https://api.privatbank.ua/p24api/exchange_rates?json&date=");
            var mockHttp = new MockHttpMessageHandler();
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{
                    ""date"": ""14.03.2023"", ""bank"":""PB"", ""baseCurrency"":980, ""baseCurrencyLit"":""UAH"",
                    ""exchangeRate"": 
                    [
                        {
                            ""currency"": ""USD"",
                            ""baseCurrency"": ""UAH"",
                            ""saleRateNB"": ""36.5686000"",
                            ""purchaseRateNB"": ""36.5686000"",
                            ""saleRate"": ""38.4000000"",
                            ""purchaseRate"": ""37.9000000""
                        }
                     ]
                }", Encoding.UTF8, "application/json")
            };
            mockHttp.When("https://api.privatbank.ua/p24api/exchange_rates?json&date=14.03.2023")
                   .Respond(req => response);
            var httpClient = new HttpClient(mockHttp);


            var actualBankResponse = await currencyRateService.GetRatesAsync(httpClient,
                                                                            new UserState { Date = DateTime.Parse("14.03.23") },
                                                                            mockConfiguration.Object["PrivatbankAPI"]);


            Assert.Equal(expectedBankResponse.Date, actualBankResponse.Date);
            Assert.Equal(expectedBankResponse.ExchangeRate[0].Currency, actualBankResponse.ExchangeRate[0].Currency);
            Assert.Equal(expectedBankResponse.ExchangeRate[0].BaseCurrency, actualBankResponse.ExchangeRate[0].BaseCurrency);
            Assert.Equal(expectedBankResponse.ExchangeRate[0].SaleRateNB, actualBankResponse.ExchangeRate[0].SaleRateNB);
            Assert.Equal(expectedBankResponse.ExchangeRate[0].PurchaseRateNB, actualBankResponse.ExchangeRate[0].PurchaseRateNB);
            Assert.Equal(expectedBankResponse.ExchangeRate[0].SaleRate, actualBankResponse.ExchangeRate[0].SaleRate);
            Assert.Equal(expectedBankResponse.ExchangeRate[0].PurchaseRate, actualBankResponse.ExchangeRate[0].PurchaseRate);
        }

        [Fact]
        public void GetRatesAsync_ArgumentIsNull_ThrowsArgumentNullException()
        {
            var httpClient = new HttpClient();
            var userState = new UserState { Date = DateTime.Now };

            Assert.ThrowsAsync<ArgumentNullException>(() => currencyRateService.GetRatesAsync(null, userState, It.IsAny<string>()));
            Assert.ThrowsAsync<ArgumentNullException>(() => currencyRateService.GetRatesAsync(httpClient, null, It.IsAny<string>()));
            Assert.ThrowsAsync<ArgumentNullException>(() => currencyRateService.GetRatesAsync(httpClient, userState, null));
        }
    }
}
