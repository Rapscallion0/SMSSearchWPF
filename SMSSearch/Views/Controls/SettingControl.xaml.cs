using System.Windows;
using System.Windows.Markup;

namespace SMS_Search.Views.Controls
{
    [ContentProperty(nameof(Input))]
    public partial class SettingControl : System.Windows.Controls.UserControl
    {
        public SettingControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(nameof(Header), typeof(string), typeof(SettingControl), new PropertyMetadata(null));

        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly DependencyProperty InputProperty =
            DependencyProperty.Register(nameof(Input), typeof(object), typeof(SettingControl), new PropertyMetadata(null));

        public object Input
        {
            get { return GetValue(InputProperty); }
            set { SetValue(InputProperty, value); }
        }

        public static readonly DependencyProperty IsSavedProperty =
            DependencyProperty.Register(nameof(IsSaved), typeof(bool), typeof(SettingControl), new PropertyMetadata(false));

        public bool IsSaved
        {
            get { return (bool)GetValue(IsSavedProperty); }
            set { SetValue(IsSavedProperty, value); }
        }
    }
}
