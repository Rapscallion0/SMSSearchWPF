using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows.Controls;

namespace SMS_Search.ViewModels.Settings
{
    public abstract class SettingsSectionViewModel : ObservableObject
    {
        public abstract string Title { get; }
        public abstract ControlTemplate Icon { get; }

        public virtual bool Matches(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;

            return Title.Contains(query, StringComparison.OrdinalIgnoreCase);
        }
    }
}
