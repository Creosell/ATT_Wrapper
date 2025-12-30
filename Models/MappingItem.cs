using Newtonsoft.Json;

namespace ATT_Wrapper.Models
    {
    /// <summary>
    /// Модель данных для сопоставления строки лога с группой и именем проверки.
    /// </summary>
    public class MappingItem
        {
        [JsonProperty("pattern")]
        public string Pattern { get; set; }

        [JsonProperty("group")]
        public string Group { get; set; }

        [JsonProperty("ufn")]
        public string Ufn { get; set; }
        }
    }
