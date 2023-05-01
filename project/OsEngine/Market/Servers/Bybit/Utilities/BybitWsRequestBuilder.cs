﻿using OsEngine.Market.Servers.Bybit.Entities;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers.Bybit.Utilities
{
    public static class BybitWsRequestBuilder
    {
        public static string GetAuthRequest(Client client)
        {
            var now_UTC = TimeManager.GetExchangeTime("UTC");
            var expires = Utils.GetMillisecondsFromEpochStart(now_UTC) + 5000;
            var signature = BybitSigner.CreateSignature(client, "GET/realtime" + expires.ToString());
            var sign = $"{{\"op\":\"auth\",\"args\":[\"{client.ApiKey}\",\"{expires}\", \"{signature}\"]}}";
            return sign;
        }

        public static string GetSubscribeRequest(string channel)
        {
            return $"{{\"op\":\"subscribe\",\"args\":[\"{channel}\"]}}";
        }

        public static string GetPingRequest()
        {
            return $"{{\"op\": \"ping\"}}";
        }
    }
}
