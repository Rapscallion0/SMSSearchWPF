using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMS_Search.Utils;
using System;
using System.Windows;

namespace SMS_Search.ViewModels
{
    public partial class EulaViewModel : ObservableObject
    {
        private readonly IConfigService _configService;

        public event Action? RequestClose;

        public EulaViewModel(IConfigService configService)
        {
            _configService = configService;
            EulaText = string.Join(Environment.NewLine + Environment.NewLine, new[]
            {
                "END-USER LICENSE AGREEMENT (EULA) — SMS SEARCH",
                "1. ACCEPTANCE & SCOPE\nBy installing, launching, or using SMS Search (\"the Software\"), you agree to be bound by the terms of this End-User License Agreement. If you do not agree, click Cancel to exit and discontinue use.",
                "2. PERMITTED USE (FREEWARE / OPEN-USE)\nSMS Search is provided free of charge for personal or commercial use. You may use the Software to inspect, search, and query database schemas and data on systems you are authorized to access.",
                "3. INDEPENDENT UTILITY (NO AFFILIATION)\nSMS Search is an independent utility and is not affiliated with, endorsed by, sponsored by, or supported by LOC Software or LOC SMS. Any references to LOC SMS table or dictionary structures are solely for interoperability.",
                "4. DATABASE ACCESS, CUSTOM SQL & IMPORTS\nSMS Search can connect to live SQL Server databases, execute Custom SQL queries, and perform SQL Import operations. You are solely responsible for:\n• Ensuring you have proper authorization to access target servers and databases;\n• Reviewing and verifying any Custom SQL or import scripts before execution;\n• Maintaining current backups of your databases prior to making any changes.",
                "5. USER-SUPPLIED DICTIONARY FILES (Fields.csv)\nIf you supply external dictionary files (such as Fields.csv, Fields2.csv, or Fields.es), SMS Search reads them in read-only mode. You are responsible for the accuracy, licensing, and suitability of any user-supplied files.",
                "6. LOCAL STORAGE & PRIVACY\nConnection settings, Windows DPAPI-encrypted credentials, query history, and diagnostic logs are stored locally on your machine. SMS Search does not collect or transmit telemetry; external network requests occur only if optional update checks are enabled.",
                "7. DISCLAIMER OF WARRANTY (\"AS IS\")\nTHERE IS NO WARRANTY FOR THE PROGRAM, TO THE EXTENT PERMITTED BY APPLICABLE LAW. EXCEPT WHEN OTHERWISE STATED IN WRITING, THE COPYRIGHT HOLDERS AND/OR OTHER PARTIES PROVIDE THE PROGRAM \"AS IS\" WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE. THE ENTIRE RISK AS TO THE QUALITY AND PERFORMANCE OF THE PROGRAM IS WITH YOU. SHOULD THE PROGRAM PROVE DEFECTIVE, YOU ASSUME THE COST OF ALL NECESSARY SERVICING, REPAIR, OR CORRECTION.",
                "8. LIMITATION OF LIABILITY\nIN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, DATA LOSS, DATABASE CORRUPTION, DOWNTIME, OR LOST PROFITS) ARISING IN ANY WAY OUT OF THE USE OF OR INABILITY TO USE THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE."
            });
        }

        [ObservableProperty]
        private string _eulaText = "";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
        private bool _isAccepted;

        [RelayCommand(CanExecute = nameof(CanAccept))]
        private void Accept()
        {
            _configService.SetValue(AppSettings.Sections.General, AppSettings.Keys.Eula, "1");
            _configService.Save();
            RequestClose?.Invoke();
        }

        private bool CanAccept() => IsAccepted;

        [RelayCommand]
        private void Cancel()
        {
            System.Windows.Application.Current.Shutdown();
        }
    }
}
