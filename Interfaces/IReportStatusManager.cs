namespace ATT_Wrapper.Interfaces
    {
    public interface IReportStatusManager
        {
        void UpdateStatus(string uploaderName, string status);
        void ResetAll();
        }
    }