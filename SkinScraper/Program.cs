using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Text;
using System.Xml.Linq;
using System.Configuration;
using System.Text.Json;
using System.Globalization;
using System.Diagnostics;


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
        static StringBuilder output = new StringBuilder();


        static IWebDriver _driver;
        static Config config;

        static async Task Main(string[] args)
        {
            #region setup
            string configFilePath = "Config.json";
            config = Config.Load(configFilePath);
            int currentPage = config.CurrentPage;

            ChromeOptions options = new ChromeOptions();
            options.AddArgument("--log-level=1");
            options.AddArgument("--lang=en");
            _driver = new ChromeDriver(options);

            ((IJavaScriptExecutor)_driver).ExecuteScript("window.open();");
            _driver.SwitchTo().Window(_driver.WindowHandles[1]);

            _driver.Navigate().GoToUrl("https://steamcommunity.com/market/");
            if (!string.IsNullOrEmpty(config.SteamGuard))
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
                System.Threading.Thread.Sleep(1000);
                var loginFields = _driver.FindElements(By.ClassName("_2GBWeup5cttgbTw8FM3tfx"));
                loginFields[0].SendKeys(config.SteamUsername);
                loginFields[1].SendKeys(config.SteamPassword);
                _driver.FindElement(By.XPath("//*[@id=\"responsive_page_template_content\"]/div[1]/div[1]/div/div/div/div[2]/div/form/div[4]/button")).Click();
            }
            Console.Clear();
            int currentpage = int.Parse(config.LastRunPage);
            string baseurl = "https://csgoskins.gg/?page=";
            _driver.SwitchTo().Window(_driver.WindowHandles[0]);
            _driver.Navigate().GoToUrl(baseurl + currentpage);
            #endregion
            while (true)
            {
                itemUrls.Clear();
                _driver.Navigate().GoToUrl(baseurl + currentpage);
                var itemArray = _driver.FindElements(By.CssSelector("div.bg-gray-800.rounded.shadow-md.relative.flex.flex-wrap"));
                foreach (var item in itemArray)
                {   // add each item url of page 
                    itemUrls.Add(item.FindElement(By.CssSelector("h2 > a")).GetAttribute("href"));
                }
                foreach (var url in itemUrls)
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    await ProccessItemPage(url);
                    stopwatch.Stop();
                    long elapsedTimeS = stopwatch.ElapsedMilliseconds / 1000;
                    if (elapsedTimeS < 1)
                    {
                        Thread.Sleep(1000);
                    }
                }
                currentpage++;
                config.CurrentPage = currentPage;
                Config.Save(configFilePath, config);
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

        static async Task ProccessItemPage(string url)
        {
            // Make the depth more clear: Item; Version; 
            versionUrls.Clear();
            output.Clear();
            offers.Clear();
            _driver.Navigate().GoToUrl(url);
            var versionsArray = _driver.FindElements(By.ClassName("version-link"));
            foreach (var versionItem in versionsArray)
            {
                Decimal versionPrice = PriceTextToValue(versionItem.FindElement(By.CssSelector("div:nth-child(1) > div:nth-child(2) > span:nth-child(1)")).Text);
                if (versionPrice > config.MinValue && versionPrice < config.MaxValue)
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
                ((IJavaScriptExecutor)_driver).ExecuteScript("window.open();");
                _driver.SwitchTo().Window(_driver.WindowHandles[2]);
                _driver.Navigate().GoToUrl(versionUrl);

                string versionName = Path.GetFileName(_driver.Url);
                skinName = ExtractSkinName(_driver.Url);


                var showMoreButtons = _driver.FindElements(By.XPath("//span[text()='Show more offers']"));
                if (showMoreButtons.Count > 0)
                {
                    showMoreButtons[0].Click();
                    System.Threading.Thread.Sleep(1000);
                }

                var priceSections = _driver.FindElements(By.CssSelector("div.active-offer"));
                foreach (var priceSection in priceSections)
                {
                    string SellerName;
                    decimal SellerPrice;

                    bool isRecommended = priceSection.GetAttribute("class").Contains("border-blue-700");
                    if (isRecommended)
                    {
                        SellerName = priceSection.FindElement(By.CssSelector("div.w-full.whitespace-nowrap a")).Text;
                        SellerPrice = PriceTextToValue(_driver.FindElement(By.XPath("/html/body/main/div[2]/div[2]/div[1]/div[2]/div[4]/div[2]/span")).Text);
                    }
                    else
                    {
                        SellerName = priceSection.FindElement(By.CssSelector("div.w-full.whitespace-nowrap a")).Text;
                        SellerPrice = PriceTextToValue(priceSection.FindElement(By.CssSelector("div.w-1\\/2.sm\\:w-1\\/4.p-4.flex-none span.font-bold.text-lg.sm\\:text-xl")).Text);
                    }
                    if (SellerName.Contains("Steam"))
                    {
                        int ProfitBasedOn = int.Parse(config.ProfitBasedOn);
                        if (ProfitBasedOn == 2 || ProfitBasedOn == 3)
                        {
                            var steamurl = priceSection.FindElement(By.CssSelector("div.w-full.sm\\:w-1\\/4.p-4.flex-none.text-center.sm\\:text-right > a")).GetAttribute("href");
                            _driver.SwitchTo().Window(_driver.WindowHandles[1]);
                            _driver.Navigate().GoToUrl(steamurl);
                            Thread.Sleep(1000);
                            if (ProfitBasedOn == 2)
                            { // Direct Buy 
                                SellerPrice = PriceTextToValue(_driver.FindElement(By.CssSelector("#market_commodity_buyrequests > span:nth-child(2)")).Text);
                            }
                            else if (ProfitBasedOn == 3)
                            { // Price History
                                SellerPrice = GetSteamSaleHistory(_driver);
                            }
                            _driver.SwitchTo().Window(_driver.WindowHandles[2]);
                            Thread.Sleep(1000);
                        }
                    }
                    if (SellerName.Contains("BUFF163"))
                    {
                        continue;
                    }
                    offers.Add(new WebsiteOffers { name = SellerName, quality = versionName, price = SellerPrice });
                }
                Thread.Sleep(1000);
                _driver.Close();
                _driver.SwitchTo().Window(_driver.WindowHandles[0]);
                _driver.Navigate().Back();
            }
            BestOffer profitOffer = GetBestOffer(offers);
            if (profitOffer.profit > config.MinProfit)
            {
                output.AppendLine("Item: " + skinName + " " + profitOffer.quality);
                output.AppendLine("Shop: " + profitOffer.bestSellerName + " " + profitOffer.bestPrice + " - Profit: " + profitOffer.profit);
                Console.WriteLine(output);
            }
            //skip if no steam seller
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
                if (response.IsSuccessStatusCode == false)
                {
                    Console.WriteLine("Steam History: Error");
                    return 999; //return something else 
                }
                var jsonString = response.Content.ReadAsStringAsync().Result;
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var priceData = JsonSerializer.Deserialize<SteamPriceHistory>(jsonString, options);
                var lastPrices = priceData.prices.TakeLast(int.Parse(config.PriceHistoryAmount)).Reverse();
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