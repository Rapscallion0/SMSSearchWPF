using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using SMS_Search.Services;
using SMS_Search.Utils;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Controls;

namespace SMS_Search.ViewModels.Settings
{
    public partial class EditorSectionViewModel : SettingsSectionViewModel
    {
        private readonly ISettingsRepository _repository;
        private readonly IIntellisenseService _intellisenseService;

        public override string Title => "Editor";
        public override ControlTemplate Icon => (ControlTemplate)System.Windows.Application.Current.FindResource("Icon_Edit");

        public EditorSectionViewModel(ISettingsRepository repository, IIntellisenseService intellisenseService)
        {
            _repository = repository;
            _intellisenseService = intellisenseService;

            // Enable IntelliSense
            var enableIntellisenseStr = repository.GetValue(AppSettings.Sections.Editor, AppSettings.Keys.EnableIntellisense);
            EnableIntellisense = new ObservableSetting<bool>(
                repository, AppSettings.Sections.Editor, AppSettings.Keys.EnableIntellisense,
                enableIntellisenseStr != "0", // Default true
                v => v ? "1" : "0");

            EnableIntellisense.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<bool>.Value))
                {
                    _intellisenseService.IsEnabled = EnableIntellisense.Value;
                }
            };

            // Auto Trigger IntelliSense
            var autoTriggerStr = repository.GetValue(AppSettings.Sections.Editor, AppSettings.Keys.AutoTriggerIntellisense);
            AutoTriggerIntellisense = new ObservableSetting<bool>(
                repository, AppSettings.Sections.Editor, AppSettings.Keys.AutoTriggerIntellisense,
                autoTriggerStr != "0", // Default true
                v => v ? "1" : "0");

            AutoTriggerIntellisense.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<bool>.Value))
                {
                    _intellisenseService.AutoTriggerEnabled = AutoTriggerIntellisense.Value;
                }
            };

            // Standard IntelliSense
            var standardStr = repository.GetValue(AppSettings.Sections.Editor, AppSettings.Keys.IntellisenseStandard);
            StandardIntellisenseEnabled = new ObservableSetting<bool>(
                repository, AppSettings.Sections.Editor, AppSettings.Keys.IntellisenseStandard,
                standardStr != "0", // Default true
                v => v ? "1" : "0");

            StandardIntellisenseEnabled.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<bool>.Value))
                {
                    _intellisenseService.StandardEnabled = StandardIntellisenseEnabled.Value;
                }
            };

            // Functional IntelliSense
            var functionalStr = repository.GetValue(AppSettings.Sections.Editor, AppSettings.Keys.IntellisenseFunctional);
            FunctionalIntellisenseEnabled = new ObservableSetting<bool>(
                repository, AppSettings.Sections.Editor, AppSettings.Keys.IntellisenseFunctional,
                functionalStr != "0", // Default true
                v => v ? "1" : "0");

            FunctionalIntellisenseEnabled.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<bool>.Value))
                {
                    _intellisenseService.FunctionalEnabled = FunctionalIntellisenseEnabled.Value;
                }
            };

            // Full IntelliSense
            var fullStr = repository.GetValue(AppSettings.Sections.Editor, AppSettings.Keys.IntellisenseFull);
            FullIntellisenseEnabled = new ObservableSetting<bool>(
                repository, AppSettings.Sections.Editor, AppSettings.Keys.IntellisenseFull,
                fullStr != "0", // Default true
                v => v ? "1" : "0");

            FullIntellisenseEnabled.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<bool>.Value))
                {
                    _intellisenseService.FullEnabled = FullIntellisenseEnabled.Value;
                }
            };

            // Standard IntelliSense Auto
            var standardAutoStr = repository.GetValue(AppSettings.Sections.Editor, AppSettings.Keys.IntellisenseStandardAuto);
            StandardIntellisenseAuto = new ObservableSetting<bool>(
                repository, AppSettings.Sections.Editor, AppSettings.Keys.IntellisenseStandardAuto,
                standardAutoStr != "0", // Default true
                v => v ? "1" : "0");

            StandardIntellisenseAuto.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<bool>.Value))
                {
                    _intellisenseService.StandardAutoEnabled = StandardIntellisenseAuto.Value;
                }
            };

            // Functional IntelliSense Auto
            var functionalAutoStr = repository.GetValue(AppSettings.Sections.Editor, AppSettings.Keys.IntellisenseFunctionalAuto);
            FunctionalIntellisenseAuto = new ObservableSetting<bool>(
                repository, AppSettings.Sections.Editor, AppSettings.Keys.IntellisenseFunctionalAuto,
                functionalAutoStr != "0", // Default true
                v => v ? "1" : "0");

            FunctionalIntellisenseAuto.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<bool>.Value))
                {
                    _intellisenseService.FunctionalAutoEnabled = FunctionalIntellisenseAuto.Value;
                }
            };

            // Full IntelliSense Auto
            var fullAutoStr = repository.GetValue(AppSettings.Sections.Editor, AppSettings.Keys.IntellisenseFullAuto);
            FullIntellisenseAuto = new ObservableSetting<bool>(
                repository, AppSettings.Sections.Editor, AppSettings.Keys.IntellisenseFullAuto,
                fullAutoStr != "0", // Default true
                v => v ? "1" : "0");

            FullIntellisenseAuto.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<bool>.Value))
                {
                    _intellisenseService.FullAutoEnabled = FullIntellisenseAuto.Value;
                }
            };

            // Sql Font Family
            string? font = repository.GetValue(AppSettings.Sections.Editor, AppSettings.Keys.SqlFontFamily);
            if (string.IsNullOrEmpty(font)) font = "Consolas";

            SqlFontFamily = new ObservableSetting<string>(
                repository, AppSettings.Sections.Editor, AppSettings.Keys.SqlFontFamily,
                font,
                v => v);

            SqlFontFamily.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<string>.Value))
                {
                    SendFontMessage();
                }
            };

            // Sql Font Size
            int fontSize = 14;
            if (int.TryParse(repository.GetValue(AppSettings.Sections.Editor, AppSettings.Keys.SqlFontSize), out int fs))
                fontSize = fs;

            SqlFontSize = new ObservableSetting<int>(
                repository, AppSettings.Sections.Editor, AppSettings.Keys.SqlFontSize,
                fontSize,
                v => v.ToString());

            SqlFontSize.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<int>.Value))
                {
                    SendFontMessage();
                }
            };
        }

        private void SendFontMessage()
        {
            WeakReferenceMessenger.Default.Send(new SqlFontSettingsChangedMessage((SqlFontFamily.Value, SqlFontSize.Value)));
        }

        public ObservableSetting<bool> EnableIntellisense { get; }
        public ObservableSetting<bool> AutoTriggerIntellisense { get; }

        public ObservableSetting<bool> StandardIntellisenseEnabled { get; }
        public ObservableSetting<bool> FunctionalIntellisenseEnabled { get; }
        public ObservableSetting<bool> FullIntellisenseEnabled { get; }

        public ObservableSetting<bool> StandardIntellisenseAuto { get; }
        public ObservableSetting<bool> FunctionalIntellisenseAuto { get; }
        public ObservableSetting<bool> FullIntellisenseAuto { get; }


        public ObservableSetting<string> SqlFontFamily { get; }
        public ObservableSetting<int> SqlFontSize { get; }

        public IEnumerable<string> SystemFontFamilies => System.Windows.Media.Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(f => f);

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void IncreaseFontSize()
        {
            if (SqlFontSize.Value < 72)
            {
                SqlFontSize.Value++;
            }
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void DecreaseFontSize()
        {
            if (SqlFontSize.Value > 8)
            {
                SqlFontSize.Value--;
            }
        }


        public override bool Matches(string query)
        {
             if (base.Matches(query)) return true;

             // Check keywords
             if ("Intellisense".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Font".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("SQL".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Custom".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;

             return false;
        }
    }
}
