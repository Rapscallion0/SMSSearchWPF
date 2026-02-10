using SMS_Search.ViewModels;
using System.Windows;

namespace SMS_Search.Views
{
    public partial class EulaWindow : Window
    {
        public EulaWindow(EulaViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.RequestClose += () => { this.DialogResult = true; this.Close(); };
        }
    }
}
