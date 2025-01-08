# CSGO-ProfitScraper: Advanced Market Scraping

## CSGO-ProfitScraper is a C# application designed to analyze and scrape CS:GO skin market-data to identify profitable deals based on configurable parameters. This tool integrates with Selenium for web automation.
<br>

![ProfitScraper-removebg-preview](https://github.com/user-attachments/assets/5d941a8b-1f12-42bb-880c-3b73d39f1fb7)


**Features**

- Analyze potential trading profits among different shops and profit options
- Headless Mode: Operates in the background without requiring user interaction.
- Last Run: Resumes scraping from the last processed page.
- Settings: Adjust parameters like minimum/maximum value, and profit thresholds...
- Delay Management: Ensures optimal request intervals to avoid detection or rate-limiting.
- Export: Results are output as a txt file in CSV format.
<br>

**Prerequisites**

Before running the application, ensure you have the following installed:
- .NET 8.0
- Google Chrome browser (latest version)
- ChromeDriver (compatible with your Chrome version)
<br>

**Example Config (JSON)**

Name has to be Config.json
```
{
  "BaseUrl": "https://csgoskins.gg/?page=",
  "CurrentPage": 1,
  "MinValue": 300,
  "MaxValue": 500,
  "MinProfit": 50,
  "ProfitBasedOn": "2",
  "PriceHistoryAmount": "3",
  "SteamGuard": "",
  "SteamUsername": "Username",
  "SteamPassword": "Password!"
}
```
> [!WARNING]
> This tool only works for the website CSGOSKINS.GG
<br>

**Usage**

1. Clone the repository
2. Install required dependencies
3. Add Config (Config.json)
4. Start Tool 
5. Output data is displayed in the terminal and results.txt
<br>

**Contributing**

Contributions are welcome! Feel free to open issues or submit pull requests.
