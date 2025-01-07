using System.Text.Json;

namespace SkinScraper
{
    internal class Config
    {
        public required string BaseUrl { get; set; } // Example: https://csgoskins.gg/?page=
        public int CurrentPage { get; set; }
        public int MinValue { get; set; }
        public int MaxValue { get; set; }
        public int MinProfit { get; set; }
        public required string ProfitBasedOn { get; set; } // 1 Cheapest Seller; 2 Instant Sell; Based on Steam History
        public required string PriceHistoryAmount { get; set; } // The amount of last sales to check
        public required string SteamGuard { get; set; } // X if you have Steam Guard enabled
        public required string SteamUsername { get; set; }
        public required  string SteamPassword { get; set; }

        public static Config Load(string sFilePath)
        {
            string sJson = File.ReadAllText(sFilePath);
            return JsonSerializer.Deserialize<Config>(sJson);
        }

        public static void Save(string sFilePath, Config config)
        {
            string sJson = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(sFilePath, sJson);
        }
    }
}