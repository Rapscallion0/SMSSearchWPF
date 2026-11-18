using Moq;
using SMS_Search.Services;
using SMS_Search.Utils;
using SMS_Search.ViewModels;
using SMS_Search.Views;
using Xunit;

namespace SMSSearch.Tests
{
    public class LegacyIniEncryptionTests
    {
        [Theory]
        [InlineData("0123456789", "#ÏÎÍÌËÊÉÈÇÆ")]
        [InlineData("ABCDEFGHIJKLMNOPQ", "#¾½¼»º¹¸·¶µ´³²±°¯®")]
        [InlineData("STUVWXYZ", "#¬«ª©¨§¦¥")]
        [InlineData("acdefghijklmn", "#žœ›š™˜—–•”“’‘")]
        [InlineData("qstuvwxyz", "#ŽŒ‹Š‰ˆ‡†…")]
        [InlineData("!@#$%^&*()_-", "#Þ¿ÜÛÚ¡ÙÕ×Ö\u00A0Ò")]
        public void EncryptLegacyIni_MatchesDelphiCipherOutput(string plain, string expected)
        {
            string actual = GeneralUtils.EncryptLegacyIni(plain);
            Assert.Equal(expected, actual);
            Assert.Equal(plain, GeneralUtils.DecryptLegacyIni(actual));
        }

        [Fact]
        public void EncryptLegacyIni_AndDecryptLegacyIni_RoundTripsAllAsciiIncludingControlMappedChars()
        {
            const string input = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!@#$%^&*()_-~";

            string encrypted = GeneralUtils.EncryptLegacyIni(input);
            Assert.StartsWith("#", encrypted);
            Assert.Equal(input.Length + 1, encrypted.Length);

            string decrypted = GeneralUtils.DecryptLegacyIni(encrypted);
            Assert.Equal(input, decrypted);

            // Also works without leading '#'
            Assert.Equal(input, GeneralUtils.DecryptLegacyIni(encrypted[1..]));
        }

        [Fact]
        public void EncryptLegacyIni_AndDecryptLegacyIni_HandleEmptyInputs()
        {
            Assert.Equal(string.Empty, GeneralUtils.EncryptLegacyIni(string.Empty));
            Assert.Equal(string.Empty, GeneralUtils.DecryptLegacyIni(string.Empty));
            Assert.Equal(string.Empty, GeneralUtils.DecryptLegacyIni("#"));
        }

        [Fact]
        public void EncryptionUtilityViewModel_UsesLegacyIniCipher()
        {
            var clipboard = new Mock<IClipboardService>();
            var dialog = new Mock<IDialogService>();
            var logger = new Mock<ILoggerService>();

            var vm = new EncryptionUtilityViewModel(clipboard.Object, dialog.Object, logger.Object);
            vm.DecryptedText = "Admin123!";
            vm.EncryptCommand.Execute(null);

            Assert.Equal("#¾›’–‘ÎÍÌÞ", vm.EncryptedText);
            clipboard.Verify(c => c.SetText("#¾›’–‘ÎÍÌÞ"), Times.Once);
            dialog.Verify(d => d.ShowToast("Encrypted string copied to clipboard.", "Encryption", ToastType.Success), Times.Once);

            vm.DecryptedText = string.Empty;
            vm.DecryptCommand.Execute(null);

            Assert.Equal("Admin123!", vm.DecryptedText);
            clipboard.Verify(c => c.SetText("Admin123!"), Times.Once);
        }
    }
}

