﻿using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Market.Servers.Bybit.Entities;
using OsEngine.Market.Servers.Bybit.Utilities;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.Bybit.EntityCreators
{
    public static class BybitCandlesCreator
    {
        public static List<Candle> Create(JToken data)
        {
            var candles = new List<Candle>();

            var JProperties = data.Children();

            foreach (var jProperty in JProperties)
            {
                Candle candle = new Candle();

                candle.TimeStart = Utils.LongToDateTime(jProperty.SelectToken("open_time").Value<long>());
                candle.Open = jProperty.SelectToken("open").Value<decimal>();
                candle.Close = jProperty.SelectToken("close").Value<decimal>();
                candle.Low = jProperty.SelectToken("low").Value<decimal>();
                candle.High = jProperty.SelectToken("high").Value<decimal>();
                candle.Volume = jProperty.SelectToken("volume").Value<decimal>();

                candles.Add(candle);
            }

            return candles;
        }



        public static List<Candle> GetCandleCollection(Client client, string security, string need_interval_for_query, int from, BybitServerRealization server)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();

            parameters.Add("symbol", security);
            parameters.Add("interval", need_interval_for_query);
            parameters.Add("limit", "200");
            parameters.Add("from", from.ToString());
            parameters.Add("recv_window", "90000000");

            object account_response = new object();

            if (client.FuturesMode == "Inverse")
                account_response = server.CreatePrivateGetQuery(client, "/v2/public/kline/list", parameters);

            if (client.FuturesMode == "USDT")
                account_response = server.CreatePrivateGetQuery(client, "/public/linear/kline", parameters);

            if (account_response == null)
            {
                return null;
            }

            string isSuccessfull = ((JToken)account_response).SelectToken("ret_msg").Value<string>();

            if (isSuccessfull == "OK")
            {
                var candles = BybitCandlesCreator.Create(((JToken)account_response).SelectToken("result"));

                return candles;
            }

            return null;
        }
    }
}
