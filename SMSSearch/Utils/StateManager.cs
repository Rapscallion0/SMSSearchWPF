namespace SMS_Search.Utils
{
    public interface IStateService : IConfigService
    {
    }

    public class StateManager : ConfigManager, IStateService
    {
        public StateManager(string filePath) : base(filePath)
        {
        }
    }
}
