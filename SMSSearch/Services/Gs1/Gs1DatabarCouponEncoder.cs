using System.Collections.Generic;
using System.Linq;
using System.Text;
using SMS_Search.Models.Gs1;
using SMS_Search.ViewModels.Gs1;

namespace SMS_Search.Services.Gs1
{
    public static class Gs1DatabarCouponEncoder
    {
        public static string Encode(IEnumerable<Gs1ParsedAiViewModel> parsedAis)
        {
            var ais = parsedAis.ToList();
            if (!ais.Any()) return "";

            var mainAi = ais.FirstOrDefault(a => a.Ai == "8110" || a.Ai == "8112");
            if (mainAi == null) return "";

            var subAis = ais.Where(a => a.Ai == "└─").ToList();
            if (!subAis.Any()) return mainAi.RawValue; // Return raw value if no sub AIs available

            var sb = new StringBuilder();

            string GetValue(string title) => subAis.FirstOrDefault(a => a.Title == title)?.RawValue ?? "";

            int GetVli(string value)
            {
                if (string.IsNullOrEmpty(value)) return 0;
                int len = value.Length - 6;
                return len < 0 ? 0 : len;
            }

            string PadVliField(string value)
            {
                if (string.IsNullOrEmpty(value)) return new string('0', 6);
                if (value.Length < 6) return value.PadLeft(6, '0');
                return value;
            }

            void AppendValue(string value) => sb.Append(value);

            void AppendVli(int vli) => sb.Append(vli);

            // Base fields
            string primaryCompanyPrefix = PadVliField(GetValue("Primary Company Prefix"));
            AppendVli(GetVli(primaryCompanyPrefix));
            AppendValue(primaryCompanyPrefix);

            AppendValue(GetValue("Offer Code").PadLeft(6, '0'));

            string saveValue = GetValue("Save Value");
            AppendVli(saveValue.Length);
            AppendValue(saveValue);

            string primaryPurchaseReq = GetValue("Primary Purchase Requirement");
            AppendVli(primaryPurchaseReq.Length);
            AppendValue(primaryPurchaseReq);

            AppendValue(GetValue("Primary Purchase Requirement Code").PadRight(1, '0').Substring(0, 1));
            AppendValue(GetValue("Primary Purchase Family Code").PadRight(3, '0').Substring(0, 3));

            // Optional fields
            var processedTitles = new HashSet<string>
            {
                "Primary Company Prefix",
                "Offer Code",
                "Save Value",
                "Primary Purchase Requirement",
                "Primary Purchase Requirement Code",
                "Primary Purchase Family Code"
            };

            var dataField0 = GetValue("Data Field 0");
            if (!string.IsNullOrEmpty(dataField0) && !processedTitles.Contains("Data Field 0"))
            {
                AppendValue("0");
                AppendValue(dataField0.PadRight(2, '0').Substring(0, 2));
                processedTitles.Add("Data Field 0");
            }

            var secondReqCode = GetValue("2nd Additional Purchase Rules Code");
            if (!string.IsNullOrEmpty(secondReqCode) && !processedTitles.Contains("2nd Additional Purchase Rules Code"))
            {
                AppendValue("1");
                AppendValue(secondReqCode.PadRight(1, '0').Substring(0, 1));

                string secondReq = GetValue("2nd Purchase Requirement");
                AppendVli(secondReq.Length);
                AppendValue(secondReq);

                AppendValue(GetValue("2nd Purchase Requirement Code").PadRight(1, '0').Substring(0, 1));
                AppendValue(GetValue("2nd Purchase Family Code").PadRight(3, '0').Substring(0, 3));

                string secondCompanyPrefix = GetValue("2nd Purchase Company Prefix");
                if (secondCompanyPrefix == "N/A" || string.IsNullOrEmpty(secondCompanyPrefix))
                {
                    AppendVli(9);
                }
                else
                {
                    string paddedSecondPrefix = PadVliField(secondCompanyPrefix);
                    AppendVli(GetVli(paddedSecondPrefix));
                    AppendValue(paddedSecondPrefix);
                }
                processedTitles.Add("2nd Additional Purchase Rules Code");
            }

            var thirdReq = GetValue("3rd Purchase Requirement");
            if (!string.IsNullOrEmpty(thirdReq) && !processedTitles.Contains("3rd Purchase Requirement"))
            {
                AppendValue("2");
                AppendVli(thirdReq.Length);
                AppendValue(thirdReq);

                AppendValue(GetValue("3rd Purchase Requirement Code").PadRight(1, '0').Substring(0, 1));
                AppendValue(GetValue("3rd Purchase Family Code").PadRight(3, '0').Substring(0, 3));

                string thirdCompanyPrefix = GetValue("3rd Purchase Company Prefix");
                if (thirdCompanyPrefix == "N/A" || string.IsNullOrEmpty(thirdCompanyPrefix))
                {
                    AppendVli(9);
                }
                else
                {
                    string paddedThirdPrefix = PadVliField(thirdCompanyPrefix);
                    AppendVli(GetVli(paddedThirdPrefix));
                    AppendValue(paddedThirdPrefix);
                }
                processedTitles.Add("3rd Purchase Requirement");
            }

            var expDate = GetValue("Expiration Date");
            if (!string.IsNullOrEmpty(expDate) && !processedTitles.Contains("Expiration Date"))
            {
                AppendValue("3");
                AppendValue(expDate.PadRight(6, '0').Substring(0, 6));
                processedTitles.Add("Expiration Date");
            }

            var startDate = GetValue("Start Date");
            if (!string.IsNullOrEmpty(startDate) && !processedTitles.Contains("Start Date"))
            {
                AppendValue("4");
                AppendValue(startDate.PadRight(6, '0').Substring(0, 6));
                processedTitles.Add("Start Date");
            }

            var serialNumber = GetValue("Serial Number");
            if (!string.IsNullOrEmpty(serialNumber) && !processedTitles.Contains("Serial Number"))
            {
                AppendValue("5");
                string paddedSerial = PadVliField(serialNumber);
                AppendVli(GetVli(paddedSerial));
                AppendValue(paddedSerial);
                processedTitles.Add("Serial Number");
            }

            var retailer = GetValue("Retailer Company Prefix / GLN");
            if (!string.IsNullOrEmpty(retailer) && !processedTitles.Contains("Retailer Company Prefix / GLN"))
            {
                AppendValue("6");
                string paddedRetailer = PadVliField(retailer);
                AppendVli(GetVli(paddedRetailer));
                AppendValue(paddedRetailer);
                processedTitles.Add("Retailer Company Prefix / GLN");
            }

            var saveValueCode = GetValue("Save Value Code");
            if (!string.IsNullOrEmpty(saveValueCode) && !processedTitles.Contains("Save Value Code"))
            {
                AppendValue("9");
                AppendValue(saveValueCode.PadRight(1, '0').Substring(0, 1));
                AppendValue(GetValue("Applies to Which Item").PadRight(1, '0').Substring(0, 1));
                AppendValue(GetValue("Store Coupon").PadRight(1, '0').Substring(0, 1));
                AppendValue(GetValue("Don't Multiply Flag").PadRight(1, '0').Substring(0, 1));
                processedTitles.Add("Save Value Code");
            }

            return sb.ToString();
        }
    }
}
