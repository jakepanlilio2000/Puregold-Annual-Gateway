using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using LocatorAutoPrint.Services;
using LocatorAutoPrint.ViewModels;
using LocatorAutoPrint.Views;

namespace LocatorAutoPrint
{
    public partial class App : Application
    {
        public MainViewModel MainViewModel { get; private set; }

        private const string FtpBaseDirectory = "ftp://192.168.200.177/toho/(722)San_Fernando/Others/Annual%20Gateway/logs/";
        private const string FtpPassword = "pw@1234";
        private static readonly string[] FtpUsers = {
            @"puregold\1",  @"puregold\2",  @"puregold\3",  @"puregold\4",
            @"puregold\5",  @"puregold\6",  @"puregold\7",  @"puregold\8",
            @"puregold\9",  @"puregold\10", @"puregold\11", @"puregold\12"
        };

        protected override void OnStartup(StartupEventArgs e)
        {
            RegisterGlobalExceptionHandlers();

            try
            {
                // Enable TLS 1.2, TLS 1.1, and TLS 1.0 compatibility for Windows 7 SP1 and newer
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072
                                                     | (SecurityProtocolType)768
                                                     | SecurityProtocolType.Tls;
            }
            catch
            {
                // Fallback gracefully if protocol is unavailable
            }

            base.OnStartup(e);

            try
            {
                // ============================================================
                // CONFIGURATION INITIALIZATION
                // ============================================================
                var configService = new ConfigService();

                if (!configService.LoadConfig())
                {
                    LogDirect("AppStartup", "Configuration Load Failed. Application shutting down.", isFatal: true);
                    Current.Shutdown();
                    return;
                }

                var sysInfo = new SystemInfoService();

                // ============================================================
                // CORE SERVICES INITIALIZATION
                // ============================================================
                var dbService = new DatabaseService(
                    configService.ConnectionString,
                    configService.AppBaseDir
                );

                var printService = new PrintService(
                    configService.AppBaseDir
                );

                var editService = new EditCountSheetService(
                    configService.ConnectionString
                );

                // ============================================================
                // FEATURE SERVICES INITIALIZATION
                // ============================================================
                var reportsService = new ReportsService(
                    configService.ConnectionString
                );

                var maintenanceService = new LocatorMaintenanceService(
                    configService.ConnectionString
                );

                var restoreService = new RestoreService(
                    configService.ConnectionString,
                    configService.AppBaseDir
                );

                var pdfService = new PdfExportService();

                var statusService = new SystemStatusService(
                    configService.ConnectionString
                );

                var userService = new UserService(
                    configService.ConnectionString
                );

                var stockService = new StockValueService(
                    configService.ConnectionString
                );

                // ============================================================
                // SUB VIEWMODELS INITIALIZATION
                // ============================================================
                var maintenanceVM = new LocatorMaintenanceViewModel(
                    maintenanceService,
                    configService
                );

                var reportsVM = new ReportsViewModel(
                    reportsService,
                    pdfService,
                    stockService,
                    printService
                );

                var usersVM = new UsersViewModel(
                    userService, configService
                );

                var updateService = new UpdateService(configService);
                var aboutVM = new AboutViewModel(updateService, sysInfo, configService);
                var settingsVM = new SettingsViewModel(configService, sysInfo);

                // ============================================================
                // PRIMARY VIEWMODELS INITIALIZATION
                // ============================================================
                var locatorViewModel = new LocatorPrintViewModel(
                    dbService,
                    printService,
                    configService,
                    maintenanceVM,
                    restoreService
                );

                var editCountSheetViewModel = new EditCountSheetViewModel(
                    editService,
                    dbService,
                    printService,
                    configService
                );

                // ============================================================
                // MAIN VIEWMODEL INITIALIZATION
                // ============================================================
                MainViewModel = new MainViewModel(
                    locatorViewModel,
                    editCountSheetViewModel,
                    reportsVM,
                    usersVM,
                    aboutVM,
                    settingsVM,
                    statusService,
                    configService,
                    sysInfo
                );

                // ============================================================
                // APPLICATION STARTUP WINDOW
                // ============================================================
                var mainWindow = new MainWindow
                {
                    DataContext = MainViewModel
                };

                Current.MainWindow = mainWindow;
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                HandleCrash("App.OnStartup Failed", ex, isTerminating: true);
                throw;
            }
        }

