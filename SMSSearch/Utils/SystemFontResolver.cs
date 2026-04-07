using System;
using System.IO;
using PdfSharp.Fonts;

namespace SMS_Search.Utils
{
    public class SystemFontResolver : IFontResolver
    {
        public string DefaultFontName => "Arial";

        public byte[] GetFont(string faceName)
        {
            // Face name is determined by ResolveTypeface.
            // We map the face names to the actual font file paths on Windows.
            string fontPath = faceName switch
            {
                "Arial" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"),
                "ArialBold" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arialbd.ttf"),
                "ArialItalic" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "ariali.ttf"),
                "ArialBoldItalic" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arialbi.ttf"),
                _ => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf")
            };

            if (File.Exists(fontPath))
            {
                return File.ReadAllBytes(fontPath);
            }

            // Fallback just in case Arial is somehow missing or path is wrong,
            // though very unlikely on Windows.
            throw new InvalidOperationException($"Font file not found: {fontPath}");
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            // Ignore the familyName and just use Arial variations
            // since that's what the app asks for. If you wanted to support more,
            // you'd check familyName here.

            if (familyName.Equals("Arial", StringComparison.OrdinalIgnoreCase))
            {
                if (isBold && isItalic)
                    return new FontResolverInfo("ArialBoldItalic");
                if (isBold)
                    return new FontResolverInfo("ArialBold");
                if (isItalic)
                    return new FontResolverInfo("ArialItalic");

                return new FontResolverInfo("Arial");
            }

            // Fallback for anything else requested, map to regular Arial
            return new FontResolverInfo("Arial");
        }
    }
}
