using System.Collections.Generic;

namespace OsEngine.Market.Servers.OKX.Entity
{
    public class OrderRequest<T>
    {
        public string id;
        public string op = "order";
        public List<T> args = new List<T>();
    }

    public class OrderRequestArgsSwap
    {
        public string side;
        public string posSide;
        public string instId;
        public string tdMode;
        public string ordType;
        public string sz;
        public string px;
        public string clOrdId;
        public bool reduceOnly;
        public string tag;
    }

    public class OrderRequestArgsSpot
    {
        public string side;
        public string instId;
        public string tdMode;
        public string ordType;
        public string sz;
        public string clOrdId;
    }
}
