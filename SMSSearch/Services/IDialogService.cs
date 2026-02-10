using System.Threading.Tasks;

namespace SMS_Search.Services
{
    public interface IDialogService
    {
        void ShowMessage(string message, string title);
        void ShowError(string message, string title);
        bool ShowConfirmation(string message, string title);
        string OpenFileDialog(string filter);
        string SaveFileDialog(string filter, string defaultName = "");
        void ShowToast(string message, string title);
    }
}
