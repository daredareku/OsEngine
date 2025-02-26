﻿using Newtonsoft.Json;

namespace OsEngine.Market.Servers.GateIo.Futures.Response
{
    public partial class GfTick
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("create_time")]
        public long CreateTime { get; set; }

        [JsonProperty("contract")]
        public string Contract { get; set; }

        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("price")]
        public string Price { get; set; }
    }
}
