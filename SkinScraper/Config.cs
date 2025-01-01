using System.Text.Json;

namespace SkinScraper
{
    internal class Config
    {
        public int CurrentPage { get; set; }
        public int MinValue { get; set; }
        public int MaxValue { get; set; }
        public int MinProfit { get; set; }
        public string ProfitBasedOn { get; set; }
        public string PriceHistoryAmount { get; set; }
        public string OrderBy { get; set; }
        public string SteamGuard { get; set; }
        public string SteamUsername { get; set; }
        public string SteamPassword { get; set; }
        public string LastRunPage { get; set; }

        public static Config Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return new Config { CurrentPage = 1 }; // Standardwerte
            }

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<Config>(json);
        }

        public static void Save(string filePath, Config config)
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
    }
}