using System.Collections.Generic;
using System.Threading.Tasks;
using SMS_Search.Models.Gs1;

namespace SMS_Search.Services.Gs1
{
    public interface IGs1Repository
    {
        Task<List<Gs1AiDefinition>> DownloadAndCacheAiDefinitionsAsync();
        Task<List<Gs1AiDefinition>> GetAiDefinitionsAsync();
    }

    public interface IGs1Parser
    {
        Gs1ParseResult Parse(string barcode, List<Gs1AiDefinition> definitions);
        string DetectType(List<Gs1ParsedAi> parsedAis);
    }

    public enum Gs1BarcodeType
    {
        Auto,
        Gs1DataMatrix,
        Gs1_128
    }

    public interface IGs1BarcodeService
    {
        string GenerateSvg(string barcodeData, Gs1BarcodeType type);
        void SaveAsPdf(string barcodeData, Gs1BarcodeType type, string filePath);
        System.Windows.Media.Imaging.BitmapSource GenerateBitmapSource(string barcodeData, Gs1BarcodeType type);
    }
}
