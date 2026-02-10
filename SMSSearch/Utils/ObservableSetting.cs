using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using SMS_Search.Services;

namespace SMS_Search.Utils
{
    public partial class ObservableSetting<T> : ObservableObject
    {
        private readonly ISettingsRepository _repository;
        private readonly string _section;
        private readonly string _key;
        private readonly Func<T, string> _serializer;
        private CancellationTokenSource? _debounceCts;

        [ObservableProperty]
        private T _value;

        [ObservableProperty]
        private bool _isSaved;

        [ObservableProperty]
        private bool _isSaving;

        public ObservableSetting(
            ISettingsRepository repository,
            string section,
            string key,
            T initialValue,
            Func<T, string>? serializer = null)
        {
            _repository = repository;
            _section = section;
            _key = key;
            _value = initialValue;
            _serializer = serializer ?? (v => v?.ToString() ?? "");
        }

        partial void OnValueChanged(T value)
        {
            QueueSave();
        }

        private async void QueueSave()
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            try
            {
                // Debounce
                await Task.Delay(500, token);
                if (token.IsCancellationRequested) return;

                IsSaving = true;
                IsSaved = false; // clear previous saved state if any

                // Save
                var serializedValue = _serializer(Value);
                await _repository.SaveAsync(_section, _key, serializedValue);

                if (token.IsCancellationRequested) return;

                IsSaving = false;
                IsSaved = true;

                // Auto-hide "Saved" after 2 seconds
                await Task.Delay(2000, token);
                if (token.IsCancellationRequested) return;

                IsSaved = false;
            }
            catch (OperationCanceledException)
            {
                // Ignored
            }
            catch (Exception)
            {
                // Handle error (logging?)
                IsSaving = false;
                // Maybe expose IsError property?
            }
        }
    }
}
