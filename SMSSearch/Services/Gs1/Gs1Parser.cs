using System;
using System.Collections.Generic;
using System.Linq;
using SMS_Search.Models.Gs1;

namespace SMS_Search.Services.Gs1
{
    public class Gs1Parser : IGs1Parser
    {
        public Gs1ParseResult Parse(string barcode, List<Gs1AiDefinition> definitions)
        {
            var result = new Gs1ParseResult();

            if (string.IsNullOrWhiteSpace(barcode))
            {
                result.IsValid = false;
                result.ErrorMessage = "Barcode is empty.";
                return result;
            }

            // Remove typical FNC1 indicators if at the start
            if (barcode.StartsWith("]C1"))
            {
                barcode = barcode.Substring(3);
            }

            int index = 0;
            while (index < barcode.Length)
            {
                // Check if it starts with an FNC1 character/group separator, usually represented by ASCII 29 in clean strings
                if (barcode[index] == (char)29)
                {
                    index++;
                    continue;
                }

                // AI is typically 2 to 4 digits
                Gs1AiDefinition? matchedDef = null;
                int aiLength = 0;

                for (int i = 4; i >= 2; i--)
                {
                    if (index + i <= barcode.Length)
                    {
                        string possibleAi = barcode.Substring(index, i);
                        matchedDef = definitions.FirstOrDefault(d => d.Ai == possibleAi);
                        if (matchedDef != null)
                        {
                            aiLength = i;
                            break;
                        }
                    }
                }

                if (matchedDef == null)
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"Unknown Application Identifier at position {index}: {barcode.Substring(index)}";
                    result.UnparsedData = barcode.Substring(index);
                    break;
                }

                index += aiLength;

                // Extract value
                string rawValue = "";
                if (matchedDef.IsVariableLength)
                {
                    // Look for Group Separator (ASCII 29) or end of string
                    int gsIndex = barcode.IndexOf((char)29, index);
                    if (gsIndex >= 0)
                    {
                        // Ensure we don't exceed max length before GS
                        int length = gsIndex - index;
                        if (length > matchedDef.MaxLength)
                        {
                            result.IsValid = false;
                            result.ErrorMessage = $"Value for AI {matchedDef.Ai} exceeds maximum length of {matchedDef.MaxLength}.";
                            rawValue = barcode.Substring(index, Math.Min(barcode.Length - index, matchedDef.MaxLength));
                            index += rawValue.Length; // Might miss GS but parser stops anyway
                        }
                        else
                        {
                            rawValue = barcode.Substring(index, length);
                            index = gsIndex + 1; // Skip GS
                        }
                    }
                    else
                    {
                        // No GS found, take up to MaxLength or end of string
                        int length = Math.Min(barcode.Length - index, matchedDef.MaxLength);
                        rawValue = barcode.Substring(index, length);
                        index += length;
                    }
                }
                else
                {
                    // Fixed length
                    if (index + matchedDef.MaxLength <= barcode.Length)
                    {
                        rawValue = barcode.Substring(index, matchedDef.MaxLength);
                        index += matchedDef.MaxLength;
                    }
                    else
                    {
                        result.IsValid = false;
                        result.ErrorMessage = $"Value for AI {matchedDef.Ai} is shorter than required fixed length of {matchedDef.MaxLength}.";
                        rawValue = barcode.Substring(index);
                        index = barcode.Length;
                    }
                }

                var parsedAi = new Gs1ParsedAi
                {
                    Ai = matchedDef.Ai,
                    Definition = matchedDef,
                    RawValue = rawValue,
                    IsValid = true // Base validation, can be enhanced with check digits
                };

                // Simple check digit validation for GTIN/SSCC
                if (matchedDef.Specification.Contains("csum"))
                {
                    if (!ValidateCheckDigit(rawValue))
                    {
                        parsedAi.IsValid = false;
                        parsedAi.ErrorMessage = "Invalid check digit.";
                        result.IsValid = false;
                    }
                }

                result.ParsedAis.Add(parsedAi);
            }

            if (string.IsNullOrEmpty(result.ErrorMessage))
            {
                result.IsValid = true;
            }

            return result;
        }

        public string DetectType(List<Gs1ParsedAi> parsedAis)
        {
            if (parsedAis.Any(a => a.Ai == "00")) return "SSCC-18";
            if (parsedAis.Any(a => a.Ai == "01"))
            {
                if (parsedAis.Any(a => a.Ai == "10" || a.Ai == "21")) return "GS1-128 (GTIN + Attributes)";
                return "GTIN-14";
            }
            if (parsedAis.Any(a => a.Ai == "255")) return "GCN";
            if (parsedAis.Any(a => a.Ai == "253")) return "GDTI";

            return "GS1 Generic";
        }

        private bool ValidateCheckDigit(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 2) return false;

            // GS1 Check Digit Calculation (Modulo 10)
            int sum = 0;
            string payload = value.Substring(0, value.Length - 1);
            int checkDigit = int.Parse(value.Substring(value.Length - 1, 1));

            bool multiplyBy3 = true;
            for (int i = payload.Length - 1; i >= 0; i--)
            {
                if (!char.IsDigit(payload[i])) return false;
                int digit = int.Parse(payload[i].ToString());
                sum += digit * (multiplyBy3 ? 3 : 1);
                multiplyBy3 = !multiplyBy3;
            }

            int calculatedCheckDigit = (10 - (sum % 10)) % 10;
            return checkDigit == calculatedCheckDigit;
        }
    }
}
