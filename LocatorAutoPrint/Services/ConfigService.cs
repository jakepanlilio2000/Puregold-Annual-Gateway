using System;
using System.IO;
using System.Windows;
using LocatorAutoPrint.Models;
using Newtonsoft.Json;

namespace LocatorAutoPrint.Services
{
    public class ConfigService
    {
        public ConfigModel Config { get; private set; }
        public string ConnectionString { get; private set; }
        public string AppBaseDir { get; private set; }

        public ConfigService()
        {
            AppBaseDir = AppDomain.CurrentDomain.BaseDirectory;
        }

        public bool LoadConfig()
        {
            string configPath = Path.Combine(AppBaseDir, "config.json");

            if (!File.Exists(configPath))
            {
                MessageBox.Show($"Could not find config.json at:\n{configPath}", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            try
            {
                string json = File.ReadAllText(configPath);
                Config = JsonConvert.DeserializeObject<ConfigModel>(json);
                ConnectionString = $"Server={Config.DbHost};User Id={Config.DbUser};Password={Config.DbPass};TrustServerCertificate=True;";
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading config.json.\n\nDetailed Error: {ex.Message}", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}