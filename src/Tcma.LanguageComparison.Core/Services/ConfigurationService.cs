using Microsoft.Extensions.Configuration;
using Tcma.LanguageComparison.Core.Models;

namespace Tcma.LanguageComparison.Core.Services
{
    /// <summary>
    /// Service for managing application configuration
    /// </summary>
    public class ConfigurationService
    {
        private readonly AppConfiguration _config;

        /// <summary>
        /// Initializes configuration service by loading from appsettings.json
        /// </summary>
        public ConfigurationService()
        {
            _config = LoadConfiguration();
        }

        /// <summary>
        /// Gets the application configuration
        /// </summary>
        public AppConfiguration Configuration => _config;

        /// <summary>
        /// Loads configuration from appsettings.json
        /// </summary>
        private static AppConfiguration LoadConfiguration()
        {
            try
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

                var configuration = builder.Build();
                var appConfig = new AppConfiguration();

                // Bind configuration sections to strongly typed classes
                configuration.Bind(appConfig);

                return appConfig;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load appsettings.json: {ex.Message}");
                Console.WriteLine("Using default configuration values.");
                return new AppConfiguration(); // Return default values
            }
        }

        /// <summary>
        /// Displays current configuration values
        /// </summary>
        public void DisplayConfiguration()
        {
            Console.WriteLine("\n=== CURRENT CONFIGURATION ===");
            Console.WriteLine($"Similarity Threshold: {_config.LanguageComparison.SimilarityThreshold:F2}");
            Console.WriteLine($"Demo Row Limit: {_config.LanguageComparison.DemoRowLimit}");
            Console.WriteLine($"Max Concurrent Requests: {_config.LanguageComparison.MaxConcurrentRequests}");
            Console.WriteLine($"Max Content Length: {_config.LanguageComparison.MaxContentLength}");
            Console.WriteLine($"Show Progress Messages: {_config.Output.ShowProgressMessages}");
            Console.WriteLine($"Strip HTML Tags: {_config.Preprocessing.StripHtmlTags}");
            Console.WriteLine("================================");
        }

        /// <summary>
        /// Updates similarity threshold at runtime and saves to file
        /// </summary>
        /// <param name="newThreshold">New threshold value (0.0 to 1.0)</param>
        public void UpdateSimilarityThreshold(double newThreshold)
        {
            if (newThreshold < 0.0 || newThreshold > 1.0)
            {
                throw new ArgumentException("Similarity threshold must be between 0.0 and 1.0", nameof(newThreshold));
            }

            _config.LanguageComparison.SimilarityThreshold = newThreshold;
            Console.WriteLine($"✓ Updated Similarity Threshold to: {newThreshold:F2}");
        }

        /// <summary>
        /// Updates demo row limit at runtime
        /// </summary>
        /// <param name="newLimit">New row limit (0 = no limit)</param>
        public void UpdateDemoRowLimit(int newLimit)
        {
            if (newLimit < 0)
            {
                throw new ArgumentException("Demo row limit must be 0 or positive", nameof(newLimit));
            }

            _config.LanguageComparison.DemoRowLimit = newLimit;
            Console.WriteLine($"✓ Updated Demo Row Limit to: {newLimit} {(newLimit == 0 ? "(no limit)" : "")}");
        }

        /// <summary>
        /// Saves current configuration back to appsettings.json
        /// </summary>
        public async Task SaveConfigurationAsync()
        {
            try
            {
                var jsonString = System.Text.Json.JsonSerializer.Serialize(_config, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync("appsettings.json", jsonString);
                Console.WriteLine("✓ Configuration saved to appsettings.json");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to save configuration: {ex.Message}");
            }
        }
    }
} 