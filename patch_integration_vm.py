import re

with open('SMSSearch/ViewModels/Settings/IntegrationSectionViewModel.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Replace StartService
start_service_old = """        [RelayCommand]
        private async Task StartService()
        {
            StatusColor = System.Windows.Media.Brushes.Yellow;
            ServiceStatusText = "Starting...";
            try
            {
                string? fileName = Process.GetCurrentProcess().MainModule?.FileName;
                if (fileName != null)
                {
                    Process.Start(new ProcessStartInfo(fileName, "--listener") { UseShellExecute = true });
                    _logger.LogInfo("Service started manually.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to start service", ex);
                StatusColor = System.Windows.Media.Brushes.Red;
            }

            // Wait for polling to pick it up
            await Task.Delay(1000);
            CheckServiceStatus();
        }"""

start_service_new = """        [RelayCommand]
        private async Task StartService()
        {
            StatusColor = System.Windows.Media.Brushes.Yellow;
            ServiceStatusText = "Starting...";
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ((App)System.Windows.Application.Current).StartListener();
                });
                _logger.LogInfo("Service started manually.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to start service", ex);
                StatusColor = System.Windows.Media.Brushes.Red;
            }

            // Wait for polling to pick it up
            await Task.Delay(1000);
            CheckServiceStatus();
        }"""

content = content.replace(start_service_old, start_service_new)

# Replace StopService
stop_service_old = """        [RelayCommand]
        private void StopService()
        {
            StatusColor = System.Windows.Media.Brushes.Yellow;
            ServiceStatusText = "Stopping...";
            try
            {
                IntPtr hWnd = FindWindow(null, "SMS_Search_Listener_Hidden_Window");
                if (hWnd != IntPtr.Zero)
                {
                    GetWindowThreadProcessId(hWnd, out uint pid);
                    var proc = Process.GetProcessById((int)pid);
                    proc.Kill();
                    _logger.LogInfo("Service stopped manually.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to stop service", ex);
            }

            CheckServiceStatus();
        }"""

stop_service_new = """        [RelayCommand]
        private void StopService()
        {
            StatusColor = System.Windows.Media.Brushes.Yellow;
            ServiceStatusText = "Stopping...";
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ((App)System.Windows.Application.Current).StopListener();
                });
                _logger.LogInfo("Service stopped manually.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to stop service", ex);
            }

            CheckServiceStatus();
        }"""

content = content.replace(stop_service_old, stop_service_new)

# Replace CheckServiceStatus
check_status_old = """        private void CheckServiceStatus()
        {
            IntPtr hWnd = FindWindow(null, "SMS_Search_Listener_Hidden_Window");
            bool running = hWnd != IntPtr.Zero;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (running)
                {
                    ServiceStatusText = "Running";
                    StatusColor = System.Windows.Media.Brushes.Green;
                    ServiceWarningVisibility = System.Windows.Visibility.Collapsed;
                }
                else
                {
                    ServiceStatusText = "Stopped";
                    StatusColor = System.Windows.Media.Brushes.Red;
                    ServiceWarningVisibility = System.Windows.Visibility.Visible;
                }
            });
        }"""

check_status_new = """        private void CheckServiceStatus()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                bool running = App.IsListenerRunning;
                if (running)
                {
                    ServiceStatusText = "Running";
                    StatusColor = System.Windows.Media.Brushes.Green;
                    ServiceWarningVisibility = System.Windows.Visibility.Collapsed;
                }
                else
                {
                    ServiceStatusText = "Stopped";
                    StatusColor = System.Windows.Media.Brushes.Red;
                    ServiceWarningVisibility = System.Windows.Visibility.Visible;
                }
            });
        }"""

content = content.replace(check_status_old, check_status_new)

with open('SMSSearch/ViewModels/Settings/IntegrationSectionViewModel.cs', 'w', encoding='utf-8') as f:
    f.write(content)
print("Updated IntegrationSectionViewModel successfully")
