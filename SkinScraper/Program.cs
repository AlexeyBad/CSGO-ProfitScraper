using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Text.Json;
using System.Globalization;
using System.Diagnostics;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;

namespace SkinScraper
{
    public class Offer
    {
        public string SkinName { get; set; }
        public string ShopName { get; set; }
        public string Quality { get; set; }
        public decimal Price { get; set; }
        public decimal SteamPrice { get; set; }
        public decimal Profit { get; set; }
    }

    public class SteamPriceEntry
    {
        public DateTime Date { get; set; }
        public double Price { get; set; }
        public int Volume { get; set; }
    }
    public class SteamPriceHistory
    {
        public bool success { get; set; }
        public string price_prefix { get; set; }
        public string price_suffix { get; set; }
        public List<List<object>> prices { get; set; }

        public List<SteamPriceEntry> GetPriceEntries()
        {
            return prices.Select(p => new SteamPriceEntry
            {
                Date = DateTime.ParseExact(p[0].ToString(), "MMM dd yyyy HH: +0", CultureInfo.InvariantCulture),
                Price = Convert.ToDouble(p[1]),
                Volume = Convert.ToInt32(p[2])
            }).ToList();
        }
    }

    internal class Program
    {
        static Config _config;
        static IWebDriver _driver;

        static async Task Main(string[] args)
        {
            #region setup
            Console.Title = "CSGOSKINS.GG - ProfitScraper";
            List<string> lItemUrls = new List<string>();

            string sConfigFilePath = "Config.json";
            _config = Config.Load(sConfigFilePath);

            ChromeOptions options = new ChromeOptions();
            options.AddArgument("--lang=en");
            options.AddArgument("--log-level=3");
            options.AddArgument("--headless");

            options.AddArgument("--window-size=1920,1080");

            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddExcludedArgument("enable-automation");
            options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.5735.199 Safari/537.36");

            _driver = new ChromeDriver(options);

            ((IJavaScriptExecutor)_driver).ExecuteScript("window.open();");
            _driver.SwitchTo().Window(_driver.WindowHandles[1]);

            _driver.Navigate().GoToUrl("https://steamcommunity.com/market/");
            if (!string.IsNullOrEmpty(_config.SteamGuard))
            {
                while (IsUserNotLoggedIn(_driver))
                {
                    Thread.Sleep(1000);
                }
            }
            else
            {
                //Todo: better selectors
                _driver.FindElement(By.XPath("//*[@id=\"global_action_menu\"]/a[2]")).Click();

                var loginFields = new WebDriverWait(_driver, TimeSpan.FromSeconds(5))
                    .Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(By.ClassName("_2GBWeup5cttgbTw8FM3tfx")));
                loginFields[0].SendKeys(_config.SteamUsername);
                loginFields[1].SendKeys(_config.SteamPassword);
                _driver.FindElement(By.XPath("//*[@id=\"responsive_page_template_content\"]/div[1]/div[1]/div/div/div/div[2]/div/form/div[4]/button")).Click();
            }
            Console.Clear();
            string sAsciiArt = @"
             ______             ___ _        ______                                     
            (_____ \           / __|_)  _   / _____)                                    
             _____) )___ ___ _| |__ _ _| |_( (____   ____  ____ _____ ____  _____  ____ 
            |  ____/ ___) _ (_   __) (_   _)\____ \ / ___)/ ___|____ |  _ \| ___ |/ ___)
            | |   | |  | |_| || |  | | | |_ _____) | (___| |   / ___ | |_| | ____| |    
            |_|   |_|   \___/ |_|  |_|  \__|______/ \____)_|   \_____|  __/|_____)_|    
                                                                     |_|                
            ";
            Console.WriteLine(sAsciiArt);
            string sBaseurl = "https://csgoskins.gg/?page=";
            _driver.SwitchTo().Window(_driver.WindowHandles[0]);
            _driver.Navigate().GoToUrl(sBaseurl + _config.CurrentPage);
            #endregion
            while (true)
            {
                lItemUrls.Clear();
                _driver.Navigate().GoToUrl(sBaseurl + _config.CurrentPage);
                var itemArray = _driver.FindElements(By.CssSelector("div.bg-gray-800.rounded.shadow-md.relative.flex.flex-wrap"));
                foreach (var item in itemArray)
                {   
                    var priceRanges = item.FindElements(By.ClassName("hover:underline"));
                    for (int i = 0; i < priceRanges.Count - 1; i++)
                    {
                        if (PriceTextToValue(priceRanges[i].Text) > _config.MinValue && PriceTextToValue(priceRanges[i].Text) < _config.MaxValue)
                        {
                            lItemUrls.Add(item.FindElement(By.CssSelector("h2 > a")).GetAttribute("href"));
                            break;
                        }
                    }
                }
                foreach (var sUrl in lItemUrls)
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    ProccessItemPage(sUrl);
                    stopwatch.Stop();
                    long iElapsedTimeS = stopwatch.ElapsedMilliseconds / 1000;
                    if (iElapsedTimeS < 1)
                    {
                        Thread.Sleep(1000);
                    }
                }
                _config.CurrentPage++;
                Config.Save(sConfigFilePath, _config);
                Thread.Sleep(new Random().Next(5, 8) * 1000);

            }
        }


        static bool IsUserNotLoggedIn(IWebDriver driver)
        {
            try
            {
                return driver.FindElement(By.XPath("//*[@id=\"global_action_menu\"]/a[2]")).Text == "login";
            }
            catch (NoSuchElementException)
            {
                return false;
            }
        }

        static void ProccessItemPage(string url)
        {
            List<Offer> lOffers = new List<Offer>();
            List<string> lQualityUrls = new List<string>();

            WebDriverWait wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(5));
            lQualityUrls.Clear();
            lOffers.Clear();
            _driver.Navigate().GoToUrl(url);
            var qualityArray = _driver.FindElements(By.ClassName("version-link"));
            foreach (var qualityItem in qualityArray)
            {
                Decimal dQualityPrice = PriceTextToValue(qualityItem.FindElement(By.CssSelector("div:nth-child(1) > div:nth-child(2) > span:nth-child(1)")).Text);
                if (dQualityPrice > _config.MinValue && dQualityPrice < _config.MaxValue)
                {
                    lQualityUrls.Add(qualityItem.GetAttribute("href"));
                }
            }
            if (lQualityUrls.Count == 0)
            {
                _driver.Navigate().Back();
                return;
            }
            string sSkinName = "";
            foreach (var qualityUrl in lQualityUrls)
            {
                Thread.Sleep(1000);
                ((IJavaScriptExecutor)_driver).ExecuteScript("window.open();");
                _driver.SwitchTo().Window(_driver.WindowHandles[2]);
                _driver.Navigate().GoToUrl(qualityUrl);

                string sQualityName = Path.GetFileName(_driver.Url);
                sSkinName = ExtractSkinName(_driver.Url);


                var showMoreButtons = _driver.FindElements(By.XPath("//span[text()='Show more offers']"));
                if (showMoreButtons.Count > 0)
                {
                    showMoreButtons[0].Click();
                }
                var priceSections = wait.Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(By.CssSelector("div.active-offer")));
                #region No Steam Offers
                bool isSoldOnSteam = false;
                foreach (var priceSection in priceSections)
                {
                  if (priceSection.FindElement(By.CssSelector("div.w-full.whitespace-nowrap a")).Text == "Steam")
                        isSoldOnSteam = true;
                }
                if (!isSoldOnSteam || priceSections.Count < 2)
                {
                    _driver.Close();
                    _driver.SwitchTo().Window(_driver.WindowHandles[0]);
                    _driver.Navigate().Back();

                    continue;
                }
                #endregion
                foreach (var priceSection in priceSections)
                {
                    string sShopName;
                    decimal dShopPrice;

                    sShopName = priceSection.FindElement(By.CssSelector("div.w-full.whitespace-nowrap a")).Text;

                    bool isRecommended = priceSection.GetAttribute("class").Contains("border-blue-700");
                    dShopPrice = isRecommended
                        ? PriceTextToValue(_driver.FindElement(By.XPath("/html/body/main/div[2]/div[2]/div[1]/div[2]/div[4]/div[2]/span")).Text)
                        : PriceTextToValue(priceSection.FindElement(By.CssSelector("div.w-1\\/2.sm\\:w-1\\/4.p-4.flex-none span.font-bold.text-lg.sm\\:text-xl")).Text);
                    if (sShopName.Contains("Steam"))
                    {
                        int iProfitBasedOn = int.Parse(_config.ProfitBasedOn);
                        if (iProfitBasedOn == 2 || iProfitBasedOn == 3)
                        {
                            var steamurl = priceSection.FindElement(By.CssSelector("div.w-full.sm\\:w-1\\/4.p-4.flex-none.text-center.sm\\:text-right > a")).GetAttribute("href");
                            _driver.SwitchTo().Window(_driver.WindowHandles[1]);
                            _driver.Navigate().GoToUrl(steamurl);
                            if (iProfitBasedOn == 2)
                            { // Direct Buy 
                                dShopPrice = PriceTextToValue(wait.Until(ExpectedConditions.ElementIsVisible(By.CssSelector("#market_commodity_buyrequests > span:nth-child(2)"))).Text);
                            }
                            else if (iProfitBasedOn == 3)
                            { // Price History
                                dShopPrice = GetSteamSaleHistory(_driver);
                            }
                            _driver.SwitchTo().Window(_driver.WindowHandles[2]);
                        }
                    }
                    if (sShopName.Contains("BUFF163"))
                    {
                        continue;
                    }
                    lOffers.Add(new Offer { ShopName = sShopName, Quality = sQualityName, Price = dShopPrice });
                }
                _driver.Close();
                _driver.SwitchTo().Window(_driver.WindowHandles[0]);
                _driver.Navigate().Back();
            }
            if (lOffers.Count > 0)
            {
                Offer bestOffer = GetBestOffer(lOffers);

                Console.ForegroundColor = bestOffer.Profit > _config.MinProfit ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine($"Item: {sSkinName} {bestOffer.Quality}, Shop: {bestOffer.ShopName} {bestOffer.Price} - Profit: {bestOffer.Profit}");
                Console.ResetColor();
            }
        }

        static decimal GetSteamSaleHistory(IWebDriver driver)
        {
            var cookies = driver.Manage().Cookies.AllCookies;

            using (var client = new HttpClient())
            {
                foreach (var cookie in cookies)
                {
                    client.DefaultRequestHeaders.Add("Cookie", $"{cookie.Name}={cookie.Value}");
                }
                var apiUrl = "https://steamcommunity.com/market/pricehistory/?appid=730&market_hash_name=" + Path.GetFileName(driver.Url);
                var response = client.GetAsync(apiUrl).Result;
                var jsonString = response.Content.ReadAsStringAsync().Result;
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var priceData = JsonSerializer.Deserialize<SteamPriceHistory>(jsonString, options);
                var lastPrices = priceData.prices.TakeLast(int.Parse(_config.PriceHistoryAmount)).Reverse();
                decimal valuesum = 0;
                foreach (var priceEntry in lastPrices)
                {
                    valuesum += decimal.Parse(priceEntry[1].ToString(), CultureInfo.InvariantCulture);
                }
                return valuesum / lastPrices.Count() + 1;
            }
        }

        static Offer GetBestOffer(List<Offer> offers)
        {
            var steamOffers = offers.Where(o => o.ShopName.Equals("Steam", StringComparison.OrdinalIgnoreCase)).ToList();
            var nonSteamOffers = offers.Where(o => !o.ShopName.Equals("Steam", StringComparison.OrdinalIgnoreCase)).ToList();

            var bestOffer = nonSteamOffers
                .GroupBy(o => o.Quality)
                .Select(group =>
                {
                    var steamPrice = steamOffers.FirstOrDefault(s => string.Equals(s.Quality, group.Key, StringComparison.OrdinalIgnoreCase))?.Price ?? 0;

                    var bestNonSteamOffer = group.OrderBy(o => o.Price).First();

                    return new Offer { Quality = group.Key, ShopName = bestNonSteamOffer.ShopName, Price = bestNonSteamOffer.Price, SteamPrice = steamPrice, Profit = Math.Max(0, Math.Truncate((steamPrice * 0.85m) - bestNonSteamOffer.Price)) };
                }).OrderByDescending(o => o.Profit).FirstOrDefault();
            return bestOffer;
        }

        static decimal PriceTextToValue(string priceText)
        {
            if (priceText.Contains("No"))
            { // Item Quality - No Offers
                return Convert.ToDecimal(0);
            }
            priceText = priceText.Substring(0, priceText.Length - 3);
            return Convert.ToDecimal(priceText.Replace("$", "").Replace(",", "").Trim());
        }

        public static string ExtractSkinName(string sWebsiteUrl)
        {
            string[] arrUrlParts = sWebsiteUrl.Split('/');
            return arrUrlParts[arrUrlParts.Length - 2];

        }
    }
}