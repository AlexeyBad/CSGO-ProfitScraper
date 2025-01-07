using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Text;
using System.Xml.Linq;
using System.Configuration;
using System.Text.Json;
using System.Globalization;
using System.Diagnostics;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;

namespace SkinScraper
{
    public class WebsiteOffers
    {
        public string name { get; set; }
        public string quality { get; set; }
        public decimal price { get; set; }
    }
    public class BestOffer
    {
        public string quality { get; set; }
        public string bestSellerName { get; set; }
        public decimal bestPrice { get; set; }
        public decimal steamPrice { get; set; }
        public decimal profit { get; set; }
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

        static List<string> itemUrls = new List<string>();
        static List<string> versionUrls = new List<string>();
        static List<WebsiteOffers> offers = new List<WebsiteOffers>();

        static Config _config;
        static IWebDriver _driver;
        static Random random = new Random();

        static async Task Main(string[] args)
        {
            #region setup
            Console.Title = "CSGOSKINS.GG - ProfitScraper";
            string configFilePath = "Config.json";
            _config = Config.Load(configFilePath);

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
            string baseurl = "https://csgoskins.gg/?page=";
            _driver.SwitchTo().Window(_driver.WindowHandles[0]);
            _driver.Navigate().GoToUrl(baseurl + _config.CurrentPage);
            #endregion
            while (true)
            {
                itemUrls.Clear();
                _driver.Navigate().GoToUrl(baseurl + _config.CurrentPage);
                var itemArray = _driver.FindElements(By.CssSelector("div.bg-gray-800.rounded.shadow-md.relative.flex.flex-wrap"));
                foreach (var item in itemArray)
                {   
                    var PriceRange = item.FindElements(By.ClassName("hover:underline"));
                    for (int i = 0; i < PriceRange.Count - 1; i++)
                    {
                        if (PriceTextToValue(PriceRange[i].Text) > _config.MinValue && PriceTextToValue(PriceRange[i].Text) < _config.MaxValue)
                        {
                            itemUrls.Add(item.FindElement(By.CssSelector("h2 > a")).GetAttribute("href"));
                            break;
                        }
                    }
                }
                foreach (var url in itemUrls)
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    ProccessItemPage(url);
                    stopwatch.Stop();
                    long elapsedTimeS = stopwatch.ElapsedMilliseconds / 1000;
                    if (elapsedTimeS < 1)
                    {
                        Thread.Sleep(1000);
                    }
                }
                _config.CurrentPage++;
                Config.Save(configFilePath, _config);
                
                Thread.Sleep(random.Next(5, 8) * 1000);
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
            // Make the depth more clear: Item; Version; 
            WebDriverWait wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(5));
            versionUrls.Clear();
            offers.Clear();
            _driver.Navigate().GoToUrl(url);
            var versionsArray = _driver.FindElements(By.ClassName("version-link"));
            foreach (var versionItem in versionsArray)
            {
                Decimal versionPrice = PriceTextToValue(versionItem.FindElement(By.CssSelector("div:nth-child(1) > div:nth-child(2) > span:nth-child(1)")).Text);
                if (versionPrice > _config.MinValue && versionPrice < _config.MaxValue)
                {
                    versionUrls.Add(versionItem.GetAttribute("href"));
                }
            }
            if (versionUrls.Count == 0)
            {
                _driver.Navigate().Back();
                return;
            }
            string skinName = "";
            foreach (var versionUrl in versionUrls)
            {
                Thread.Sleep(1000);
                ((IJavaScriptExecutor)_driver).ExecuteScript("window.open();");
                _driver.SwitchTo().Window(_driver.WindowHandles[2]);
                _driver.Navigate().GoToUrl(versionUrl);

                string versionName = Path.GetFileName(_driver.Url);
                skinName = ExtractSkinName(_driver.Url);


                var showMoreButtons = _driver.FindElements(By.XPath("//span[text()='Show more offers']"));
                if (showMoreButtons.Count > 0)
                {
                    showMoreButtons[0].Click();
                }
                var priceSections = wait.Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(By.CssSelector("div.active-offer")));
                bool bItemSoldOnSteam = false;
                foreach (var priceSection in priceSections)
                {
                  if (priceSection.FindElement(By.CssSelector("div.w-full.whitespace-nowrap a")).Text == "Steam")
                        bItemSoldOnSteam = true;
                }
                if (!bItemSoldOnSteam)
                {
                    _driver.Close();
                    _driver.SwitchTo().Window(_driver.WindowHandles[0]);
                    _driver.Navigate().Back();

                    continue;
                }
                foreach (var priceSection in priceSections)
                {
                    string SellerName;
                    decimal SellerPrice;

                    SellerName = priceSection.FindElement(By.CssSelector("div.w-full.whitespace-nowrap a")).Text;

                    bool isRecommended = priceSection.GetAttribute("class").Contains("border-blue-700");
                    SellerPrice = isRecommended
                        ? PriceTextToValue(_driver.FindElement(By.XPath("/html/body/main/div[2]/div[2]/div[1]/div[2]/div[4]/div[2]/span")).Text)
                        : PriceTextToValue(priceSection.FindElement(By.CssSelector("div.w-1\\/2.sm\\:w-1\\/4.p-4.flex-none span.font-bold.text-lg.sm\\:text-xl")).Text);
                    if (SellerName.Contains("Steam"))
                    {
                        int ProfitBasedOn = int.Parse(_config.ProfitBasedOn);
                        if (ProfitBasedOn == 2 || ProfitBasedOn == 3)
                        {
                            var steamurl = priceSection.FindElement(By.CssSelector("div.w-full.sm\\:w-1\\/4.p-4.flex-none.text-center.sm\\:text-right > a")).GetAttribute("href");
                            _driver.SwitchTo().Window(_driver.WindowHandles[1]);
                            _driver.Navigate().GoToUrl(steamurl);
                            if (ProfitBasedOn == 2)
                            { // Direct Buy 
                                SellerPrice = PriceTextToValue(wait.Until(ExpectedConditions.ElementIsVisible(By.CssSelector("#market_commodity_buyrequests > span:nth-child(2)"))).Text);
                            }
                            else if (ProfitBasedOn == 3)
                            { // Price History
                                SellerPrice = GetSteamSaleHistory(_driver);
                            }
                            _driver.SwitchTo().Window(_driver.WindowHandles[2]);
                        }
                    }
                    if (SellerName.Contains("BUFF163"))
                    {
                        continue;
                    }
                    offers.Add(new WebsiteOffers { name = SellerName, quality = versionName, price = SellerPrice });
                }
                _driver.Close();
                _driver.SwitchTo().Window(_driver.WindowHandles[0]);
                _driver.Navigate().Back();
            }
            if (offers.Count > 0)
            {
                BestOffer profitOffer = GetBestOffer(offers);

                Console.ForegroundColor = profitOffer.profit > _config.MinProfit ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine($"Item: {skinName} {profitOffer.quality}, Shop: {profitOffer.bestSellerName} {profitOffer.bestPrice} - Profit: {profitOffer.profit}");
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

        static BestOffer GetBestOffer(List<WebsiteOffers> offers)
        {
            var steamOffers = offers.Where(o => o.name.Equals("Steam", StringComparison.OrdinalIgnoreCase)).ToList();
            var nonSteamOffers = offers.Where(o => !o.name.Equals("Steam", StringComparison.OrdinalIgnoreCase)).ToList();

            var bestOffer = nonSteamOffers
                .GroupBy(o => o.quality)
                .Select(group =>
                {
                    var steamPrice = steamOffers.FirstOrDefault(s => string.Equals(s.quality, group.Key, StringComparison.OrdinalIgnoreCase))?.price ?? 0;

                    var bestNonSteamOffer = group.OrderBy(o => o.price).First();

                    return new BestOffer { quality = group.Key, bestSellerName = bestNonSteamOffer.name, bestPrice = bestNonSteamOffer.price, steamPrice = steamPrice, profit = Math.Truncate((steamPrice * 0.85m) - bestNonSteamOffer.price) };
                }).OrderByDescending(o => o.profit).FirstOrDefault();
            return bestOffer;
        }

        static decimal PriceTextToValue(string priceText)
        {
            if (priceText.Contains("No"))
            { // Item Version - No Offers
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