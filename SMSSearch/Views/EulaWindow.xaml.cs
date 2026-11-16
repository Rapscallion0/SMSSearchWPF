using System;
using SMS_Search.ViewModels;
using System.Windows;

namespace SMS_Search.Views
{
    public partial class EulaWindow : Window
    {
        private readonly EulaViewModel _viewModel;
        private readonly Action _requestCloseHandler;

        public EulaWindow(EulaViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
            _requestCloseHandler = () => { this.DialogResult = true; this.Close(); };
            _viewModel.RequestClose += _requestCloseHandler;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _viewModel.RequestClose -= _requestCloseHandler;
        }
    }
}
