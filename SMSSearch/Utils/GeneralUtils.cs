using System;
using System.Text;
using System.Security.Cryptography;

namespace SMS_Search.Utils
{
    public static class GeneralUtils
    {
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
    }
}
