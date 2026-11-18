using System;
using System.Text;
using System.Security.Cryptography;

namespace SMS_Search.Utils
{
    public static class GeneralUtils
    {
        private static readonly Lazy<Encoding> Windows1252Encoding = new(() =>
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252);
        });

        public static string Encrypt(string sLine)
        {
            if (string.IsNullOrEmpty(sLine)) return "";
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(sLine);
                byte[] encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encrypted);
            }
            catch
            {
                return "";
            }
        }

        public static string Decrypt(string sLine)
        {
            if (string.IsNullOrEmpty(sLine)) return "";
            if (sLine.StartsWith("#")) return "";

            try
            {
                byte[] data = Convert.FromBase64String(sLine);
                byte[] decrypted = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Encrypts text using the legacy Delphi INI cipher (Windows-1252 byte complement 255 - b with a '#' prefix).
        /// </summary>
        public static string EncryptLegacyIni(string sLine)
        {
            if (string.IsNullOrEmpty(sLine)) return string.Empty;
            return "#" + TransformLegacyIni(sLine);
        }

        /// <summary>
        /// Decrypts text encrypted with the legacy Delphi INI cipher (strips optional leading '#' and applies 255 - b).
        /// </summary>
        public static string DecryptLegacyIni(string sLine)
        {
            if (string.IsNullOrEmpty(sLine)) return string.Empty;
            string payload = sLine.StartsWith('#') ? sLine[1..] : sLine;
            if (payload.Length == 0) return string.Empty;
            return TransformLegacyIni(payload);
        }

        private static string TransformLegacyIni(string input)
        {
            var cp1252 = Windows1252Encoding.Value;
            var result = new char[input.Length];
            var singleChar = new char[1];
            var singleByte = new byte[1];

            for (int i = 0; i < input.Length; i++)
            {
                char ch = input[i];
                byte b;
                if (IsUndefinedCp1252Control(ch))
                {
                    b = (byte)ch;
                }
                else
                {
                    singleChar[0] = ch;
                    cp1252.GetBytes(singleChar, 0, 1, singleByte, 0);
                    b = singleByte[0];
                }

                byte transformed = (byte)(255 - b);
                if (IsUndefinedCp1252Byte(transformed))
                {
                    result[i] = (char)transformed;
                }
                else
                {
                    singleByte[0] = transformed;
                    cp1252.GetChars(singleByte, 0, 1, singleChar, 0);
                    result[i] = singleChar[0];
                }
            }

            return new string(result);
        }

        private static bool IsUndefinedCp1252Control(char ch) =>
            ch is '\u0081' or '\u008D' or '\u008F' or '\u0090' or '\u009D';

        private static bool IsUndefinedCp1252Byte(byte b) =>
            b is 0x81 or 0x8D or 0x8F or 0x90 or 0x9D;
    }
}
