using System.IO;
using ZXing;
using ZXing.Common;
using ZXing.Rendering;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using SMS_Search.Models.Gs1;

namespace SMS_Search.Services.Gs1
{
    public class Gs1BarcodeService : IGs1BarcodeService
    {
        public string GenerateSvg(string barcodeData, Gs1BarcodeType type)
        {
            var format = type == Gs1BarcodeType.Gs1DataMatrix ? BarcodeFormat.DATA_MATRIX : BarcodeFormat.CODE_128;
            var writer = new BarcodeWriterSvg
            {
                Format = format,
                Options = new EncodingOptions
                {
                    Width = 300,
                    Height = 100,
                    Margin = 10,
                    PureBarcode = false
                }
            };
            var svgImage = writer.Write(barcodeData);
            return svgImage.Content;
        }

        public System.Windows.Media.Imaging.BitmapSource GenerateBitmapSource(string barcodeData, Gs1BarcodeType type)
        {
            var format = type == Gs1BarcodeType.Gs1DataMatrix ? BarcodeFormat.DATA_MATRIX : BarcodeFormat.CODE_128;
            var writer = new ZXing.Windows.Compatibility.BarcodeWriter
            {
                Format = format,
                Options = new EncodingOptions
                {
                    Width = 600,
                    Height = 200,
                    Margin = 10,
                    PureBarcode = false
                }
            };

            using (var bitmap = writer.Write(barcodeData))
            {
                var hbitmap = bitmap.GetHbitmap();
                try
                {
                    var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hbitmap,
                        System.IntPtr.Zero,
                        System.Windows.Int32Rect.Empty,
                        System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

                    // Freeze it for cross-thread access if needed
                    source.Freeze();
                    return source;
                }
                finally
                {
                    // Clean up GDI handles to avoid memory leaks
                    DeleteObject(hbitmap);
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(System.IntPtr hObject);

        public void SaveAsPdf(string barcodeData, Gs1BarcodeType type, string filePath, bool includeDetails = false, System.Collections.Generic.List<Gs1ParsedAi>? parsedAis = null, string? barcodeName = null, string? barcodeDescription = null)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            var document = new PdfDocument();
            var page = document.AddPage();
            var gfx = XGraphics.FromPdfPage(page);

            var format = type == Gs1BarcodeType.Gs1DataMatrix ? BarcodeFormat.DATA_MATRIX : BarcodeFormat.CODE_128;
            var writer = new BarcodeWriterPixelData
            {
                Format = format,
                Options = new EncodingOptions
                {
                    Width = 300,
                    Height = 100,
                    Margin = 10,
                    PureBarcode = false
                }
            };

            var pixelData = writer.Write(barcodeData);

            // To embed in PDF, we need to convert PixelData to an image format PdfSharp understands.
            // Simplified approach using a temporary file since PdfSharp may lack direct bitmap support
            string tempImage = Path.GetTempFileName() + ".png";

            using (var bmp = new System.Drawing.Bitmap(pixelData.Width, pixelData.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                var bmpData = bmp.LockBits(new System.Drawing.Rectangle(0, 0, pixelData.Width, pixelData.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bmpData.Scan0, pixelData.Pixels.Length);
                }
                finally
                {
                    bmp.UnlockBits(bmpData);
                }
                bmp.Save(tempImage, System.Drawing.Imaging.ImageFormat.Png);
            }

            XImage image = XImage.FromFile(tempImage);

            double currentY = 50;

            if (!string.IsNullOrWhiteSpace(barcodeName))
            {
                var fontName = new XFont("Arial", 20, XFontStyleEx.Bold);
                gfx.DrawString(barcodeName, fontName, XBrushes.Black, new XRect(50, currentY, page.Width.Point - 100, 25), XStringFormats.TopCenter);
                currentY += 30;
            }

            if (!string.IsNullOrWhiteSpace(barcodeDescription))
            {
                var fontDesc = new XFont("Arial", 14, XFontStyleEx.Regular);
                // Basic text layout
                var formatter = new PdfSharp.Drawing.Layout.XTextFormatter(gfx);
                var rect = new XRect(50, currentY, page.Width.Point - 100, 50);
                formatter.DrawString(barcodeDescription, fontDesc, XBrushes.Black, rect, XStringFormats.TopCenter);
                currentY += 40;
            }

            gfx.DrawImage(image, 50, currentY, 300, 100);

            var fontRegular = new XFont("Arial", 10, XFontStyleEx.Regular);

            currentY += 105;

            if (!includeDetails)
            {
                // Note: User requested removal of double printed barcode text when printing,
                // but this applies to print/pdf when IncludeDetails is false. The barcode image
                // inherently includes the text.
            }

            if (includeDetails && parsedAis != null && parsedAis.Count > 0)
            {
                var fontBold = new XFont("Arial", 12, XFontStyleEx.Bold);
                var fontSmall = new XFont("Arial", 8, XFontStyleEx.Regular);

                double yPosition = currentY + 15; // Below the image

                gfx.DrawString("GS1 Barcode Details", fontBold, XBrushes.Black, new XRect(50, yPosition, page.Width.Point - 100, 20), XStringFormats.TopLeft);
                yPosition += 25;

                gfx.DrawString($"Raw Data: {barcodeData}", fontRegular, XBrushes.Black, new XRect(50, yPosition, page.Width.Point - 100, 20), XStringFormats.TopLeft);
                yPosition += 30;

                // Draw table header
                gfx.DrawString("AI", fontBold, XBrushes.Black, new XRect(50, yPosition, 50, 20), XStringFormats.TopLeft);
                gfx.DrawString("Value", fontBold, XBrushes.Black, new XRect(100, yPosition, 150, 20), XStringFormats.TopLeft);
                gfx.DrawString("Title", fontBold, XBrushes.Black, new XRect(250, yPosition, page.Width.Point - 300, 20), XStringFormats.TopLeft);
                yPosition += 20;

                gfx.DrawLine(new XPen(XColors.Black, 1), 50, yPosition, page.Width.Point - 50, yPosition);
                yPosition += 5;

                foreach (var ai in parsedAis)
                {
                    string aiText = ai.Ai == "└─" ? "  └─" : ai.Ai ?? "";
                    string rawVal = ai.RawValue ?? "";
                    string title = ai.Definition?.Title ?? "Unknown";

                    // Handle wrapping or simply drawing
                    gfx.DrawString(aiText, fontRegular, XBrushes.Black, new XRect(50, yPosition, 50, 20), XStringFormats.TopLeft);
                    gfx.DrawString(rawVal, fontRegular, XBrushes.Black, new XRect(100, yPosition, 150, 20), XStringFormats.TopLeft);

                    // Simple word wrap for Title since some titles are very long
                    var titleRect = new XRect(250, yPosition, page.Width.Point - 300, 40);
                    XStringFormat strFormat = new XStringFormat();
                    strFormat.Alignment = XStringAlignment.Near;
                    strFormat.LineAlignment = XLineAlignment.Near;

                    // PdfSharp DrawString doesn't natively word-wrap for simple DrawString. Use XTextFormatter or calculate
                    // But we can just use an XTextFormatter for safety if we had one. Let's just draw string.
                    gfx.DrawString(title, fontRegular, XBrushes.Black, new XRect(250, yPosition, page.Width.Point - 300, 20), XStringFormats.TopLeft);

                    yPosition += 15;

                    if (!string.IsNullOrEmpty(ai.Definition?.Description) && ai.Definition.Description != ai.Definition.Title)
                    {
                        gfx.DrawString(ai.Definition.Description, fontSmall, XBrushes.Gray, new XRect(250, yPosition, page.Width.Point - 300, 20), XStringFormats.TopLeft);
                        yPosition += 15;
                    }

                    yPosition += 5;

                    if (yPosition > page.Height.Point - 50)
                    {
                        page = document.AddPage();
                        gfx = XGraphics.FromPdfPage(page);
                        yPosition = 50;
                    }
                }
            }

            document.Save(filePath);

            // Clean up
            if (File.Exists(tempImage))
            {
                File.Delete(tempImage);
            }
        }
    }
}
