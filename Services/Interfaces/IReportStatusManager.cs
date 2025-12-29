namespace ATT_Wrapper.Services.Interfaces
    {
    public interface IReportStatusManager
        {
        void UpdateStatus(string uploaderName, string status);
        void ResetAll();
        }
    }