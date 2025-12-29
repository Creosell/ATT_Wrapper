namespace ATT_Wrapper.Services.Interfaces
    {
    public interface IConsoleRenderer
        {
        /// <summary>
        /// Добавляет текст в лог (с поддержкой цветов, если реализация позволяет).
        /// </summary>
        void AppendText(string text);
        }
    }