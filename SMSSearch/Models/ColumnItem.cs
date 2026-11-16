using CommunityToolkit.Mvvm.ComponentModel;

namespace SMS_Search.Models
{
    public partial class ColumnItem : ObservableObject
    {
        public string Name { get; }
        public bool IsRequired { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayText))]
        private string _description;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayText))]
        private bool _isInvalid;

        public string DisplayText
        {
            get
            {
                string baseText = string.IsNullOrWhiteSpace(Description)
                    ? Name
                    : $"{Name} — {Description}";
                if (IsRequired) baseText += " [Required]";
                if (IsInvalid) baseText += " [Invalid]";
                return baseText;
            }
        }

        public ColumnItem(string name, string description = "", bool isRequired = false)
        {
            Name = name;
            _description = description;
            IsRequired = isRequired;
        }

        public override bool Equals(object? obj)
        {
            return obj is ColumnItem other &&
                   string.Equals(Name, other.Name, System.StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return System.StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
        }
    }
}
