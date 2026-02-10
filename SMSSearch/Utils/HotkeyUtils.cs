using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace SMS_Search.Utils
{
    public static class HotkeyUtils
    {
        public static (Key Key, ModifierKeys Modifiers) Parse(string? input)
        {
             if (string.IsNullOrWhiteSpace(input)) return (Key.None, ModifierKeys.None);

             ModifierKeys modifiers = ModifierKeys.None;
             Key key = Key.None;

             var parts = input.Split(new[] { '+', ',' }, StringSplitOptions.RemoveEmptyEntries);
             foreach (var part in parts)
             {
                 var p = part.Trim().ToUpperInvariant();
                 if (p == "CTRL" || p == "CONTROL" || p == "<CTRL>") modifiers |= ModifierKeys.Control;
                 else if (p == "ALT" || p == "<ALT>") modifiers |= ModifierKeys.Alt;
                 else if (p == "SHIFT" || p == "<SHIFT>") modifiers |= ModifierKeys.Shift;
                 else if (p == "WIN" || p == "WINDOWS") modifiers |= ModifierKeys.Windows;
                 else
                 {
                     try {
                         if (Enum.TryParse(p.Replace("<", "").Replace(">", ""), true, out Key k))
                         {
                             key = k;
                         }
                     } catch {}
                 }
             }

             return (key, modifiers);
        }

        public static string GetFriendlyKeyName(Key key)
        {
            switch (key)
            {
                case Key.Oem3: return "`";
                case Key.OemQuestion: return "?";
                case Key.OemPlus: return "+";
                case Key.OemMinus: return "-";
                case Key.OemPeriod: return ".";
                case Key.OemComma: return ",";
                case Key.Oem1: return ";";
                case Key.Oem5: return "\\";
                case Key.Oem6: return "]";
                case Key.OemOpenBrackets: return "[";
                case Key.OemQuotes: return "'";
                case Key.Enter: return "Enter";
                case Key.Back: return "Backspace";
                case Key.Delete: return "Delete";
                case Key.Escape: return "Esc";
                case Key.Space: return "Space";
                case Key.Tab: return "Tab";

                // D0-D9
                case Key.D0: return "0";
                case Key.D1: return "1";
                case Key.D2: return "2";
                case Key.D3: return "3";
                case Key.D4: return "4";
                case Key.D5: return "5";
                case Key.D6: return "6";
                case Key.D7: return "7";
                case Key.D8: return "8";
                case Key.D9: return "9";

                // NumPad
                case Key.NumPad0: return "Num 0";
                case Key.NumPad1: return "Num 1";
                case Key.NumPad2: return "Num 2";
                case Key.NumPad3: return "Num 3";
                case Key.NumPad4: return "Num 4";
                case Key.NumPad5: return "Num 5";
                case Key.NumPad6: return "Num 6";
                case Key.NumPad7: return "Num 7";
                case Key.NumPad8: return "Num 8";
                case Key.NumPad9: return "Num 9";
                case Key.Add: return "Num +";
                case Key.Subtract: return "Num -";
                case Key.Multiply: return "Num *";
                case Key.Divide: return "Num /";
                case Key.Decimal: return "Num .";

                default: return key.ToString();
            }
        }

        public static string GetFriendlyString(Key key, ModifierKeys modifiers)
        {
            var parts = new List<string>();

            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control) parts.Add("Ctrl");
            if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) parts.Add("Alt");
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) parts.Add("Shift");
            if ((modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) parts.Add("Win");

            bool isModifierKey =
                key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin;

            if (key != Key.None && !isModifierKey && key != Key.System)
            {
                parts.Add(GetFriendlyKeyName(key));
            }

            string result = string.Join(" + ", parts);

            // If we have modifiers but no terminal key (or just pressed a modifier), append " + "
            if (parts.Count > 0 && (key == Key.None || isModifierKey || key == Key.System))
            {
                result += " + ";
            }

            return result;
        }
    }
}
