using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMS_Search.Services;
using SMS_Search.Utils;

namespace SMS_Search.ViewModels
{
    public partial class EncryptionUtilityViewModel : ObservableObject
    {
        private readonly IClipboardService _clipboard;
        private readonly IDialogService _dialogService;

        public EncryptionUtilityViewModel(IClipboardService clipboard, IDialogService dialogService)
        {
            _clipboard = clipboard;
            _dialogService = dialogService;
        }

        [ObservableProperty]
        private string _decryptedText;

        [ObservableProperty]
        private string _encryptedText;

        [RelayCommand]
        private void Encrypt()
        {
            if (string.IsNullOrEmpty(DecryptedText)) return;

            try
            {
                 string encrypted = GeneralUtils.Encrypt(DecryptedText);
                 EncryptedText = encrypted;
                 if (!string.IsNullOrEmpty(encrypted))
                 {
                     _clipboard.SetText(encrypted);
                     _dialogService.ShowToast("Encrypted string copied to clipboard.", "Encryption", SMS_Search.Views.ToastType.Success);
                 }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Encryption failed: " + ex.Message, "Error");
            }
        }

        [RelayCommand]
        private void Decrypt()
        {
            if (string.IsNullOrEmpty(EncryptedText)) return;

            try
            {
                string decrypted = GeneralUtils.Decrypt(EncryptedText);
                DecryptedText = decrypted;
                if (!string.IsNullOrEmpty(decrypted))
                {
                    _clipboard.SetText(decrypted);
                    _dialogService.ShowToast("Decrypted string copied to clipboard.", "Encryption", SMS_Search.Views.ToastType.Success);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Decryption failed: " + ex.Message, "Error");
            }
        }
    }
}
