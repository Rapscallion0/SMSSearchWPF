using System.Windows;
using SMS_Search.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using SMS_Search.Views;

namespace SMS_Search
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.RequestOpenSettings += OnRequestOpenSettings;
        }

        private void OnRequestOpenSettings()
        {
            var win = App.Current.Services.GetRequiredService<SettingsWindow>();
            win.Owner = this;
            win.ShowDialog();
        }
    }
}
