using System.Windows;
using System.Windows.Controls;
using SMS_Search.ViewModels;

namespace SMS_Search.Utils
{
    public sealed class WorkspaceLayoutTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? DocumentTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is ResultTabViewModel)
            {
                return DocumentTemplate;
            }

            return base.SelectTemplate(item, container);
        }
    }

    public sealed class WorkspaceLayoutStyleSelector : StyleSelector
    {
        public Style? DocumentStyle { get; set; }

        public override Style? SelectStyle(object item, DependencyObject container)
        {
            if (item is ResultTabViewModel)
            {
                return DocumentStyle;
            }

            return base.SelectStyle(item, container);
        }
    }
}

