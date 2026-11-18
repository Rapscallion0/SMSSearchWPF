using CommunityToolkit.Mvvm.ComponentModel;
using SMS_Search.Data;
using System.Collections.ObjectModel;

namespace SMS_Search.ViewModels
{
    public enum GroupBracketPart
    {
        None,
        Top,
        Middle,
        Bottom,
        Single
    }

    public class GroupBracketIndicator
    {
        public int Depth { get; set; }
        public GroupBracketPart Part { get; set; }
    }

    public partial class SearchClauseRowViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelectedForGroup;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanMoveUp))]
        private bool _isFirstRow;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanMoveDown))]
        private bool _isLastRow;

        public bool CanMoveUp => !IsFirstRow;
        public bool CanMoveDown => !IsLastRow;

        [ObservableProperty]
        private SearchLogicalOperator _logicalOperator = SearchLogicalOperator.And;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsTableField))]
        [NotifyPropertyChangedFor(nameof(IsNumericField))]
        private SearchFieldTarget _field = SearchFieldTarget.Description;

        [ObservableProperty]
        private string _columnName = string.Empty;

        [ObservableProperty]
        private SearchComparisonOperator _operator = SearchComparisonOperator.Contains;

        [ObservableProperty]
        private string _value = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsTableField))]
        private bool _isFieldMode;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsTableField))]
        private bool _isRecordQueryMode;

        public bool IsTableField => IsFieldMode && !IsRecordQueryMode && Field == SearchFieldTarget.Table;
        public bool IsNumericField => !IsRecordQueryMode && Field == SearchFieldTarget.Number;

        public ObservableCollection<GroupBracketIndicator> GroupBrackets { get; } = new();

        partial void OnIsFieldModeChanged(bool value)
        {
            OnPropertyChanged(nameof(IsTableField));
            if (!value && Field == SearchFieldTarget.Table)
            {
                Field = SearchFieldTarget.Description;
            }
        }
    }
}

