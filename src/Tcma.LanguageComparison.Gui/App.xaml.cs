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
    }
}

