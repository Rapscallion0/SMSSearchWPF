using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace SMS_Search.Views
{
    public partial class ClipboardOptionsWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _dontAskAgain;
        public bool DontAskAgain
        {
            get => _dontAskAgain;
            set
            {
                if (_dontAskAgain != value)
                {
                    _dontAskAgain = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _preserveLayout;
        public bool PreserveLayout
        {
            get => _preserveLayout;
            private set
            {
                if (_preserveLayout != value)
                {
                    _preserveLayout = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand CopyContentCommand { get; }
        public ICommand PreserveLayoutCommand { get; }

        public ClipboardOptionsWindow()
        {
            InitializeComponent();
            DataContext = this;

            CopyContentCommand = new RelayCommand(() =>
            {
                PreserveLayout = false;
                DialogResult = true;
                Close();
            });

            PreserveLayoutCommand = new RelayCommand(() =>
            {
                PreserveLayout = true;
                DialogResult = true;
                Close();
            });
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
