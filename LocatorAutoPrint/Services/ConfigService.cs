using System;
using System.Data.SqlClient;
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

        public event EventHandler ConfigUpdated;

        public ConfigService()
        {
            AppBaseDir = AppDomain.CurrentDomain.BaseDirectory;
        }

        public bool LoadConfig()
        {
            string configPath = Path.Combine(AppBaseDir, "config.json");

            try
            {
                if (!File.Exists(configPath))
                {
                    // Auto-generate fallback defaults if missing
                    Config = CreateDefaultConfig();
                    SaveConfigInternal(Config, configPath);
                    ConnectionString = BuildConnectionString(Config);
                    return true;
                }

                string json = File.ReadAllText(configPath);
                Config = JsonConvert.DeserializeObject<ConfigModel>(json);

                if (Config == null)
                {
                    // Auto-recover if file is empty
                    Config = CreateDefaultConfig();
                    SaveConfigInternal(Config, configPath);
                }
                else
                {
                    // Ensure directories and fallback paths have defaults
                    EnsurePathDefaults(Config);
                }

                ConnectionString = BuildConnectionString(Config);
                return true;
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("ConfigService.LoadConfig", ex, isTerminating: false);
                try
                {
                    // Attempt fallback recovery
                    string backupCorrupted = Path.Combine(AppBaseDir, $"config.json.corrupt_{DateTime.Now:yyyyMMddHHmmss}");
                    if (File.Exists(configPath))
                    {
                        File.Move(configPath, backupCorrupted);
                    }
                    Config = CreateDefaultConfig();
                    SaveConfigInternal(Config, configPath);
                    ConnectionString = BuildConnectionString(Config);
                    MessageBox.Show($"Warning: Corrupted config.json was backed up and re-initialized with defaults.\n\nDetails: {ex.Message}", "Config Recovered", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return true;
                }
                catch (Exception recoveryEx)
                {
                    MessageBox.Show($"Fatal error reading config.json.\n\nDetails: {recoveryEx.Message}", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
        }

        public bool SaveConfig(ConfigModel newConfig)
        {
            if (newConfig == null) return false;

            try
            {
                string configPath = Path.Combine(AppBaseDir, "config.json");
                SaveConfigInternal(newConfig, configPath);
                Config = newConfig;
                ConnectionString = BuildConnectionString(newConfig);
                ConfigUpdated?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("ConfigService.SaveConfig", ex, isTerminating: false);
                return false;
            }
        }

        public ConfigModel CreateDefaultConfig()
        {
            var def = new ConfigModel();
            EnsurePathDefaults(def);
            return def;
        }

        private void EnsurePathDefaults(ConfigModel model)
        {
            if (string.IsNullOrWhiteSpace(model.ExportDirectory))
            {
                model.ExportDirectory = Path.Combine(AppBaseDir, "Exports");
            }
            if (string.IsNullOrWhiteSpace(model.LogDirectory))
            {
                model.LogDirectory = Path.Combine(AppBaseDir, "logs");
            }
            if (string.IsNullOrWhiteSpace(model.DownloadDirectory))
            {
                model.DownloadDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            }
            if (string.IsNullOrWhiteSpace(model.DateFormat))
            {
                model.DateFormat = "MM/dd/yyyy";
            }
            if (model.DbPort <= 0)
            {
                model.DbPort = 1433;
            }
            if (string.IsNullOrWhiteSpace(model.DbCatalog))
            {
                model.DbCatalog = "PUREGOLD";
            }
            if (string.IsNullOrWhiteSpace(model.FtpHost))
            {
                model.FtpHost = "192.168.200.177";
            }
            if (string.IsNullOrWhiteSpace(model.FtpDirectory))
            {
                model.FtpDirectory = "/toho/(722)San_Fernando/Others/Annual Gateway/";
            }
            if (string.IsNullOrWhiteSpace(model.FtpPrefix))
            {
                model.FtpPrefix = "A&VG";
            }
            if (model.FtpTimeoutSeconds <= 0)
            {
                model.FtpTimeoutSeconds = 10;
            }
            if (model.PollIntervalSeconds <= 0)
            {
                model.PollIntervalSeconds = 10;
            }
        }

        private void SaveConfigInternal(ConfigModel model, string path)
        {
            string json = JsonConvert.SerializeObject(model, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        public static string BuildConnectionString(ConfigModel config)
        {
            if (config == null) return string.Empty;

            string host = string.IsNullOrWhiteSpace(config.DbHost) ? "localhost" : config.DbHost.Trim();
            int port = config.DbPort > 0 ? config.DbPort : 1433;
            string server = port == 1433 ? host : $"{host},{port}";

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = server,
                UserID = string.IsNullOrWhiteSpace(config.DbUser) ? "sa" : config.DbUser.Trim(),
                Password = config.DbPass ?? "",
                TrustServerCertificate = true,
                ConnectTimeout = 5
            };

            if (!string.IsNullOrWhiteSpace(config.DbCatalog))
            {
                builder.InitialCatalog = config.DbCatalog.Trim();
            }

            return builder.ConnectionString;
        }
    }
}
