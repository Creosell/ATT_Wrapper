namespace ATT_Wrapper.Interfaces
    {
    /// <summary>
    /// Интерфейс для запуска команд Git.
    /// Позволяет подменять реальный вызов процесса на заглушку (Mock) в тестах.
    /// </summary>
    public interface IGitRunner
        {
        string Run(string arguments);
        }
    }