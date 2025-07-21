using System.Configuration;
using System.Data;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Tcma.LanguageComparison.Gui.Services;
using Tcma.LanguageComparison.Gui.ViewModels;

namespace Tcma.LanguageComparison.Gui
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static ServiceProvider? ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            // Đăng ký handler cho mọi unhandled exception
            RegisterGlobalExceptionHandlers();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<SettingsService>();
            services.AddSingleton<ISettingsService>(sp => sp.GetRequiredService<SettingsService>());
            services.AddSingleton<IErrorHandlingService, ErrorHandlingService>();
            services.AddSingleton<IRetryPolicyService, RetryPolicyService>();
            services.AddSingleton<MainWindowViewModel>();
            // Đăng ký ViewModel, các service khác nếu cần
        }

        private void RegisterGlobalExceptionHandlers()
        {
            // Lấy ErrorHandlingService từ DI
            var errorHandler = ServiceProvider?.GetService<IErrorHandlingService>();
            if (errorHandler == null) return;

            // UI thread exceptions
            this.DispatcherUnhandledException += (s, exArgs) =>
            {
                errorHandler.LogInfo("[AppCrash] DispatcherUnhandledException", exArgs.Exception.ToString());
                var error = errorHandler.ProcessException(exArgs.Exception, "DispatcherUnhandledException");
                errorHandler.HandleErrorAsync(error, false).Wait();
                exArgs.Handled = true; // Ngăn app tắt ngay
            };

            // Non-UI thread exceptions
            AppDomain.CurrentDomain.UnhandledException += (s, exArgs) =>
            {
                var ex = exArgs.ExceptionObject as Exception;
                errorHandler.LogInfo("[AppCrash] UnhandledException", ex?.ToString() ?? "Unknown");
                var error = errorHandler.ProcessException(ex ?? new Exception("Unknown unhandled exception"), "AppDomain.UnhandledException");
                errorHandler.HandleErrorAsync(error, false).Wait();
            };

            // Unobserved task exceptions
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, exArgs) =>
            {
                errorHandler.LogInfo("[AppCrash] UnobservedTaskException", exArgs.Exception.ToString());
                var error = errorHandler.ProcessException(exArgs.Exception, "TaskScheduler.UnobservedTaskException");
                errorHandler.HandleErrorAsync(error, false).Wait();
                exArgs.SetObserved();
            };
        }
    }
}

