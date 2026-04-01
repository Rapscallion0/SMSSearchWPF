using System.IO;
using ZXing;
using ZXing.Common;
using ZXing.Rendering;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;
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
                    Margin = 10
                }
            };
            var svgImage = writer.Write(barcodeData);
            return svgImage.Content;
        }

        public void SaveAsPdf(string barcodeData, Gs1BarcodeType type, string filePath)
        {
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
                    Margin = 10
                }
            };

            var pixelData = writer.Write(barcodeData);

            // To embed in PDF, we need to convert PixelData to an image format PdfSharp understands.
            // Simplified approach using a temporary file since PdfSharpCore may lack direct bitmap support
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
            gfx.DrawImage(image, 50, 50, 300, 100);

            document.Save(filePath);

            // Clean up
            if (File.Exists(tempImage))
            {
                File.Delete(tempImage);
            }
        }
    }
}
