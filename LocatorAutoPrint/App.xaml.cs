using System.Windows;
using LocatorAutoPrint.Services;
using LocatorAutoPrint.ViewModels;
using LocatorAutoPrint.Views;

namespace LocatorAutoPrint
{
    public partial class App : Application
    {
        public MainViewModel MainViewModel { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Start base application lifecycle
            base.OnStartup(e);

            // ============================================================
            // CONFIGURATION INITIALIZATION
            // ============================================================
            var configService = new ConfigService();

            // Load application configuration
            if (!configService.LoadConfig())
            {
                Current.Shutdown();
                return;
            }

            // Load system information
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
                statusService,
                configService,
                sysInfo
            );

            // ============================================================
            // APPLICATION STARTUP WINDOW
            // ============================================================
            // Bypass login and launch MainWindow directly
            var mainWindow = new MainWindow
            {
                DataContext = MainViewModel
            };

            Current.MainWindow = mainWindow;
            mainWindow.Show();
        }
    }
}