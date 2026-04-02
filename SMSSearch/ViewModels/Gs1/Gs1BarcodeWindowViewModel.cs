using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMS_Search.Services.Gs1;
using SMS_Search.Services;

namespace SMS_Search.ViewModels.Gs1
{
    public partial class Gs1BarcodeWindowViewModel : ObservableObject
    {
        private readonly IGs1BarcodeService _barcodeService;
        private readonly IClipboardService _clipboard;
        private readonly IDialogService _dialogService;
        private readonly string _barcodeData;

        public Gs1BarcodeWindowViewModel(
            string barcodeData,
            IGs1BarcodeService barcodeService,
            IClipboardService clipboard,
            IDialogService dialogService)
        {
            _barcodeData = barcodeData;
            _barcodeService = barcodeService;
            _clipboard = clipboard;
            _dialogService = dialogService;

            AvailableSymbologies = new ObservableCollection<Gs1BarcodeType>
            {
                Gs1BarcodeType.Gs1_128,
                Gs1BarcodeType.Gs1DataMatrix
            };

            SelectedSymbology = Gs1BarcodeType.Gs1_128;
            UpdateBarcodeImage();
        }

        public ObservableCollection<Gs1BarcodeType> AvailableSymbologies { get; }

        [ObservableProperty]
        private Gs1BarcodeType _selectedSymbology;

        partial void OnSelectedSymbologyChanged(Gs1BarcodeType value)
        {
            UpdateBarcodeImage();
        }

        [ObservableProperty]
        private System.Windows.Media.Imaging.BitmapSource? _barcodeImage;

        private void UpdateBarcodeImage()
        {
            if (string.IsNullOrWhiteSpace(_barcodeData)) return;

            try
            {
                BarcodeImage = _barcodeService.GenerateBitmapSource(_barcodeData, SelectedSymbology);
            }
            catch (System.Exception)
            {
                _dialogService.ShowToast("Failed to generate barcode image.", "Error", SMS_Search.Views.ToastType.Error);
            }
        }

        [RelayCommand]
        private void CopySvg()
        {
            string svg = _barcodeService.GenerateSvg(_barcodeData, SelectedSymbology);
            _clipboard.SetText(svg);
            _dialogService.ShowToast("Barcode SVG copied to clipboard.", "Copy SVG", SMS_Search.Views.ToastType.Success);
        }

        [RelayCommand]
        private void SavePdf()
        {
            string path = _dialogService.SaveFileDialog("PDF Files (*.pdf)|*.pdf", "barcode.pdf") ?? "";
            if (!string.IsNullOrEmpty(path))
            {
                _barcodeService.SaveAsPdf(_barcodeData, SelectedSymbology, path);
                _dialogService.ShowToast($"Barcode PDF saved.", "Save PDF", SMS_Search.Views.ToastType.Success);
            }
        }

        [RelayCommand]
        private void Print()
        {
            var printDialog = new System.Windows.Controls.PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                var image = new System.Windows.Controls.Image
                {
                    Source = BarcodeImage,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    Margin = new System.Windows.Thickness(50)
                };

                // Measure and arrange the image so it has size
                image.Measure(new System.Windows.Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight));
                image.Arrange(new System.Windows.Rect(0, 0, printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight));

                printDialog.PrintVisual(image, "GS1 Barcode");
            }
        }
    }
}