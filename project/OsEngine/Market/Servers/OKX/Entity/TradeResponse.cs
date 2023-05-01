using System.Collections.Generic;

namespace OsEngine.Market.Servers.OKX.Entity
{
    public class TradeResponse
    {
        public TradeResponseArgs arg;
        public List<TradeResponseData> data;
    }
    public class TradeResponseArgs
    {
        public string channel;
        public string instId;
    }

    public class TradeResponseData
    {
        public string instId;
        public string tradeId;
        public string px;
        public string sz;
        public string side;
        public string ts;
    }

}
