using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using SMS_Search.Services;
using SMS_Search.Utils;
using System;
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
            var enableIntellisenseStr = repository.GetValue("GENERAL", "ENABLE_INTELLISENSE");
            EnableIntellisense = new ObservableSetting<bool>(
                repository, "GENERAL", "ENABLE_INTELLISENSE",
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
            var autoTriggerStr = repository.GetValue("GENERAL", "AUTO_TRIGGER_INTELLISENSE");
            AutoTriggerIntellisense = new ObservableSetting<bool>(
                repository, "GENERAL", "AUTO_TRIGGER_INTELLISENSE",
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
            var standardStr = repository.GetValue("GENERAL", "INTELLISENSE_STANDARD");
            StandardIntellisenseEnabled = new ObservableSetting<bool>(
                repository, "GENERAL", "INTELLISENSE_STANDARD",
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
            var functionalStr = repository.GetValue("GENERAL", "INTELLISENSE_FUNCTIONAL");
            FunctionalIntellisenseEnabled = new ObservableSetting<bool>(
                repository, "GENERAL", "INTELLISENSE_FUNCTIONAL",
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
            var fullStr = repository.GetValue("GENERAL", "INTELLISENSE_FULL");
            FullIntellisenseEnabled = new ObservableSetting<bool>(
                repository, "GENERAL", "INTELLISENSE_FULL",
                fullStr != "0", // Default true
                v => v ? "1" : "0");

            FullIntellisenseEnabled.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<bool>.Value))
                {
                    _intellisenseService.FullEnabled = FullIntellisenseEnabled.Value;
                }
            };

            // Select Custom SQL on Build
            var selectCustomSqlStr = repository.GetValue("GENERAL", "SELECT_CUSTOM_SQL_ON_BUILD");
            SelectCustomSqlOnBuild = new ObservableSetting<bool>(
                repository, "GENERAL", "SELECT_CUSTOM_SQL_ON_BUILD",
                selectCustomSqlStr != "0", // Default true
                v => v ? "1" : "0");

            // Sql Font Family
            string? font = repository.GetValue("GENERAL", "SQL_FONT_FAMILY");
            if (string.IsNullOrEmpty(font)) font = "Consolas";

            SqlFontFamily = new ObservableSetting<string>(
                repository, "GENERAL", "SQL_FONT_FAMILY",
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
            if (int.TryParse(repository.GetValue("GENERAL", "SQL_FONT_SIZE"), out int fs))
                fontSize = fs;

            SqlFontSize = new ObservableSetting<int>(
                repository, "GENERAL", "SQL_FONT_SIZE",
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

        public ObservableSetting<bool> SelectCustomSqlOnBuild { get; }
        public ObservableSetting<string> SqlFontFamily { get; }
        public ObservableSetting<int> SqlFontSize { get; }

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