        private void RegisterGlobalExceptionHandlers()
        {
            DispatcherUnhandledException += (s, args) =>
            {
                HandleCrash("DispatcherUnhandledException (UI Thread)", args.Exception, isTerminating: false);
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception ?? new Exception($"Fatal CLR Error: {args.ExceptionObject}");
                HandleCrash("AppDomain.UnhandledException (Process Fatal)", ex, isTerminating: args.IsTerminating);
            };

            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                HandleCrash("TaskScheduler.UnobservedTaskException", args.Exception, isTerminating: false);
                args.SetObserved();
            };
        }

        private void HandleCrash(string context, Exception ex, bool isTerminating)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("================================================================================");
                sb.AppendLine($"APPLICATION CRASH REPORT - {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                sb.AppendLine("================================================================================");
                sb.AppendLine($"Machine Name  : {Environment.MachineName}");
                sb.AppendLine($"OS Version    : {Environment.OSVersion}");
                sb.AppendLine($"Environment   : 64-bit OS: {Environment.Is64BitOperatingSystem}, 64-bit Process: {Environment.Is64BitProcess}");
                sb.AppendLine($"Logged In User: {Environment.UserDomainName}\\{Environment.UserName}");
                sb.AppendLine($"Context       : {context}");
                sb.AppendLine($"Is Terminating: {isTerminating}");
                sb.AppendLine("--------------------------------------------------------------------------------");
                sb.AppendLine("EXCEPTION DETAILS:");

                int depth = 0;
                Exception currentEx = ex;
                while (currentEx != null)
                {
                    sb.AppendLine($"[Level {depth}] {currentEx.GetType().FullName}: {currentEx.Message}");
                    sb.AppendLine($"Target Site: {currentEx.TargetSite}");
                    sb.AppendLine($"Stack Trace:\n{currentEx.StackTrace}");
                    sb.AppendLine("--------------------------------------------------------------------------------");
                    currentEx = currentEx.InnerException;
                    depth++;
                }

                string reportContent = sb.ToString();
                string localDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CrashLogs");
                Directory.CreateDirectory(localDirectory);

                string fileName = $"Crash_{Environment.MachineName}_{DateTime.Now:yyyyMMdd_HHmmssfff}.log";
                string localFilePath = Path.Combine(localDirectory, fileName);
                File.WriteAllText(localFilePath, reportContent);

                
                Task.Run(() => UploadLogToFtp(fileName, localFilePath));
            }
            catch
            {
                
            }
        }

        private void LogDirect(string context, string message, bool isFatal)
        {
            var ex = new ApplicationException(message);
            HandleCrash(context, ex, isFatal);
        }

        private void UploadLogToFtp(string remoteFileName, string localFilePath)
        {
            if (!File.Exists(localFilePath)) return;
            byte[] fileBytes = File.ReadAllBytes(localFilePath);
            string targetUrl = FtpBaseDirectory.TrimEnd('/') + "/" + remoteFileName;
            foreach (var user in FtpUsers)
            {
                try
                {
                    var request = (FtpWebRequest)WebRequest.Create(targetUrl);
                    request.Method = WebRequestMethods.Ftp.UploadFile;
                    request.Credentials = new NetworkCredential(user, FtpPassword);
                    request.UseBinary = true;
                    request.KeepAlive = false;
                    request.Timeout = 3000; 
                    request.ReadWriteTimeout = 3000; 

                    using (var requestStream = request.GetRequestStream())
                    {
                        requestStream.Write(fileBytes, 0, fileBytes.Length);
                    }

                    using (var response = (FtpWebResponse)request.GetResponse())
                    {
                        return;
                    }
                }
                catch
                {
                    continue;
                }
            }
        }
    }
}