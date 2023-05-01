using Newtonsoft.Json;

namespace OsEngine.Market.Servers.Huobi
{
    public class PingMessage
    {
        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("data")]
        public PingData Data { get; set; }
    }

    public class PingData
    {
        [JsonProperty("ts")]
        public long TimeStamp { get; set; }
    }
}
