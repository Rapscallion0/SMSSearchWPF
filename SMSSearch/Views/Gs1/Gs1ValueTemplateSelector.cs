using System.Windows;
using System.Windows.Controls;
using SMS_Search.ViewModels.Gs1;

namespace SMS_Search.Views.Gs1
{
    public class Gs1ValueTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? TextTemplate { get; set; }
        public DataTemplate? CheckBoxTemplate { get; set; }
        public DataTemplate? ComboBoxTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is Gs1ParsedAiViewModel viewModel)
            {
                if (viewModel.ControlType == "CheckBox" && CheckBoxTemplate != null)
                {
                    return CheckBoxTemplate;
                }
                else if (viewModel.ControlType == "ComboBox" && ComboBoxTemplate != null)
                {
                    return ComboBoxTemplate;
                }
            }

            return TextTemplate;
        }
    }
}
