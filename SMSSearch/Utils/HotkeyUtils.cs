using System;
using System.Windows.Input;

namespace SMS_Search.Utils
{
    public static class HotkeyUtils
    {
        public static (Key Key, ModifierKeys Modifiers) Parse(string input)
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
    }
}
