using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace SMS_Search.Views
{
    public partial class ClipboardOptionsWindow : Window, INotifyPropertyChanged
    {
        public bool DontAskAgain { get; set; }
        public bool PreserveLayout { get; private set; }

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
    }
}
