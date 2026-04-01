using System.Threading.Tasks;
using SMS_Search.Views;

namespace SMS_Search.Services
{
    public enum ExistingTableAction
    {
        Prompt,
        Recreate,
        RecreateAll,
        Skip,
        SkipAll
    }

    public interface IDialogService
    {
        void ShowMessage(string message, string title);
        void ShowError(string message, string title);
        void ShowWarning(string message, string title);
        bool ShowConfirmation(string message, string title);
        string? OpenFileDialog(string filter);
        string? SaveFileDialog(string filter, string defaultName = "");
        string? PickColor(string? defaultColor = null);
        void ShowToast(string message, string title, ToastType type = ToastType.Info, string? details = null, string? filePath = null, System.Windows.Window? owner = null);
        ExistingTableAction ShowTableExistsPrompt(string tableName);
    }
}
