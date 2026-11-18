using System;
using System.Windows;
using SMS_Search.ViewModels;

namespace SMS_Search.Views.Panels
{
    public partial class DateConverterPanelView : System.Windows.Controls.UserControl
    {
        public DateConverterPanelView()
        {
            InitializeComponent();
        }

        private void Today_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.GregorianDate = DateTime.Today;
            }
        }
    }
}
