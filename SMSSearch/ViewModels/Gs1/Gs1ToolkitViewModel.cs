using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMS_Search.Models.Gs1;
using SMS_Search.Services.Gs1;
using SMS_Search.Services;
using SMS_Search.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace SMS_Search.ViewModels.Gs1
{
    public partial class Gs1ToolkitViewModel : ObservableObject, INotifyDataErrorInfo
    {
        private readonly IGs1Repository _repository;
        private readonly IGs1Parser _parser;
        private readonly IGs1BarcodeService _barcodeService;
        private readonly IDialogService _dialogService;
        private readonly ILoggerService _logger;
        private readonly IClipboardService _clipboard;
        private readonly IStateService _stateService;
        private readonly ISettingsRepository _settingsRepository;
        private readonly string _historyFilePath;

        [ObservableProperty]
        private string _rawBarcode = "";

        [ObservableProperty]
        private string _detectedType = "Unknown";

        public ObservableCollection<Gs1BarcodeSegmentViewModel> BarcodeSegments { get; } = new ObservableCollection<Gs1BarcodeSegmentViewModel>();

        [ObservableProperty]
        private Gs1ParsedAiViewModel? _selectedAi;

        partial void OnSelectedAiChanged(Gs1ParsedAiViewModel? value)
        {
            UpdateSegmentSelection();
        }

        [RelayCommand]
        private void CopyCode()
        {
            string codeToCopy = string.Join("", ParsedAis.Where(a => a.Ai != "└─").Select(a => $"{a.Ai}{a.DraftValue}"));
            if (string.IsNullOrEmpty(codeToCopy))
            {
                _dialogService.ShowToast("No code to copy.", "Copy Barcode", SMS_Search.Views.ToastType.Warning);
                return;
            }
            _clipboard.SetText(codeToCopy);
            _dialogService.ShowToast("Barcode copied to clipboard.", "Copy Barcode", SMS_Search.Views.ToastType.Success);
        }

        private void UpdateSegmentSelection()
        {
            if (BarcodeSegments == null) return;
            foreach (var segment in BarcodeSegments)
            {
                segment.IsSelected = false;
            }

            if (SelectedAi == null) return;

            foreach (var segment in BarcodeSegments)
            {
                if (segment.AssociatedAi == SelectedAi)
                {
                    segment.IsSelected = true;
                }
            }
        }

        private void RebuildSegments()
        {
            var oldSegments = BarcodeSegments.ToList();
            BarcodeSegments.Clear();

            foreach (var ai in ParsedAis.Where(a => a.Ai != "└─"))
            {
                var aiSeg = new Gs1BarcodeSegmentViewModel
                {
                    Text = ai.Ai,
                    AssociatedAi = ai
                };
                aiSeg.HoverStarted = () =>
                {
                    ai.IsHovered = true;
                    IsAnySegmentHovered = true;
                };
                aiSeg.HoverEnded = () =>
                {
                    ai.IsHovered = false;
                    IsAnySegmentHovered = BarcodeSegments.Any(s => s.IsHovered);
                };
                BarcodeSegments.Add(aiSeg);

                if (ai.Ai == "8110" || ai.Ai == "8112")
                {
                    // Databar coupon sub-segments
                    foreach (var subAi in ParsedAis.Where(a => a.Ai == "└─"))
                    {
                        if (string.IsNullOrEmpty(subAi.DraftValue)) continue;

                        // User specifically asked for lengths of length fields
                        string segText = subAi.DraftValue;

                        var subSeg = new Gs1BarcodeSegmentViewModel
                        {
                            Text = segText,
                            AssociatedAi = subAi
                        };
                        subSeg.HoverStarted = () =>
                        {
                            subAi.IsHovered = true;
                            IsAnySegmentHovered = true;
                        };
                        subSeg.HoverEnded = () =>
                        {
                            subAi.IsHovered = false;
                            IsAnySegmentHovered = BarcodeSegments.Any(s => s.IsHovered);
                        };
                        BarcodeSegments.Add(subSeg);
                    }
                }
                else
                {
                    var valSeg = new Gs1BarcodeSegmentViewModel
                    {
                        Text = ai.DraftValue,
                        AssociatedAi = ai
                    };
                    valSeg.HoverStarted = () =>
                    {
                        ai.IsHovered = true;
                        IsAnySegmentHovered = true;
                    };
                    valSeg.HoverEnded = () =>
                    {
                        ai.IsHovered = false;
                        IsAnySegmentHovered = BarcodeSegments.Any(s => s.IsHovered);
                    };
                    BarcodeSegments.Add(valSeg);
                }
            }

            // Restore selection and hover states if possible
            UpdateSegmentSelection();
        }

        public ObservableCollection<string> AvailableTemplates { get; } = new ObservableCollection<string>
        {
            "GS1 Databar Coupon",
            "SSCC-18",
            "GTIN-14",
            "GS1-128 (GTIN + Attributes)"
        };

        [ObservableProperty]
        private string? _selectedTemplate;

        private bool _isUpdatingTemplateInternally;

        [ObservableProperty]
        private bool _isAddAiVisible;

        private void UpdateFilteredDefinitions()
        {
            FilteredDefinitions.Clear();

            if (SelectedTemplate == "GS1-128 (GTIN + Attributes)")
            {
                var filtered = AvailableDefinitions.Where(d =>
                    d.Ai != "00" &&
                    d.Ai != "01" &&
                    d.Ai != "8110" &&
                    d.Ai != "8112" &&
                    d.Ai != "└─");

                foreach (var def in filtered)
                {
                    FilteredDefinitions.Add(def);
                }
            }

            IsAddAiVisible = FilteredDefinitions.Count > 0;
            SelectedDefinitionToAdd = FilteredDefinitions.FirstOrDefault();
        }

        partial void OnSelectedTemplateChanged(string? value)
        {
            UpdateFilteredDefinitions();

            if (string.IsNullOrEmpty(value) || _isUpdatingTemplateInternally) return;

            // Clear the raw barcode and draft raw barcode first, so the ForceParse triggers don't happen
            // in an unexpected way when ParsedAis is manipulated.
            _isClearingValues = true;
            try
            {
                RawBarcode = "";
                DraftRawBarcode = "";
            }
            finally
            {
                _isClearingValues = false;
            }

            ParsedAis.Clear();
            if (value == "GS1 Databar Coupon")
            {
                var def = AvailableDefinitions.FirstOrDefault(d => d.Ai == "8110");
                if (def != null)
                {
                    AddEmptyAi(def, true);
                    AddEmptySubAi("Primary Company Prefix", true);
                    AddEmptySubAi("Offer Code", true);
                    AddEmptySubAi("Save Value", true);
                    AddEmptySubAi("Primary Purchase Requirement", true);
                    AddEmptySubAi("Primary Purchase Requirement Code", true);
                    AddEmptySubAi("Primary Purchase Family Code", true);

                    AddEmptySubAi("Data Field 0", false);
                    AddEmptySubAi("2nd Additional Purchase Rules Code", false);
                    AddEmptySubAi("2nd Purchase Requirement", false);
                    AddEmptySubAi("2nd Purchase Requirement Code", false);
                    AddEmptySubAi("2nd Purchase Family Code", false);
                    AddEmptySubAi("2nd Purchase Company Prefix", false);

                    AddEmptySubAi("3rd Purchase Requirement", false);
                    AddEmptySubAi("3rd Purchase Requirement Code", false);
                    AddEmptySubAi("3rd Purchase Family Code", false);
                    AddEmptySubAi("3rd Purchase Company Prefix", false);

                    AddEmptySubAi("Expiration Date", false);
                    AddEmptySubAi("Start Date", false);
                    AddEmptySubAi("Serial Number", false);
                    AddEmptySubAi("Retailer Company Prefix / GLN", false);

                    AddEmptySubAi("Save Value Code", false);
                    AddEmptySubAi("Applies to Which Item", false);
                    AddEmptySubAi("Store Coupon", false);
                    AddEmptySubAi("Don't Multiply Flag", false);
                }
            }
            else if (value == "SSCC-18")
            {
                var def = AvailableDefinitions.FirstOrDefault(d => d.Ai == "00");
                if (def != null) AddEmptyAi(def, true);
            }
            else if (value == "GTIN-14")
            {
                var def = AvailableDefinitions.FirstOrDefault(d => d.Ai == "01");
                if (def != null) AddEmptyAi(def, true);
            }
            else if (value == "GS1-128 (GTIN + Attributes)")
            {
                var def01 = AvailableDefinitions.FirstOrDefault(d => d.Ai == "01");
                if (def01 != null) AddEmptyAi(def01, true);

                var def10 = AvailableDefinitions.FirstOrDefault(d => d.Ai == "10");
                if (def10 != null) AddEmptyAi(def10, false);

                var def21 = AvailableDefinitions.FirstOrDefault(d => d.Ai == "21");
                if (def21 != null) AddEmptyAi(def21, false);
            }

            DetectedType = value;
            RawBarcode = ""; // Clear raw string when using template
        }

        public ObservableCollection<Gs1ParsedAiViewModel> ParsedAis { get; } = new ObservableCollection<Gs1ParsedAiViewModel>();
        public ObservableCollection<Gs1AiDefinition> AvailableDefinitions { get; } = new ObservableCollection<Gs1AiDefinition>();
        public ObservableCollection<Gs1AiDefinition> FilteredDefinitions { get; } = new ObservableCollection<Gs1AiDefinition>();
        public ObservableCollection<Gs1HistoryItem> History { get; } = new ObservableCollection<Gs1HistoryItem>();

        [ObservableProperty]
        private Gs1AiDefinition? _selectedDefinitionToAdd;

        [ObservableProperty]
        private bool _isHistoryPanelOpen;

        [ObservableProperty]
        private bool _isHistoryPanelPinned;

        [ObservableProperty]
        private double _historyPanelWidth;

        partial void OnIsHistoryPanelOpenChanged(bool value)
        {
            _stateService.SetValue("GS1", "HISTORY_PANEL_OPEN", value.ToString());
            _stateService.Save();
        }

        partial void OnIsHistoryPanelPinnedChanged(bool value)
        {
            _stateService.SetValue("GS1", "HISTORY_PANEL_PINNED", value.ToString());
            _stateService.Save();
        }

        partial void OnHistoryPanelWidthChanged(double value)
        {
            if (value >= 200)
            {
                _stateService.SetValue("GS1", "HISTORY_PANEL_WIDTH", value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                _stateService.Save();
            }
        }

        [RelayCommand]
        private void ToggleHistoryPanel()
        {
            IsHistoryPanelOpen = !IsHistoryPanelOpen;
        }

        [RelayCommand]
        private void ToggleHistoryPanelPinned()
        {
            IsHistoryPanelPinned = !IsHistoryPanelPinned;
            if (IsHistoryPanelPinned && !IsHistoryPanelOpen)
            {
                IsHistoryPanelOpen = true;
            }
        }

        public Gs1ToolkitViewModel(
            IGs1Repository repository,
            IGs1Parser parser,
            IGs1BarcodeService barcodeService,
            IDialogService dialogService,
            ILoggerService logger,
            IClipboardService clipboard,
            IStateService stateService,
            ISettingsRepository settingsRepository)
        {
            _repository = repository;
            _settingsRepository = settingsRepository;
            _parser = parser;
            _barcodeService = barcodeService;
            _dialogService = dialogService;
            _logger = logger;
            _clipboard = clipboard;
            _stateService = stateService;

            string baseDir = System.AppDomain.CurrentDomain.BaseDirectory;
            _historyFilePath = System.IO.Path.Combine(baseDir, "gs1-history.json");

            _ = InitializeAsync();
        }

        private void LoadPanelSettings()
        {
            if (bool.TryParse(_stateService.GetValue("GS1", "HISTORY_PANEL_OPEN"), out bool isOpen))
            {
                IsHistoryPanelOpen = isOpen;
            }
            else
            {
                IsHistoryPanelOpen = true; // default
            }

            if (bool.TryParse(_stateService.GetValue("GS1", "HISTORY_PANEL_PINNED"), out bool isPinned))
            {
                IsHistoryPanelPinned = isPinned;
            }
            else
            {
                IsHistoryPanelPinned = true; // default
            }

            if (double.TryParse(_stateService.GetValue("GS1", "HISTORY_PANEL_WIDTH"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double width) && width >= 200)
            {
                HistoryPanelWidth = width;
            }
            else
            {
                HistoryPanelWidth = 300; // default
            }
        }

        private async Task InitializeAsync()
        {
            try
            {
                LoadPanelSettings();
                await LoadHistoryAsync();

                var defs = await _repository.GetAiDefinitionsAsync();
                foreach (var def in defs)
                {
                    AvailableDefinitions.Add(def);
                }

                if (string.IsNullOrEmpty(SelectedTemplate))
                {
                    SelectedTemplate = "GS1 Databar Coupon";
                }

                _logger.LogInfo("GS1 Toolkit initialized successfully.");
            }
            catch (System.Exception ex)
            {
                _logger.LogError("Failed to initialize GS1 Toolkit", ex);
                _dialogService.ShowToast("Failed to load GS1 definitions.", "GS1 Toolkit", SMS_Search.Views.ToastType.Error);
            }
            finally
            {
                UpdateFilteredDefinitions();
            }
        }

        [ObservableProperty]
        private bool _isAnySegmentHovered;

        [ObservableProperty]
        private string _draftRawBarcode = "";

        [ObservableProperty]
        private bool _isRawBarcodeModified;

        partial void OnDraftRawBarcodeChanged(string value)
        {
            IsRawBarcodeModified = value != RawBarcode;
        }

        [RelayCommand]
        private void CommitRawBarcode()
        {
            _logger.LogInfo($"Committing raw barcode. Length: {DraftRawBarcode.Length}");
            if (RawBarcode == DraftRawBarcode)
            {
                ForceParse(DraftRawBarcode);
            }
            else
            {
                RawBarcode = DraftRawBarcode;
            }
            IsRawBarcodeModified = false;
        }

        [RelayCommand]
        private void RevertRawBarcode()
        {
            DraftRawBarcode = RawBarcode;
            IsRawBarcodeModified = false;
        }

        private bool _isUpdatingFromSubAi;
        private bool _isClearingValues;

        partial void OnRawBarcodeChanged(string value)
        {
            if (_isClearingValues) return;

            if (!_isUpdatingFromSubAi)
            {
                DraftRawBarcode = value;
                IsRawBarcodeModified = false;
            }

            ForceParse(value);
        }

        private void ForceParse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                foreach (var ai in ParsedAis)
                {
                    ai.PropertyChanged -= OnParsedAiPropertyChanged;
                }
                ParsedAis.Clear();
                DetectedType = "Unknown";
                return;
            }

            // Save current selection to restore after re-parse
            string? selectedAiCode = SelectedAi?.Ai;
            string? selectedAiTitle = SelectedAi?.Title;

            var result = _parser.Parse(value, AvailableDefinitions.ToList());

            foreach (var ai in ParsedAis)
            {
                ai.PropertyChanged -= OnParsedAiPropertyChanged;
            }
            ParsedAis.Clear();

            foreach (var ai in result.ParsedAis)
            {
                var vm = new Gs1ParsedAiViewModel(ai, _dialogService);
                vm.PropertyChanged += OnParsedAiPropertyChanged;
                ParsedAis.Add(vm);
            }
            DetectedType = _parser.DetectType(result.ParsedAis);

            _isUpdatingTemplateInternally = true;
            SelectedTemplate = DetectedType;
            _isUpdatingTemplateInternally = false;
            UpdateFilteredDefinitions();

            ApplyRequiredStatusByTemplate(DetectedType);

            // Restore selection if possible
            if (!string.IsNullOrEmpty(selectedAiCode))
            {
                SelectedAi = ParsedAis.FirstOrDefault(a => a.Ai == selectedAiCode && a.Title == selectedAiTitle)
                             ?? ParsedAis.FirstOrDefault(a => a.Ai == selectedAiCode);
            }

            RebuildSegments();

            if (!result.IsValid)
            {
                _logger.LogWarning($"GS1 Parsing Error: {result.ErrorMessage}");
                _dialogService.ShowToast(result.ErrorMessage, "GS1 Parsing Error", SMS_Search.Views.ToastType.Warning);
            }
            else
            {
                _logger.LogInfo($"Successfully parsed GS1 barcode. Detected type: {DetectedType}. Extracted {result.ParsedAis.Count} AIs.");
            }
        }

        private void ApplyRequiredStatusByTemplate(string templateName)
        {
            if (templateName == "GS1 Databar Coupon")
            {
                var requiredTitles = new[] { "Primary Company Prefix", "Offer Code", "Save Value", "Primary Purchase Requirement", "Primary Purchase Requirement Code", "Primary Purchase Family Code" };
                foreach (var ai in ParsedAis)
                {
                    if (ai.Ai == "8110" || ai.Ai == "8112" || requiredTitles.Contains(ai.Title))
                    {
                        ai.IsRequired = true;
                    }
                }
            }
            else if (templateName == "SSCC-18" || templateName == "GTIN-14")
            {
                foreach (var ai in ParsedAis) ai.IsRequired = true;
            }
            else if (templateName == "GS1-128 (GTIN + Attributes)")
            {
                var def01 = ParsedAis.FirstOrDefault(a => a.Ai == "01");
                if (def01 != null) def01.IsRequired = true;
            }
        }

        private void OnParsedAiPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isUpdatingFromSubAi) return;

            if (e.PropertyName == nameof(Gs1ParsedAiViewModel.RawValue) && sender is Gs1ParsedAiViewModel vm)
            {
                if (vm.Ai == "└─")
                {
                    UpdateDatabarCouponFromSubAis();
                }
                else if (vm.Ai == "8110" || vm.Ai == "8112")
                {
                    // If the user edits the main AI, update RawBarcode to trigger a re-parse and refresh sub AIs
                    _isUpdatingFromSubAi = true;
                    try
                    {
                        RawBarcode = string.Join("", ParsedAis.Where(a => a.Ai != "└─").Select(a => (a.Ai == "8110" || a.Ai == "8112") ? $"{a.Ai}{a.RawValue}" : $"({a.Ai}){a.RawValue}"));
                    }
                    finally
                    {
                        _isUpdatingFromSubAi = false;
                    }
                }
                RebuildSegments();

                // Pulse segment if it exists
                if (!_isClearingValues)
                {
                    foreach (var seg in BarcodeSegments.Where(s => s.AssociatedAi == vm))
                    {
                        seg.PulseAnimation();
                    }
                }
            }
        }

        private void UpdateDatabarCouponFromSubAis()
        {
            var mainAi = ParsedAis.FirstOrDefault(a => a.Ai == "8110" || a.Ai == "8112");
            if (mainAi == null) return;

            // Recalculate and pulse animations for VLI fields
            void UpdateVli(string vliTitle, int newVliValue)
            {
                var vliVm = ParsedAis.FirstOrDefault(a => a.Ai == "└─" && a.Title == vliTitle);
                if (vliVm != null)
                {
                    string newValStr = newVliValue.ToString();
                    if (vliVm.RawValue != newValStr)
                    {
                        vliVm.RawValue = newValStr;
                        vliVm.DraftValue = newValStr;
                        PulseAnimation(vliVm);
                    }
                }
            }

            int GetVli(string value)
            {
                if (string.IsNullOrEmpty(value)) return 0;
                int len = value.Length - 6;
                return len < 0 ? 0 : len;
            }

            string PadVliField(string? value)
            {
                if (string.IsNullOrEmpty(value)) return new string('0', 6);
                if (value.Length < 6) return value.PadLeft(6, '0');
                return value;
            }

            UpdateVli("Primary Company Prefix Length", GetVli(PadVliField(ParsedAis.FirstOrDefault(a => a.Title == "Primary Company Prefix")?.RawValue)));
            UpdateVli("Save Value Length", (ParsedAis.FirstOrDefault(a => a.Title == "Save Value")?.RawValue ?? "").Length);
            UpdateVli("Primary Purchase Requirement Length", (ParsedAis.FirstOrDefault(a => a.Title == "Primary Purchase Requirement")?.RawValue ?? "").Length);

            var p2Req = ParsedAis.FirstOrDefault(a => a.Title == "2nd Purchase Requirement");
            if (p2Req != null) UpdateVli("2nd Purchase Requirement Length", (p2Req.RawValue ?? "").Length);

            var p2Cp = ParsedAis.FirstOrDefault(a => a.Title == "2nd Purchase Company Prefix");
            if (p2Cp != null)
            {
                if (p2Cp.RawValue == "N/A" || string.IsNullOrEmpty(p2Cp.RawValue)) UpdateVli("2nd Purchase Company Prefix Length", 9);
                else UpdateVli("2nd Purchase Company Prefix Length", GetVli(PadVliField(p2Cp.RawValue)));
            }

            var p3Req = ParsedAis.FirstOrDefault(a => a.Title == "3rd Purchase Requirement");
            if (p3Req != null) UpdateVli("3rd Purchase Requirement Length", (p3Req.RawValue ?? "").Length);

            var p3Cp = ParsedAis.FirstOrDefault(a => a.Title == "3rd Purchase Company Prefix");
            if (p3Cp != null)
            {
                if (p3Cp.RawValue == "N/A" || string.IsNullOrEmpty(p3Cp.RawValue)) UpdateVli("3rd Purchase Company Prefix Length", 9);
                else UpdateVli("3rd Purchase Company Prefix Length", GetVli(PadVliField(p3Cp.RawValue)));
            }

            var serial = ParsedAis.FirstOrDefault(a => a.Title == "Serial Number");
            if (serial != null) UpdateVli("Serial Number Length", GetVli(PadVliField(serial.RawValue)));

            string newRawValue = Gs1DatabarCouponEncoder.Encode(ParsedAis);

            _isUpdatingFromSubAi = true;
            try
            {
                mainAi.RawValue = newRawValue;

                // Also update the full RawBarcode to reflect changes
                RawBarcode = string.Join("", ParsedAis.Where(a => a.Ai != "└─").Select(a => (a.Ai == "8110" || a.Ai == "8112") ? $"{a.Ai}{a.RawValue}" : $"({a.Ai}){a.RawValue}"));
            }
            finally
            {
                _isUpdatingFromSubAi = false;
            }
        }

        private async void PulseAnimation(Gs1ParsedAiViewModel vm)
        {
            vm.IsAnimatingUpdate = true;
            // 250ms * 2 (forward + backward) * 2 repeats = 1000ms. Add a small buffer.
            await Task.Delay(1100);
            vm.IsAnimatingUpdate = false;
        }

        private void AddEmptyAi(Gs1AiDefinition definition, bool isRequired = false)
        {
            var vm = new Gs1ParsedAiViewModel(new Gs1ParsedAi
            {
                Ai = definition.Ai,
                Definition = definition,
                RawValue = "",
                IsValid = true
            }, _dialogService);
            vm.IsRequired = isRequired;
            vm.PropertyChanged += OnParsedAiPropertyChanged;
            ParsedAis.Add(vm);
            RebuildSegments();
        }

        private void AddEmptySubAi(string title, bool isRequired = false)
        {
            var def = AvailableDefinitions.FirstOrDefault(d => d.Ai == "└─" && d.Title == title)
                      ?? new Gs1AiDefinition { Title = title, Ai = "└─" };

            var vm = new Gs1ParsedAiViewModel(new Gs1ParsedAi
            {
                Ai = "└─",
                Definition = def,
                RawValue = "",
                IsValid = true
            }, _dialogService);
            vm.IsRequired = isRequired;
            vm.PropertyChanged += OnParsedAiPropertyChanged;
            ParsedAis.Add(vm);
            RebuildSegments();
        }

        [RelayCommand]
        private void AddAi()
        {
            if (SelectedDefinitionToAdd != null)
            {
                _logger.LogInfo($"Adding new AI manually: {SelectedDefinitionToAdd.Ai}");
                AddEmptyAi(SelectedDefinitionToAdd);
            }
        }

        [RelayCommand]
        private void ClearValues()
        {
            _logger.LogInfo("Clearing all AI values.");
            _isClearingValues = true;
            try
            {
                foreach (var ai in ParsedAis)
                {
                    ai.RawValue = "";
                    ai.DraftValue = "";
                    ai.IsModified = false;
                }

                // Also update the full RawBarcode to reflect changes
                RawBarcode = string.Join("", ParsedAis.Where(a => a.Ai != "└─").Select(a => (a.Ai == "8110" || a.Ai == "8112") ? $"{a.Ai}{a.RawValue}" : $"({a.Ai}){a.RawValue}"));
            }
            finally
            {
                _isClearingValues = false;
            }
            RebuildSegments();
        }

        [RelayCommand]
        private void ViewBarcode()
        {
            string data = string.Join("", ParsedAis.Where(a => a.Ai != "└─").Select(a => (a.Ai == "8110" || a.Ai == "8112") ? $"{a.Ai}{a.RawValue}" : $"({a.Ai}){a.RawValue}"));
            if (string.IsNullOrWhiteSpace(data))
            {
                _dialogService.ShowToast("No data to encode. Please enter barcode values.", "View Barcode", SMS_Search.Views.ToastType.Warning);
                return;
            }

            var parsedModels = ParsedAis.Select(vm => vm.Model).ToList();
            var vmInstance = new Gs1BarcodeWindowViewModel(data, _barcodeService, _clipboard, _dialogService, _logger, parsedModels);
            var window = new SMS_Search.Views.Gs1.Gs1BarcodeWindow
            {
                DataContext = vmInstance
            };

            // Set owner to the active window for proper centering
            window.Owner = System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive);
            window.ShowDialog();

            AddToHistory(data);
        }

        private void AddToHistory(string formattedValue)
        {
            var rawValueToSave = string.Join("", ParsedAis.Where(a => a.Ai != "└─").Select(a => (a.Ai == "8110" || a.Ai == "8112") ? $"{a.Ai}{a.RawValue}" : $"({a.Ai}){a.RawValue}"));

            var existingItem = History.FirstOrDefault(h => h.RawValue == rawValueToSave);
            if (existingItem != null)
            {
                History.Remove(existingItem);
            }

            var item = new Gs1HistoryItem
            {
                RawValue = rawValueToSave,
                FormattedValue = formattedValue,
                DetectedType = DetectedType,
                Timestamp = System.DateTime.Now,
                OriginalAi = ParsedAis.FirstOrDefault(a => a.Ai == "8110" || a.Ai == "8112")?.Ai ?? ""
            };

            History.Insert(0, item);

            // Limit history size in memory
            while (History.Count > 50)
            {
                History.RemoveAt(History.Count - 1);
            }

            // Save to disk
            _ = SaveHistoryAsync();
        }

        [RelayCommand]
        private void LoadHistoryItem(Gs1HistoryItem item)
        {
            if (item != null)
            {
                _logger.LogInfo($"Loading history item. Type: {item.DetectedType}");
                // The parser now natively supports injecting parentheses for raw strings starting with 8110 or 8112.
                // We can just pass the raw string and let the parser handle it.
                RawBarcode = item.RawValue;
            }
        }

        [RelayCommand]
        private void DeleteHistoryItem(Gs1HistoryItem item)
        {
            if (item != null && History.Contains(item))
            {
                History.Remove(item);
                _ = SaveHistoryAsync();
            }
        }

        [RelayCommand]
        private void ClearHistory()
        {
            if (History.Count > 0)
            {
                History.Clear();
                _ = SaveHistoryAsync();
            }
        }

        private async Task LoadHistoryAsync()
        {
            if (!System.IO.File.Exists(_historyFilePath)) return;

            try
            {
                string json = await System.IO.File.ReadAllTextAsync(_historyFilePath);
                var items = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<Gs1HistoryItem>>(json);
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        History.Add(item);
                    }
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError("Failed to load GS1 history.", ex);
            }
        }

        private async Task SaveHistoryAsync()
        {
            try
            {
                string json = System.Text.Json.JsonSerializer.Serialize(History.ToList(), new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await System.IO.File.WriteAllTextAsync(_historyFilePath, json);
            }
            catch (System.Exception ex)
            {
                _logger.LogError("Failed to save GS1 history.", ex);
            }
        }

        // INotifyDataErrorInfo implementation
        public event System.EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged
        {
            add { }
            remove { }
        }
        public System.Collections.IEnumerable GetErrors(string? propertyName) => System.Linq.Enumerable.Empty<object>();
        public bool HasErrors => false;
    }

    public partial class Gs1ParsedAiViewModel : ObservableObject, INotifyDataErrorInfo
    {
        private readonly Gs1ParsedAi _model;
        private readonly SMS_Search.Services.IDialogService? _dialogService;

        public Gs1ParsedAiViewModel(Gs1ParsedAi model, SMS_Search.Services.IDialogService? dialogService = null)
        {
            _model = model;
            _dialogService = dialogService;
            RawValue = model.RawValue;
            DraftValue = model.RawValue;
        }

        public Gs1ParsedAi Model => _model;
        public string Ai => _model.Ai;
        public string Title => _model.Definition?.Title ?? "Unknown";
        public string Description => _model.Definition?.Description ?? "";
        public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
        public string DataType => _model.Definition?.DataType ?? "";
        public int MinLength => _model.Definition?.MinLength ?? 0;
        public int MaxLength => _model.Definition?.MaxLength ?? 0;

        public string ControlType => _model.Definition?.ControlType ?? "Text";
        public bool IsReadOnly => _model.Definition?.IsReadOnly ?? false;
        public System.Collections.Generic.List<Gs1AiOption>? Options => _model.Definition?.Options;

        [ObservableProperty]
        private string _rawValue;

        [ObservableProperty]
        private string _draftValue;

        [ObservableProperty]
        private bool _isModified;

        [ObservableProperty]
        private bool _isRequired;

        [ObservableProperty]
        private bool _boolValue;

        [ObservableProperty]
        private bool _isAnimatingUpdate;

        [ObservableProperty]
        private bool _isHovered;

        public bool IsRootComplexAi => Ai == "8110" || Ai == "8112";
        public bool IsEffectivelyReadOnly => IsReadOnly || IsRootComplexAi;

        public string DisplayValue => IsRootComplexAi ? "(-- See Above --)" : DraftValue;

        partial void OnDraftValueChanged(string value)
        {
            IsModified = value != RawValue;
            if (ControlType == "CheckBox")
            {
                bool newBoolVal = value == "1";
                if (BoolValue != newBoolVal)
                {
                    BoolValue = newBoolVal;
                }
            }
            Validate();
        }

        partial void OnBoolValueChanged(bool value)
        {
            string newDraftVal = value ? "1" : "0";
            if (DraftValue != newDraftVal)
            {
                DraftValue = newDraftVal;
                CommitCommand.Execute(null); // Auto commit checkbox toggles
            }
        }

        partial void OnRawValueChanged(string value)
        {
            _model.RawValue = value;
            if (DraftValue != value)
            {
                DraftValue = value;
            }
            OnPropertyChanged(nameof(DisplayValue));
        }

        [RelayCommand]
        public void Commit()
        {
            Validate();
            if (HasErrors)
            {
                if (_dialogService != null)
                {
                    var errors = GetErrors(nameof(DraftValue)).Cast<string>();
                    string msg = string.Join("\n", errors.Select(e => $"[{Title}] {e}"));
                    _dialogService.ShowToast(msg, "Validation Error", SMS_Search.Views.ToastType.Warning);
                }
                return;
            }

            if (IsModified)
            {
                RawValue = DraftValue;
                IsModified = false;
            }
        }

        [RelayCommand]
        public void Revert()
        {
            DraftValue = RawValue;
            IsModified = false;
            Validate();
        }

        public void CommitSilently()
        {
             CommitCommand.Execute(null);
        }

        private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> _errors = new();

        private void Validate()
        {
            _errors.Clear();
            if (_model.Definition != null)
            {
                if (!IsRequired && string.IsNullOrEmpty(DraftValue))
                {
                    // If it's optional and empty, it's valid. No length checks needed.
                }
                else
                {
                    if (DraftValue.Length < _model.Definition.MinLength)
                    {
                        _errors[nameof(DraftValue)] = new System.Collections.Generic.List<string> { $"Minimum length is {_model.Definition.MinLength}, {DraftValue.Length} provided" };
                    }
                    else if (DraftValue.Length > _model.Definition.MaxLength)
                    {
                        _errors[nameof(DraftValue)] = new System.Collections.Generic.List<string> { $"Maximum length is {_model.Definition.MaxLength}, {DraftValue.Length} provided" };
                    }

                    string dataType = _model.Definition.DataType ?? "";
                    if (dataType.Contains("N") && !dataType.Contains("X"))
                    {
                        if (!System.Text.RegularExpressions.Regex.IsMatch(DraftValue, "^[0-9]+$"))
                        {
                            if (!_errors.ContainsKey(nameof(DraftValue)))
                                _errors[nameof(DraftValue)] = new System.Collections.Generic.List<string>();
                            _errors[nameof(DraftValue)].Add("Value must be numeric.");
                        }
                    }
                    else if (System.Text.RegularExpressions.Regex.IsMatch(DraftValue, @"\s"))
                    {
                        if (!_errors.ContainsKey(nameof(DraftValue)))
                            _errors[nameof(DraftValue)] = new System.Collections.Generic.List<string>();
                        _errors[nameof(DraftValue)].Add("Value cannot contain whitespace.");
                    }
                }
            }
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(DraftValue)));
        }

        public event System.EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public System.Collections.IEnumerable GetErrors(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName) || !_errors.ContainsKey(propertyName))
            {
                return System.Linq.Enumerable.Empty<string>();
            }
            return _errors[propertyName];
        }

        public bool HasErrors => _errors.Count > 0;
    }
}
