using System.Text.Json;

namespace SkinScraper
{
    internal class Config
    {
        public int CurrentPage { get; set; }
        public int MinValue { get; set; }
        public int MaxValue { get; set; }
        public int MinProfit { get; set; }
        public required string ProfitBasedOn { get; set; }
        public required string PriceHistoryAmount { get; set; }
        public required string SteamGuard { get; set; }
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