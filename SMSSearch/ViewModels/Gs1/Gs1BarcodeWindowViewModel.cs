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

        private readonly System.Collections.Generic.List<Models.Gs1.Gs1ParsedAi> _parsedAis;

        public Gs1BarcodeWindowViewModel(
            string barcodeData,
            IGs1BarcodeService barcodeService,
            IClipboardService clipboard,
            IDialogService dialogService,
            System.Collections.Generic.List<Models.Gs1.Gs1ParsedAi>? parsedAis = null)
        {
            _barcodeData = barcodeData;
            _barcodeService = barcodeService;
            _clipboard = clipboard;
            _dialogService = dialogService;
            _parsedAis = parsedAis ?? new System.Collections.Generic.List<Models.Gs1.Gs1ParsedAi>();

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

        [ObservableProperty]
        private bool _includeDetails;

        [ObservableProperty]
        private string? _barcodeName;

        [ObservableProperty]
        private string? _barcodeDescription;

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
                try
                {
                    _barcodeService.SaveAsPdf(_barcodeData, SelectedSymbology, path, IncludeDetails, _parsedAis, BarcodeName, BarcodeDescription);
                    _dialogService.ShowToast($"Barcode PDF saved.", "Save PDF", SMS_Search.Views.ToastType.Success);
                }
                catch (System.Exception)
                {
                    _dialogService.ShowToast("Failed to save barcode PDF.", "Error", SMS_Search.Views.ToastType.Error);
                }
            }
        }

        [RelayCommand]
        private void Print()
        {
            var printDialog = new System.Windows.Controls.PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                System.Windows.FrameworkElement printElement;

                var stackPanel = new System.Windows.Controls.StackPanel
                {
                    Margin = new System.Windows.Thickness(50)
                };

                if (!string.IsNullOrWhiteSpace(BarcodeName))
                {
                    stackPanel.Children.Add(new System.Windows.Controls.TextBlock
                    {
                        Text = BarcodeName,
                        FontWeight = System.Windows.FontWeights.Bold,
                        FontSize = 20,
                        Margin = new System.Windows.Thickness(0, 0, 0, 5),
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                    });
                }

                if (!string.IsNullOrWhiteSpace(BarcodeDescription))
                {
                    stackPanel.Children.Add(new System.Windows.Controls.TextBlock
                    {
                        Text = BarcodeDescription,
                        FontSize = 14,
                        Margin = new System.Windows.Thickness(0, 0, 0, 20),
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        TextWrapping = System.Windows.TextWrapping.Wrap
                    });
                }

                var image = new System.Windows.Controls.Image
                {
                    Source = BarcodeImage,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    Margin = new System.Windows.Thickness(50)
                };

                image.Margin = new System.Windows.Thickness(0, 0, 0, 20);
                stackPanel.Children.Add(image);

                if (IncludeDetails && _parsedAis.Count > 0)
                {

                    var headerText = new System.Windows.Controls.TextBlock
                    {
                        Text = "GS1 Barcode Details",
                        FontWeight = System.Windows.FontWeights.Bold,
                        FontSize = 16,
                        Margin = new System.Windows.Thickness(0, 0, 0, 10)
                    };
                    stackPanel.Children.Add(headerText);

                    var rawDataText = new System.Windows.Controls.TextBlock
                    {
                        Text = $"Raw Data: {_barcodeData}",
                        Margin = new System.Windows.Thickness(0, 0, 0, 20)
                    };
                    stackPanel.Children.Add(rawDataText);

                    var grid = new System.Windows.Controls.Grid();
                    grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(50) });
                    grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(150) });
                    grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });

                    var headerAi = new System.Windows.Controls.TextBlock { Text = "AI", FontWeight = System.Windows.FontWeights.Bold };
                    var headerValue = new System.Windows.Controls.TextBlock { Text = "Value", FontWeight = System.Windows.FontWeights.Bold };
                    var headerTitle = new System.Windows.Controls.TextBlock { Text = "Title", FontWeight = System.Windows.FontWeights.Bold };

                    System.Windows.Controls.Grid.SetColumn(headerAi, 0);
                    System.Windows.Controls.Grid.SetColumn(headerValue, 1);
                    System.Windows.Controls.Grid.SetColumn(headerTitle, 2);

                    grid.Children.Add(headerAi);
                    grid.Children.Add(headerValue);
                    grid.Children.Add(headerTitle);

                    var separator = new System.Windows.Controls.Border
                    {
                        BorderBrush = System.Windows.Media.Brushes.Black,
                        BorderThickness = new System.Windows.Thickness(0, 0, 0, 1),
                        Margin = new System.Windows.Thickness(0, 20, 0, 5)
                    };
                    grid.Children.Add(separator);
                    System.Windows.Controls.Grid.SetColumnSpan(separator, 3);

                    int rowIndex = 1;
                    foreach (var ai in _parsedAis)
                    {
                        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

                        var aiText = new System.Windows.Controls.TextBlock { Text = ai.Ai == "└─" ? "  └─" : ai.Ai, Margin = new System.Windows.Thickness(0, 5, 0, 0) };
                        var valueText = new System.Windows.Controls.TextBlock { Text = ai.RawValue, Margin = new System.Windows.Thickness(0, 5, 0, 0), TextWrapping = System.Windows.TextWrapping.Wrap };

                        var titleStack = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(0, 5, 0, 0) };
                        titleStack.Children.Add(new System.Windows.Controls.TextBlock { Text = ai.Definition?.Title ?? "Unknown", TextWrapping = System.Windows.TextWrapping.Wrap });

                        if (!string.IsNullOrEmpty(ai.Definition?.Description) && ai.Definition.Description != ai.Definition.Title)
                        {
                            titleStack.Children.Add(new System.Windows.Controls.TextBlock
                            {
                                Text = ai.Definition.Description,
                                Foreground = System.Windows.Media.Brushes.Gray,
                                FontSize = 10,
                                TextWrapping = System.Windows.TextWrapping.Wrap
                            });
                        }

                        System.Windows.Controls.Grid.SetRow(aiText, rowIndex);
                        System.Windows.Controls.Grid.SetColumn(aiText, 0);

                        System.Windows.Controls.Grid.SetRow(valueText, rowIndex);
                        System.Windows.Controls.Grid.SetColumn(valueText, 1);

                        System.Windows.Controls.Grid.SetRow(titleStack, rowIndex);
                        System.Windows.Controls.Grid.SetColumn(titleStack, 2);

                        grid.Children.Add(aiText);
                        grid.Children.Add(valueText);
                        grid.Children.Add(titleStack);

                        rowIndex++;
                    }

                    stackPanel.Children.Add(grid);
                }

                printElement = stackPanel;

                // Measure and arrange the element so it has size
                printElement.Measure(new System.Windows.Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight));
                printElement.Arrange(new System.Windows.Rect(0, 0, printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight));

                printDialog.PrintVisual(printElement, "GS1 Barcode");
            }
        }
    }
}