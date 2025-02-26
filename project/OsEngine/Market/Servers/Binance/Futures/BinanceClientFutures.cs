﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Binance.Futures.Entity;
using OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using WebSocket4Net;
using TradeResponse = OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity.TradeResponse;

namespace OsEngine.Market.Servers.Binance.Futures
{
    public class BinanceClientFutures
    {
        public BinanceClientFutures(string pubKey, string secKey)
        {
            ApiKey = pubKey;
            SecretKey = secKey;
        }

        public string ApiKey;
        public string SecretKey;

        public FuturesType futures_type;

        public string _baseUrl = "https://fapi.binance.com";
        public string wss_point = "wss://fstream.binance.com";
        public string type_str_selector = "fapi";

        /// <summary>
        /// connecto to the exchange
        /// установить соединение с биржей 
        /// </summary>
        public void Connect()
        {
            if (string.IsNullOrEmpty(ApiKey) ||
                string.IsNullOrEmpty(SecretKey))
            {
                return;
            }

            // check server availability for HTTP communication with it / проверяем доступность сервера для HTTP общения с ним
            Uri uri = new Uri(_baseUrl + "/" + type_str_selector + "/v1/time");
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
                HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            }
            catch (Exception exception)
            {
                SendLogMessage("Сервер не доступен. Отсутствует интернет. ", LogMessageType.Error);
                return;
            }

            IsConnected = true;

            if (Connected != null)
            {
                Connected();
            }

            CreateDataStreams();

            _timeStart = DateTime.Now;
        }

        private string _listenKey = "";

        private WebSocket _socketClient;

        private void CreateDataStreams()
        {
            try
            {
                _listenKey = CreateListenKey();
                string urlStr = wss_point + "/ws/" + _listenKey;

                _socketClient = new WebSocket(urlStr);
                _socketClient.Opened += Connect;
                _socketClient.Closed += Disconnect;
                _socketClient.Error += WsError;
                _socketClient.MessageReceived += UserDataMessageHandler;
                _socketClient.Open();

                _wsStreams.Add("userDataStream", _socketClient);

                Thread keepalive = new Thread(KeepaliveUserDataStream);
                keepalive.CurrentCulture = new CultureInfo("ru-RU");
                keepalive.IsBackground = true;
                keepalive.Start();

                Thread converter = new Thread(Converter);
                converter.CurrentCulture = new CultureInfo("ru-RU");
                converter.IsBackground = true;
                converter.Start();

                Thread converterUserData = new Thread(ConverterUserData);
                converterUserData.CurrentCulture = new CultureInfo("ru-RU");
                converterUserData.IsBackground = true;
                converterUserData.Start();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message, LogMessageType.Connect);
            }

        }

        /// <summary>
        /// requesting a new listenKey for web socket from Binance via sending HTTP request
        /// запрвшиваем новый listenKey для веб сокета от Binance через отправку делаем HTTP запроса
        /// </summary>
        public void RenewListenKey()
        {
            try
            {
                _listenKey = CreateListenKey();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message, LogMessageType.Connect);
            }
        }

        /// <summary>
        /// sending HTTP request to Binance to create and get new listenKey for web socket connection
        /// делаем HTTP запрос на Binance чтобы создать и получить listenKey для веб сокета
        /// </summary>
        private string CreateListenKey()
        {
            string createListenKeyUrl = String.Format("/{0}/v1/listenKey", type_str_selector);
            var createListenKeyResult = CreateQueryNoLock(Method.POST, createListenKeyUrl, null, false);
            return JsonConvert.DeserializeAnonymousType(createListenKeyResult, new ListenKey()).listenKey;
        }

        /// <summary>
        /// bring the program to the start. Clear all objects involved in connecting to the server
        /// привести программу к моменту запуска. Очистить все объекты участвующие в подключении к серверу
        /// </summary>
        public void Dispose()
        {
            try
            {
                foreach (var ws in _wsStreams)
                {
                    ws.Value.Opened -= new EventHandler(Connect);
                    ws.Value.Closed -= new EventHandler(Disconnect);
                    ws.Value.Error -= new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>(WsError);
                    ws.Value.MessageReceived -= new EventHandler<MessageReceivedEventArgs>(GetRes);

                    ws.Value.Close();
                    ws.Value.Dispose();
                }
            }
            catch
            {
                // ignore
            }

            try
            {
                if (_socketClient != null)
                {
                    _socketClient.Opened -= Connect;
                    _socketClient.Closed -= Disconnect;
                    _socketClient.Error -= WsError;
                    _socketClient.MessageReceived -= UserDataMessageHandler;
                    _socketClient.Close();
                    //_socketClient.Dispose();
                    _socketClient = null;
                }
            }
            catch
            {
                // ignore
            }

            IsConnected = false;

            if (Disconnected != null)
            {
                Disconnected();
            }

            _isDisposed = true;
        }

        /// <summary>
        /// there was a request to clear the object
        /// произошёл запрос на очистку объекта
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// queue of new messages from the exchange server
        /// очередь новых сообщений, пришедших с сервера биржи
        /// </summary>
        private ConcurrentQueue<BinanceUserMessage> _newUserDataMessage = new ConcurrentQueue<BinanceUserMessage>();

        /// <summary>
        /// user data handler
        /// обработчик пользовательских данных
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserDataMessageHandler(object sender, MessageReceivedEventArgs e)
        {
            if (_isDisposed)
            {
                return;
            }
            BinanceUserMessage message = new BinanceUserMessage();
            message.MessageStr = e.Message;
            _newUserDataMessage.Enqueue(message);
        }

        /// <summary>
        /// close user data stream
        /// закрыть поток пользовательских данных
        /// </summary>
        private void CloseUserDataStream()
        {
            if (_listenKey != "")
            {
                CreateQuery(Method.DELETE, "api/v1/userDataStream", new Dictionary<string, string>() { { "listenKey=", _listenKey } }, false);
            }
        }

        private DateTime _timeStart;

        /// <summary>
        /// every half hour we send the message that the stream does not close
        /// каждые полчаса отправляем сообщение, чтобы поток не закрылся
        /// </summary>
        private void KeepaliveUserDataStream()
        {
            while (true)
            {
                Thread.Sleep(30000);

                if (_listenKey == "")
                {
                    return;
                }

                if (_isDisposed == true)
                {
                    return;
                }

                if (_timeStart.AddMinutes(25) < DateTime.Now)
                {
                    _timeStart = DateTime.Now;

                    CreateQueryNoLock(Method.PUT,
                        "/" + type_str_selector + "/v1/listenKey", new Dictionary<string, string>()
                            { { "listenKey=", _listenKey } }, false);

                }
            }
        }

        #region проверка ордеров на исполнение

        private string GetMyTradesToBinance()
        {
            var res = CreateQuery(
                       Method.GET,
                       "/" + type_str_selector + "/v1/userTrades",
                       new Dictionary<string, string>(),
                       true);
            return res;
        }

        private MyTrade ConvertTradesToSystem(TradesResponseReserches responcetrade)
        {
            try
            {
                MyTrade trade = new MyTrade();
                trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(responcetrade.time));
                trade.NumberOrderParent = responcetrade.orderid.ToString();
                trade.NumberTrade = responcetrade.id.ToString();
                trade.Volume = responcetrade.qty.ToDecimal();
                trade.Price = responcetrade.price.ToDecimal();
                trade.SecurityNameCode = responcetrade.symbol;
                trade.Side = responcetrade.side == "BUY" ? Side.Buy : Side.Sell;

                return trade;
            }
            catch (Exception error)
            {
                SendLogMessage(error.Message, LogMessageType.Error);
                return null;
            }
        }

        public void ResearchTradesToOrders_Binance(List<Order> orders)
        {
            try
            {

                var res = GetMyTradesToBinance();
                List<TradesResponseReserches> responceTrades = JsonConvert.DeserializeAnonymousType(res, new List<TradesResponseReserches>());

                for (int i = 0; i < orders.Count; i++)
                {
                    for (int j = 0; j < responceTrades.Count; j++)
                    {
                        if (orders[i].NumberMarket == Convert.ToString(responceTrades[j].orderid))
                        {
                            var trade = ConvertTradesToSystem(responceTrades[j]);

                            if (MyTradeEvent != null && trade != null)
                            {
                                MyTradeEvent(trade);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                //ignore
            }
        }

        #endregion

        /// <summary>
        /// get realtime Mark Price and Funding Rate
        /// получать среднюю цену инструмента (на всех биржах) и ставку фандирования в реальном времени
        /// </summary>
        public PremiumIndex GetPremiumIndex(string symbol)
        {
            try
            {
                var res = CreateQuery(
                    Method.GET,
                    "/" + type_str_selector + "/v1/premiumIndex",
                    new Dictionary<string, string>() { { "symbol=", symbol } },
                    true);

                PremiumIndex resp = JsonConvert.DeserializeAnonymousType(res, new PremiumIndex());
                return resp;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// shows whether connection works
        /// работает ли соединение
        /// </summary>
        public bool IsConnected;

        private object _lock = new object();

        /// <summary>
        /// shows account info
        /// показывает статистику по аккаунту пользователя
        /// </summary>
        public AccountResponseFutures GetAccountInfo()
        {
            lock (_lock)
            {
                try
                {
                    string res = null;

                    if (type_str_selector == "dapi")
                    {
                        res = CreateQuery(Method.GET, "/" + type_str_selector + "/v1/account", null, true);
                    }
                    else if (type_str_selector == "fapi")
                    {
                        res = CreateQuery(Method.GET, "/" + type_str_selector + "/v2/account", null, true);
                    }

                    if (res == null)
                    {
                        return null;
                    }

                    AccountResponseFutures resp = JsonConvert.DeserializeAnonymousType(res, new AccountResponseFutures());
                    return resp;
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    return null;
                }
            }
        }

        public AccountResponseFutures GetAccountInfoFromDFut(string response)
        {
            if (response == null)
            {
                return null;
            }

            AccountResponseFutures resp = new AccountResponseFutures();

            List<AssetFuturesCoinM> assetsCoinM = JsonConvert.DeserializeAnonymousType(response, new List<AssetFuturesCoinM>());

            List<AssetFutures> assets = new List<AssetFutures>();

            for (int i = 0; i < assetsCoinM.Count; i++)
            {
                AssetFutures futAss = new AssetFutures();
                futAss.asset = assetsCoinM[i].asset;
                futAss.marginBalance = assetsCoinM[i].balance;
                assets.Add(futAss);
            }

            resp.assets = assets;
            resp.positions = new List<PositionFutures>();

            return resp;
        }

        /// <summary>
        /// balance 
        /// баланс портфеля
        /// </summary>
        public void GetBalance()
        {
            try
            {
                AccountResponseFutures resp = GetAccountInfo();
                if (NewPortfolio != null && resp != null)
                {
                    NewPortfolio(resp);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        public event Action<AccountResponseFutures> NewPortfolio;

        /// <summary>
        /// take securities
        /// взять бумаги
        /// </summary>
        public void GetSecurities()
        {
            lock (_lock)
            {
                try
                {
                    //Get All Margin Pairs (MARKET_DATA)
                    //GET /sapi/v1/margin/allPairs

                    var res = CreateQuery(Method.GET, "/" + type_str_selector + "/v1/exchangeInfo", null, false);

                    SecurityResponce secResp = JsonConvert.DeserializeAnonymousType(res, new SecurityResponce());

                    if (UpdatePairs != null)
                    {
                        UpdatePairs(secResp);
                    }

                }
                catch (Exception ex)
                {
                    if (LogMessageEvent != null)
                    {
                        LogMessageEvent(ex.ToString(), LogMessageType.Error);
                    }
                }
            }
        }

        /// <summary>
        /// candles
        /// свечи
        /// </summary>
        private List<Candle> _candles;

        private readonly object _candleLocker = new object();

        /// <summary>
        /// convert JSON to candles
        /// преобразует JSON в свечи
        /// </summary>
        /// <param name="jsonCandles"></param>
        /// <returns></returns>
        private List<Candle> _deserializeCandles(string jsonCandles)
        {
            try
            {
                lock (_candleLocker)
                {
                    if (jsonCandles == null ||
                        jsonCandles == "[]")
                        return null;

                    string res = jsonCandles.Trim(new char[] { '[', ']' });

                    if (string.IsNullOrEmpty(res) == true)
                    {
                        return null;
                    }

                    var res2 = res.Split(new char[] { ']' });

                    _candles = new List<Candle>();

                    Candle newCandle;

                    for (int i = 0; i < res2.Length; i++)
                    {
                        if (i != 0)
                        {
                            string upd = res2[i].Substring(2);
                            upd = upd.Replace("\"", "");
                            string[] param = upd.Split(',');

                            newCandle = new Candle();
                            newCandle.TimeStart = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(param[0]));
                            newCandle.Low = param[3].ToDecimal();
                            newCandle.High = param[2].ToDecimal();
                            newCandle.Open = param[1].ToDecimal();
                            newCandle.Close = param[4].ToDecimal();
                            newCandle.Volume = param[5].ToDecimal();
                            _candles.Add(newCandle);
                        }
                        else
                        {
                            string[] param = res2[i].Replace("\"", "").Split(',');

                            newCandle = new Candle();
                            newCandle.TimeStart = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(param[0]));
                            newCandle.Low = param[3].ToDecimal();
                            newCandle.High = param[2].ToDecimal();
                            newCandle.Open = param[1].ToDecimal();
                            newCandle.Close = param[4].ToDecimal();
                            newCandle.Volume = param[5].ToDecimal();

                            _candles.Add(newCandle);
                        }
                    }

                    return _candles;
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        public List<Candle> GetCandlesForTimes(string nameSec, TimeSpan tf, DateTime timeStart, DateTime timeEnd)
        {
            DateTime yearBegin = new DateTime(1970, 1, 1);

            var timeStampStart = timeStart - yearBegin;
            var r = timeStampStart.TotalMilliseconds;
            string startTime = Convert.ToInt64(r).ToString();

            var timeStampEnd = timeEnd - yearBegin;
            var rEnd = timeStampEnd.TotalMilliseconds;
            string endTime = Convert.ToInt64(rEnd).ToString();


            string needTf = "";

            switch ((int)tf.TotalMinutes)
            {
                case 1:
                    needTf = "1m";
                    break;
                case 2:
                    needTf = "2m";
                    break;
                case 3:
                    needTf = "3m";
                    break;
                case 5:
                    needTf = "5m";
                    break;
                case 10:
                    needTf = "10m";
                    break;
                case 15:
                    needTf = "15m";
                    break;
                case 20:
                    needTf = "20m";
                    break;
                case 30:
                    needTf = "30m";
                    break;
                case 45:
                    needTf = "45m";
                    break;
                case 60:
                    needTf = "1h";
                    break;
                case 120:
                    needTf = "2h";
                    break;
            }

            string endPoint = "" + type_str_selector + "/v1/klines";

            if (needTf != "2m" && needTf != "10m" && needTf != "20m" && needTf != "45m")
            {
                var param = new Dictionary<string, string>();
                param.Add("symbol=" + nameSec.ToUpper(), "&interval=" + needTf + "&startTime=" + startTime + "&endTime=" + endTime);

                var res = CreateQuery(Method.GET, endPoint, param, false);

                var candles = _deserializeCandles(res);
                return candles;

            }
            else
            {
                if (needTf == "2m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=1m" + "&startTime=" + startTime + "&endTime=" + endTime);
                    var res = CreateQuery(Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);

                    var newCandles = BuildCandles(candles, 2, 1);
                    return newCandles;
                }
                else if (needTf == "10m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=5m" + "&startTime=" + startTime + "&endTime=" + endTime);
                    var res = CreateQuery(Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 10, 5);
                    return newCandles;
                }
                else if (needTf == "20m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=5m" + "&startTime=" + startTime + "&endTime=" + endTime);
                    var res = CreateQuery(Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 20, 5);
                    return newCandles;
                }
                else if (needTf == "45m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=15m" + "&startTime=" + startTime + "&endTime=" + endTime);
                    var res = CreateQuery(Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 45, 15);
                    return newCandles;
                }
            }

            return null;
        }

        private object _locker = new object();

        public List<Trade> GetTickHistoryToSecurity(string security, DateTime endTime)
        {
            lock (_locker)
            {
                try
                {
                    long from = TimeManager.GetTimeStampMilliSecondsToDateTime(endTime);

                    string timeStamp = TimeManager.GetUnixTimeStampMilliseconds().ToString();
                    Dictionary<string, string> param = new Dictionary<string, string>();

                    param.Add("symbol=" + security, "&limit=1000" + "&startTime=" + from);

                    string endPoint = "" + type_str_selector + "/v1/aggTrades";

                    var res2 = CreateQuery(Method.GET, endPoint, param, false);

                    AgregatedHistoryTrade[] tradeHistory = JsonConvert.DeserializeObject<AgregatedHistoryTrade[]>(res2);

                    var oldTrades = CreateTradesFromJson(security, tradeHistory);

                    return oldTrades;
                }
                catch
                {
                    SendLogMessage(OsLocalization.Market.Message95 + security, LogMessageType.Error);

                    return null;
                }
            }
        }

        public decimal StringToDecimal(string value)
        {
            string sep = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            return Convert.ToDecimal(value.Replace(",", sep).Replace(".", sep));
        }

        private List<Trade> CreateTradesFromJson(string secName, AgregatedHistoryTrade[] binTrades)
        {
            List<Trade> trades = new List<Trade>();

            foreach (var jtTrade in binTrades)
            {
                var trade = new Trade();

                trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(jtTrade.T));
                trade.Price = StringToDecimal(jtTrade.P);
                trade.MicroSeconds = 0;
                trade.Id = jtTrade.A.ToString();
                trade.Volume = Math.Abs(StringToDecimal(jtTrade.Q));
                trade.SecurityNameCode = secName;

                if (!jtTrade.m)
                {
                    trade.Side = Side.Buy;
                    trade.Ask = 0;
                    trade.AsksVolume = 0;
                    trade.Bid = trade.Price;
                    trade.BidsVolume = trade.Volume;
                }
                else
                {
                    trade.Side = Side.Sell;
                    trade.Ask = trade.Price;
                    trade.AsksVolume = trade.Volume;
                    trade.Bid = 0;
                    trade.BidsVolume = 0;
                }


                trades.Add(trade);
            }

            return trades;
        }

        /// <summary>
        /// take candles
        /// взять свечи
        /// </summary>
        /// <returns></returns>
        public List<Candle> GetCandles(string nameSec, TimeSpan tf)
        {
            string needTf = "";

            switch ((int)tf.TotalMinutes)
            {
                case 1:
                    needTf = "1m";
                    break;
                case 2:
                    needTf = "2m";
                    break;
                case 3:
                    needTf = "3m";
                    break;
                case 5:
                    needTf = "5m";
                    break;
                case 10:
                    needTf = "10m";
                    break;
                case 15:
                    needTf = "15m";
                    break;
                case 20:
                    needTf = "20m";
                    break;
                case 30:
                    needTf = "30m";
                    break;
                case 45:
                    needTf = "45m";
                    break;
                case 60:
                    needTf = "1h";
                    break;
                case 120:
                    needTf = "2h";
                    break;
                case 240:
                    needTf = "4h";
                    break;
                case 1440:
                    needTf = "1d";
                    break;
            }

            string endPoint = "/" + type_str_selector + "/v1/klines";

            if (needTf != "2m" && needTf != "10m" && needTf != "20m" && needTf != "45m")
            {
                var param = new Dictionary<string, string>();
                param.Add("symbol=" + nameSec.ToUpper(), "&interval=" + needTf);

                var res = CreateQuery(Method.GET, endPoint, param, false);

                var candles = _deserializeCandles(res);
                return candles;

            }
            else
            {
                if (needTf == "2m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=1m");
                    var res = CreateQuery(Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);

                    var newCandles = BuildCandles(candles, 2, 1);
                    return newCandles;
                }
                else if (needTf == "10m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=5m");
                    var res = CreateQuery(Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 10, 5);
                    return newCandles;
                }
                else if (needTf == "20m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=5m");
                    var res = CreateQuery(Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 20, 5);
                    return newCandles;
                }
                else if (needTf == "45m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=15m");
                    var res = CreateQuery(Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 45, 15);
                    return newCandles;
                }
            }

            return null;
        }

        /// <summary>
        /// converts candles of one timeframe to a larger
        /// преобразует свечи одного таймфрейма в больший
        /// </summary>
        /// <param name="oldCandles"></param>
        /// <param name="needTf"></param>
        /// <param name="oldTf"></param>
        /// <returns></returns>
        private List<Candle> BuildCandles(List<Candle> oldCandles, int needTf, int oldTf)
        {
            if (oldCandles == null)
            {
                return null;
            }

            List<Candle> newCandles = new List<Candle>();

            int index = oldCandles.FindIndex(can => can.TimeStart.Minute % needTf == 0);

            int count = needTf / oldTf;

            int counter = 0;

            Candle newCandle = new Candle();

            for (int i = index; i < oldCandles.Count; i++)
            {
                counter++;

                if (counter == 1)
                {
                    newCandle = new Candle();
                    newCandle.Open = oldCandles[i].Open;
                    newCandle.TimeStart = oldCandles[i].TimeStart;
                    newCandle.Low = Decimal.MaxValue;
                }

                newCandle.High = oldCandles[i].High > newCandle.High
                    ? oldCandles[i].High
                    : newCandle.High;

                newCandle.Low = oldCandles[i].Low < newCandle.Low
                    ? oldCandles[i].Low
                    : newCandle.Low;

                newCandle.Volume += oldCandles[i].Volume;

                if (counter == count)
                {
                    newCandle.Close = oldCandles[i].Close;
                    newCandle.State = CandleState.Finished;
                    newCandles.Add(newCandle);
                    counter = 0;
                }

                if (i == oldCandles.Count - 1 && counter != count)
                {
                    newCandle.Close = oldCandles[i].Close;
                    newCandle.State = CandleState.None;
                    newCandles.Add(newCandle);
                }
            }

            return newCandles;
        }

        #region Аутентификация запроса

        /// <summary>
        /// multi-threaded access locker to http-requests
        /// блокиратор многопоточного доступа к http запросам
        /// </summary>
        private object _queryHttpLocker = new object();

        RateGate _rateGate = new RateGate(1, TimeSpan.FromMilliseconds(100));

        /// <summary>
        /// method sends a request and returns a response from the server
        /// sending of a requests is controlled by locker to avoid to many 
        /// requests be sent at a time in case of multiple threads
        /// метод отправляет запрос и возвращает ответ от сервера
        /// отправка запросов контроллируется локером чтобы избежать
        /// отправки чрезмерного количества запросов на сервер в случае многих потоков
        /// </summary>
        public string CreateQuery(Method method, string endpoint, Dictionary<string, string> param = null, bool auth = false)
        {
            try
            {
                lock (_queryHttpLocker)
                {
                    _rateGate.WaitToProceed();
                    return PerformHttpRequest(method, endpoint, param, auth);
                }
            }
            catch (Exception ex)
            {
                return HandleHttpRequestException(ex);
            }
        }

        /// <summary>
        /// method sends a request and returns a response from the server
        /// there is NO locker to allow some high priority requests 
        /// be sent immediately if needed
        /// метод отправляет запрос и возвращает ответ от сервера
        /// в этом методе отсутствует локирование чтобы дать возможность 
        /// в некоторых важных случаях возможность сделать незамедлительный запрос на сервер
        /// </summary>
        public string CreateQueryNoLock(Method method, string endpoint, Dictionary<string, string> param = null, bool auth = false)
        {
            try
            {
                return PerformHttpRequest(method, endpoint, param, auth);
            }
            catch (Exception ex)
            {
                return HandleHttpRequestException(ex);
            }
        }

        private string PerformHttpRequest(Method method, string endpoint, Dictionary<string, string> param = null, bool auth = false)
        {
            string fullUrl = "";

            if (param != null)
            {
                fullUrl += "?";

                foreach (var onePar in param)
                {
                    fullUrl += onePar.Key + onePar.Value;
                }
            }

            if (auth)
            {
                string message = "";

                string timeStamp = GetNonce();

                message += "timestamp=" + timeStamp;

                if (fullUrl == "")
                {
                    fullUrl = "?timestamp=" + timeStamp + "&signature=" + CreateSignature(message);
                }
                else
                {
                    message = fullUrl + "&timestamp=" + timeStamp;
                    fullUrl += "&timestamp=" + timeStamp + "&signature=" + CreateSignature(message.Trim('?'));
                }
            }

            var request = new RestRequest(endpoint + fullUrl, method);
            request.AddHeader("X-MBX-APIKEY", ApiKey);

            string baseUrl = _baseUrl;

            var response = new RestClient(baseUrl).Execute(request).Content;

            if (response.StartsWith("<!DOCTYPE"))
            {
                throw new Exception(response);
            }
            else if (response.Contains("code") && !response.Contains("code\": 200"))
            {
                var error = JsonConvert.DeserializeAnonymousType(response, new ErrorMessage());
                throw new Exception(error.msg);
            }

            return response;
        }

        private string HandleHttpRequestException(Exception ex)
        {
            if (ex.ToString().Contains("This listenKey does not exist"))
            {
                RenewListenKey();
                return null;
            }
            if (ex.ToString().Contains("Unknown order sent"))
            {
                SendLogMessage(ex.ToString(), LogMessageType.System);
                return null;
            }

            SendLogMessage(ex.ToString(), LogMessageType.Error);
            return null;
        }

        private string GetNonce()
        {
            var resTime = CreateQuery(Method.GET, "/" + type_str_selector + "/v1/time", null, false);

            if (!string.IsNullOrEmpty(resTime))
            {
                var result = JsonConvert.DeserializeAnonymousType(resTime, new BinanceTime());
                return (result.serverTime + 500).ToString();
            }
            else
            {
                DateTime yearBegin = new DateTime(1970, 1, 1);
                var timeStamp = DateTime.UtcNow - yearBegin;
                var r = timeStamp.TotalMilliseconds;
                var re = Convert.ToInt64(r);

                return re.ToString();
            }
        }

        private string CreateSignature(string message)
        {
            var messageBytes = Encoding.UTF8.GetBytes(message);
            var keyBytes = Encoding.UTF8.GetBytes(SecretKey);
            var hash = new HMACSHA256(keyBytes);
            var computedHash = hash.ComputeHash(messageBytes);
            return BitConverter.ToString(computedHash).Replace("-", "").ToLower();
        }

        private byte[] Hmacsha256(byte[] keyByte, byte[] messageBytes)
        {
            using (var hash = new HMACSHA256(keyByte))
            {
                return hash.ComputeHash(messageBytes);
            }
        }

        #endregion

        // work with orders работа с ордерами

        /// <summary>
        /// execute order
        /// исполнить ордер
        /// </summary>
        /// <param name="order"></param>
        public void ExecuteOrder(Order order)
        {
            lock (_lockOrder)
            {
                try
                {
                    if (IsConnected == false)
                    {
                        return;
                    }

                    Dictionary<string, string> param = new Dictionary<string, string>();

                    param.Add("symbol=", order.SecurityNameCode.ToUpper());
                    param.Add("&side=", order.Side == Side.Buy ? "BUY" : "SELL");
                    if (HedgeMode)
                    {
                        if (order.PositionConditionType == OrderPositionConditionType.Close)
                        {
                            param.Add("&positionSide=", order.Side == Side.Buy ? "SHORT" : "LONG");
                        }
                        else
                        {
                            param.Add("&positionSide=", order.Side == Side.Buy ? "LONG" : "SHORT");
                        }
                    }
                    param.Add("&type=", order.TypeOrder == OrderPriceType.Limit ? "LIMIT" : "MARKET");
                    //param.Add("&timeInForce=", "GTC");
                    param.Add("&newClientOrderId=", "x-gnrPHWyE" + order.NumberUser.ToString());
                    param.Add("&quantity=",
                        order.Volume.ToString(CultureInfo.InvariantCulture)
                            .Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, "."));

                    if (!HedgeMode && order.PositionConditionType == OrderPositionConditionType.Close)
                    {
                        param.Add("&reduceOnly=", "true");
                    }

                    if (order.TypeOrder == OrderPriceType.Limit)
                    {
                        param.Add("&timeInForce=", "GTC");
                        param.Add("&price=",
                            order.Price.ToString(CultureInfo.InvariantCulture)
                                .Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, "."));

                    }

                    var res = CreateQuery(Method.POST, "/" + type_str_selector + "/v1/order", param, true);

                    if (res != null && res.Contains("clientOrderId"))
                    {
                        SendLogMessage(res, LogMessageType.Trade);
                    }
                    else
                    {
                        order.State = OrderStateType.Fail;

                        if (MyOrderEvent != null)
                        {
                            MyOrderEvent(order);
                        }
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        private object _lockOrder = new object();

        List<Order> _canselOrders = new List<Order>();

        private bool CanCanselOrder(Order order)
        {
            bool isInArray = false;

            for (int i = 0; i < _canselOrders.Count; i++)
            {
                if (_canselOrders[i].NumberUser == order.NumberUser)
                {
                    isInArray = true;
                    break;
                }
            }

            if (isInArray == true)
            {
                return false;
            }
            else
            {
                _canselOrders.Add(order);
                return true;
            }
        }

        /// <summary>
        /// cancel order
        /// отменить ордер
        /// </summary>
        public void CanсelOrder(Order order)
        {
            lock (_lockOrder)
            {
                try
                {
                    if (CanCanselOrder(order) == false)
                    {
                        Order onBoard = GetOrderState(order);

                        if (onBoard == null)
                        {
                            order.State = OrderStateType.Cancel;

                            if (MyOrderEvent != null)
                            {
                                MyOrderEvent(order);
                            }

                            return;
                        }

                        order.State = onBoard.State;
                        order.NumberMarket = onBoard.NumberMarket;

                        if (MyOrderEvent != null)
                        {
                            MyOrderEvent(order);
                        }

                        return;
                    }

                    if (string.IsNullOrEmpty(order.NumberMarket))
                    {
                        Order onBoard = GetOrderState(order);

                        if (onBoard == null)
                        {
                            order.State = OrderStateType.Cancel;
                            SendLogMessage("При отзыве ордера не нашли такого на бирже. считаем что он уже отозван",
                                LogMessageType.Error);
                            if (MyOrderEvent != null)
                            {
                                MyOrderEvent(order);
                            }
                            return;
                        }

                        order.NumberMarket = onBoard.NumberMarket;
                        order = onBoard;
                    }

                    Dictionary<string, string> param = new Dictionary<string, string>();

                    param.Add("symbol=", order.SecurityNameCode.ToUpper());
                    param.Add("&orderId=", order.NumberMarket);

                    CreateQuery(Method.DELETE, "/" + type_str_selector + "/v1/order", param, true);

                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);

                }
            }
        }

        /// <summary>
        /// chack order state
        /// проверить ордера на состояние
        /// </summary>
        public bool GetAllOrders(List<Order> oldOpenOrders)
        {
            List<string> namesSec = new List<string>();

            for (int i = 0; i < oldOpenOrders.Count; i++)
            {
                if (namesSec.Find(name => name.Contains(oldOpenOrders[i].SecurityNameCode)) == null)
                {
                    namesSec.Add(oldOpenOrders[i].SecurityNameCode);
                }
            }

            string endPoint = "/" + type_str_selector + "/v1/allOrders";

            List<HistoryOrderReport> allOrders = new List<HistoryOrderReport>();

            for (int i = 0; i < namesSec.Count; i++)
            {
                var param = new Dictionary<string, string>();
                param.Add("symbol=", namesSec[i].ToUpper());
                //param.Add("&recvWindow=" , "100");
                //param.Add("&limit=", GetNonce());
                param.Add("&limit=", "500");
                //"symbol={symbol.ToUpper()}&recvWindow={recvWindow}"

                var res = CreateQuery(Method.GET, endPoint, param, true);

                if (res == null)
                {
                    continue;
                }

                HistoryOrderReport[] orders = JsonConvert.DeserializeObject<HistoryOrderReport[]>(res);

                if (orders != null && orders.Length != 0)
                {
                    allOrders.AddRange(orders);
                }
            }

            for (int i = 0; i < oldOpenOrders.Count; i++)
            {
                if (oldOpenOrders[i].Volume == oldOpenOrders[i].VolumeExecute)
                {
                    continue;
                }
                HistoryOrderReport myOrder = allOrders.Find(ord => ord.orderId == oldOpenOrders[i].NumberMarket);

                if (myOrder == null)
                {
                    for (int i2 = 0; i2 < allOrders.Count; i2++)
                    {
                        if (string.IsNullOrEmpty(allOrders[i2].clientOrderId))
                        {
                            continue;
                        }

                        string id = allOrders[i2].clientOrderId.Replace("x-gnrPHWyE", "");

                        try
                        {
                            if (Convert.ToInt32(id) == oldOpenOrders[i].NumberUser)
                            {
                                myOrder = allOrders[i2];
                                break;
                            }
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                }

                if (myOrder == null)
                {
                    continue;
                }

                if (myOrder.status == "NEW")
                { // order is active. Do nothing / ордер активен. Ничего не делаем
                    continue;
                }

                else if (myOrder.status == "FILLED" ||
                    myOrder.status == "PARTIALLY_FILLED")
                { // order executed / ордер исполнен

                    try
                    {
                        if (myOrder.executedQty.ToDecimal() - oldOpenOrders[i].VolumeExecute <= 0)
                        {
                            continue;
                        }
                    }
                    catch
                    {
                        continue;
                    }

                    Order newOrder = new Order();
                    newOrder.NumberMarket = myOrder.orderId;
                    newOrder.NumberUser = oldOpenOrders[i].NumberUser;
                    newOrder.SecurityNameCode = oldOpenOrders[i].SecurityNameCode;
                    newOrder.State = OrderStateType.Done;

                    newOrder.Volume = oldOpenOrders[i].Volume;
                    newOrder.VolumeExecute = oldOpenOrders[i].VolumeExecute;
                    newOrder.Price = oldOpenOrders[i].Price;
                    newOrder.TypeOrder = oldOpenOrders[i].TypeOrder;
                    newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(myOrder.updateTime));
                    newOrder.TimeCancel = newOrder.TimeCallBack;
                    newOrder.ServerType = ServerType.BinanceFutures;
                    newOrder.PortfolioNumber = oldOpenOrders[i].PortfolioNumber;


                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(newOrder);
                    }
                }
                else
                {
                    Order newOrder = new Order();
                    newOrder.NumberMarket = myOrder.orderId;
                    newOrder.NumberUser = oldOpenOrders[i].NumberUser;
                    newOrder.SecurityNameCode = oldOpenOrders[i].SecurityNameCode;
                    newOrder.State = OrderStateType.Cancel;

                    newOrder.Volume = oldOpenOrders[i].Volume;
                    newOrder.VolumeExecute = oldOpenOrders[i].VolumeExecute;
                    newOrder.Price = oldOpenOrders[i].Price;
                    newOrder.TypeOrder = oldOpenOrders[i].TypeOrder;
                    newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(myOrder.updateTime));
                    newOrder.TimeCancel = newOrder.TimeCallBack;
                    newOrder.ServerType = ServerType.BinanceFutures;
                    newOrder.PortfolioNumber = oldOpenOrders[i].PortfolioNumber;

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(newOrder);
                    }
                }
            }
            return true;
        }

        private Order GetOrderState(Order oldOrder)
        {
            List<string> namesSec = new List<string>();
            namesSec.Add(oldOrder.SecurityNameCode);

            string endPoint = "/" + type_str_selector + "/v1/allOrders";

            List<HistoryOrderReport> allOrders = new List<HistoryOrderReport>();

            for (int i = 0; i < namesSec.Count; i++)
            {
                var param = new Dictionary<string, string>();
                param.Add("symbol=", namesSec[i].ToUpper());
                //param.Add("&recvWindow=" , "100");
                //param.Add("&limit=", GetNonce());
                param.Add("&limit=", "500");
                //"symbol={symbol.ToUpper()}&recvWindow={recvWindow}"

                var res = CreateQuery(Method.GET, endPoint, param, true);

                if (res == null)
                {
                    continue;
                }

                HistoryOrderReport[] orders = JsonConvert.DeserializeObject<HistoryOrderReport[]>(res);

                if (orders != null && orders.Length != 0)
                {
                    allOrders.AddRange(orders);
                }
            }

            HistoryOrderReport orderOnBoard =
                allOrders.Find(ord => ord.clientOrderId.Replace("x-gnrPHWyE", "") == oldOrder.NumberUser.ToString());

            if (orderOnBoard == null)
            {
                return null;
            }

            Order newOrder = new Order();
            newOrder.NumberMarket = orderOnBoard.orderId;
            newOrder.NumberUser = oldOrder.NumberUser;
            newOrder.SecurityNameCode = oldOrder.SecurityNameCode;
            newOrder.State = OrderStateType.Cancel;

            newOrder.Volume = oldOrder.Volume;
            newOrder.VolumeExecute = oldOrder.VolumeExecute;
            newOrder.Price = oldOrder.Price;
            newOrder.TypeOrder = oldOrder.TypeOrder;
            newOrder.TimeCallBack = oldOrder.TimeCallBack;
            newOrder.TimeCancel = newOrder.TimeCallBack;
            newOrder.ServerType = ServerType.BinanceFutures;
            newOrder.PortfolioNumber = oldOrder.PortfolioNumber;

            if (orderOnBoard.status == "NEW" ||
                orderOnBoard.status == "PARTIALLY_FILLED")
            { // order is active. Do nothing / ордер активен. Ничего не делаем
                newOrder.State = OrderStateType.Activ;
            }
            else if (orderOnBoard.status == "FILLED")
            {
                newOrder.State = OrderStateType.Done;
            }
            else
            {
                newOrder.State = OrderStateType.Cancel;
            }

            if (MyOrderEvent != null)
            {
                MyOrderEvent(newOrder);
            }

            return newOrder;
        }

        // stream data from WEBSOCKET потоковые данные из WEBSOCKET

        /// <summary>
        /// WebSocket client
        /// клиент вебсокет
        /// </summary>
        private WebSocket _wsClient;

        /// <summary>
        /// takes messages that came through ws and puts them in a general queue
        /// берет пришедшие через ws сообщения и кладет их в общую очередь
        /// </summary>        
        private void GetRes(object sender, MessageReceivedEventArgs e)
        {
            if (_isDisposed == true)
            {
                return;
            }
            _newMessage.Enqueue(e.Message);
        }

        /// <summary>
        /// ws-connection is opened
        /// соединение по ws открыто
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Connect(object sender, EventArgs e)
        {
            IsConnected = true;
        }

        /// <summary>
        /// ws-connection is closed
        /// соединение по ws закрыто
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Disconnect(object sender, EventArgs e)
        {
            if (IsConnected)
            {
                IsConnected = false;

                foreach (var ws in _wsStreams)
                {
                    ws.Value.Opened -= new EventHandler(Connect);
                    ws.Value.Closed -= new EventHandler(Disconnect);
                    ws.Value.Error -= new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>(WsError);
                    ws.Value.MessageReceived -= new EventHandler<MessageReceivedEventArgs>(GetRes);

                    ws.Value.Close();
                    ws.Value.Dispose();
                }

                _wsStreams.Clear();

                if (Disconnected != null)
                {
                    Disconnected();
                }
            }
        }

        /// <summary>
        /// error from ws4net
        /// ошибка из ws4net
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WsError(object sender, EventArgs e)
        {
            //if (e.ToString().Contains("Unknown order"))
            //{
            //    return;
            //}
            SendLogMessage("Ошибка из ws4net :" + e.ToString(), LogMessageType.Error);
        }

        /// <summary>
        /// queue of new messages from the exchange server
        /// очередь новых сообщений, пришедших с сервера биржи
        /// </summary>
        private ConcurrentQueue<string> _newMessage = new ConcurrentQueue<string>();

        /// <summary>
        /// data stream collection
        /// коллекция потоков данных
        /// </summary>
        private Dictionary<string, WebSocket> _wsStreams = new Dictionary<string, WebSocket>();

        /// <summary>
        /// subscribe this security to get depths and trades
        /// подписать данную бумагу на получение стаканов и трейдов
        /// </summary>
        public void SubscribleTradesAndDepths(Security security)
        {
            if (!_wsStreams.ContainsKey(security.Name))
            {
                string urlStr = wss_point + "/stream?streams="
                                 + security.Name.ToLower() + "@depth20/"
                                 + security.Name.ToLower() + "@trade";
                _wsClient = new WebSocket(urlStr); // create web-socket / создаем вебсоке

                _wsClient.Opened += new EventHandler(Connect);
                _wsClient.Closed += new EventHandler(Disconnect);
                _wsClient.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>(WsError);
                _wsClient.MessageReceived += new EventHandler<MessageReceivedEventArgs>(GetRes);

                if (_wsStreams.ContainsKey(security.Name))
                {
                    _wsStreams[security.Name].Opened -= new EventHandler(Connect);
                    _wsStreams[security.Name].Closed -= new EventHandler(Disconnect);
                    _wsStreams[security.Name].Error -= new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>(WsError);
                    _wsStreams[security.Name].MessageReceived -= new EventHandler<MessageReceivedEventArgs>(GetRes);
                    _wsStreams[security.Name].Close();
                    _wsStreams.Remove(security.Name);
                }

                _wsClient.Open();

                _wsStreams.Add(security.Name, _wsClient);
            }

        }

        /// <summary>
        /// takes messages from the general queue, converts them to C # classes and sends them to up
        /// берет сообщения из общей очереди, конвертирует их в классы C# и отправляет на верх
        /// </summary>
        public void ConverterUserData()
        {
            while (true)
            {
                try
                {
                    if (!_newUserDataMessage.IsEmpty)
                    {
                        BinanceUserMessage messsage;

                        if (_newUserDataMessage.TryDequeue(out messsage))
                        {
                            string mes = messsage.MessageStr;

                            if (mes.Contains("code"))
                            {
                                // если есть code ошибки, то пытаемся распарсить
                                ErrorMessage _err = new ErrorMessage();

                                try
                                {
                                    _err = JsonConvert.DeserializeAnonymousType(mes, new ErrorMessage());
                                }
                                catch (Exception e)
                                {
                                    // если не смогли распарсить, то просто покажем что пришло
                                    _err.code = 9999;
                                    _err.msg = mes;
                                }
                                SendLogMessage("code:" + _err.code.ToString() + ",msg:" + _err.msg, LogMessageType.Error);
                            }

                            else if (mes.Contains("\"e\"" + ":" + "\"ORDER_TRADE_UPDATE\""))
                            {
                                // если ошибки в ответе ордера
                                OrderUpdResponse ord = new OrderUpdResponse();
                                try
                                {
                                    ord = JsonConvert.DeserializeAnonymousType(mes, new OrderUpdResponse());
                                }
                                catch (Exception)
                                {
                                    SendLogMessage("error in order update:" + mes, LogMessageType.Error);
                                    continue;
                                }

                                var order = ord.o;

                                Int32 orderNumUser;

                                try
                                {
                                    orderNumUser = Convert.ToInt32(order.c.ToString().Replace("x-gnrPHWyE", ""));
                                }
                                catch (Exception)
                                {
                                    orderNumUser = Convert.ToInt32(order.c.GetHashCode());
                                }

                                if (order.x == "NEW")
                                {
                                    Order newOrder = new Order();
                                    newOrder.SecurityNameCode = order.s;
                                    newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(ord.T));
                                    newOrder.NumberUser = orderNumUser;

                                    newOrder.NumberMarket = order.i.ToString();
                                    //newOrder.PortfolioNumber = order.PortfolioNumber; добавить в сервере
                                    newOrder.Side = order.S == "BUY" ? Side.Buy : Side.Sell;
                                    newOrder.State = OrderStateType.Activ;
                                    newOrder.Volume = order.q.ToDecimal();
                                    newOrder.Price = order.p.ToDecimal();
                                    newOrder.ServerType = ServerType.BinanceFutures;
                                    newOrder.PortfolioNumber = newOrder.SecurityNameCode;

                                    if (MyOrderEvent != null)
                                    {
                                        MyOrderEvent(newOrder);
                                    }
                                }
                                else if (order.x == "CANCELED")
                                {
                                    Order newOrder = new Order();
                                    newOrder.SecurityNameCode = order.s;
                                    newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(ord.T));
                                    newOrder.TimeCancel = newOrder.TimeCallBack;
                                    newOrder.NumberUser = orderNumUser;
                                    newOrder.NumberMarket = order.i.ToString();
                                    newOrder.Side = order.S == "BUY" ? Side.Buy : Side.Sell;
                                    newOrder.State = OrderStateType.Cancel;
                                    newOrder.Volume = order.q.ToDecimal();
                                    newOrder.Price = order.p.ToDecimal();
                                    newOrder.ServerType = ServerType.BinanceFutures;
                                    newOrder.PortfolioNumber = newOrder.SecurityNameCode;

                                    if (MyOrderEvent != null)
                                    {
                                        MyOrderEvent(newOrder);
                                    }
                                }
                                else if (order.x == "REJECTED")
                                {
                                    Order newOrder = new Order();
                                    newOrder.SecurityNameCode = order.s;
                                    newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(ord.T));
                                    newOrder.NumberUser = orderNumUser;
                                    newOrder.NumberMarket = order.i.ToString();
                                    newOrder.Side = order.S == "BUY" ? Side.Buy : Side.Sell;
                                    newOrder.State = OrderStateType.Fail;
                                    newOrder.Volume = order.q.ToDecimal();
                                    newOrder.Price = order.p.ToDecimal();
                                    newOrder.ServerType = ServerType.BinanceFutures;
                                    newOrder.PortfolioNumber = newOrder.SecurityNameCode;

                                    if (MyOrderEvent != null)
                                    {
                                        MyOrderEvent(newOrder);
                                    }
                                }
                                else if (order.x == "TRADE")
                                {

                                    MyTrade trade = new MyTrade();
                                    trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(order.T));
                                    trade.NumberOrderParent = order.i;
                                    trade.NumberTrade = order.t;
                                    trade.Volume = order.l.ToDecimal();
                                    trade.Price = order.L.ToDecimal();
                                    trade.SecurityNameCode = order.s;
                                    trade.Side = order.S == "BUY" ? Side.Buy : Side.Sell;

                                    if (MyTradeEvent != null)
                                    {
                                        MyTradeEvent(trade);
                                    }
                                }
                                else if (order.x == "EXPIRED")
                                {
                                    Order newOrder = new Order();
                                    newOrder.SecurityNameCode = order.s;
                                    newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(ord.T));
                                    newOrder.TimeCancel = newOrder.TimeCallBack;
                                    newOrder.NumberUser = orderNumUser;
                                    newOrder.NumberMarket = order.i.ToString();
                                    newOrder.Side = order.S == "BUY" ? Side.Buy : Side.Sell;
                                    newOrder.State = OrderStateType.Cancel;
                                    newOrder.Volume = order.q.ToDecimal();
                                    newOrder.Price = order.p.ToDecimal();
                                    newOrder.ServerType = ServerType.BinanceFutures;
                                    newOrder.PortfolioNumber = newOrder.SecurityNameCode;

                                    if (MyOrderEvent != null)
                                    {
                                        MyOrderEvent(newOrder);
                                    }
                                }

                                continue;
                            }

                            else if (mes.Contains("\"e\"" + ":" + "\"ACCOUNT_UPDATE\""))
                            {
                                var portfolios = JsonConvert.DeserializeAnonymousType(mes, new AccountResponseFuturesFromWebSocket());

                                if (UpdatePortfolio != null)
                                {
                                    UpdatePortfolio(portfolios);
                                }
                                continue;
                            }

                            else if (IsListenKeyExpiredEvent(mes))
                            {
                                if (ListenKeyExpiredEvent != null)
                                {
                                    ListenKeyExpiredEvent(this);
                                }
                                continue;
                            }
                            else
                            {

                            }

                            //ORDER_TRADE_UPDATE
                            // "{\"e\":\"ORDER_TRADE_UPDATE\",\"T\":1579688850841,\"E\":1579688850846,\"o\":{\"s\":\"BTCUSDT\",\"c\":\"1998\",\"S\":\"BUY\",\"o\":\"LIMIT\",\"f\":\"GTC\",\"q\":\"0.001\",\"p\":\"8671.86\",\"ap\":\"0.00000\",\"sp\":\"0.00\",\"x\":\"NEW\",\"X\":\"NEW\",\"i\":760799835,\"l\":\"0.000\",\"z\":\"0.000\",\"L\":\"0.00\",\"T\":1579688850841,\"t\":0,\"b\":\"0.00000\",\"a\":\"0.00000\",\"m\":false,\"R\":false,\"wt\":\"CONTRACT_PRICE\",\"ot\":\"LIMIT\"}}"

                            //ACCOUNT_UPDATE
                            //"{\"e\":\"ACCOUNT_UPDATE\",\"T\":1579688850841,\"E\":1579688850846,\"a\":{\"B\":[{\"a\":\"USDT\",\"wb\":\"29.88018817\",\"cw\":\"29.88018817\"},{\"a\":\"BNB\",\"wb\":\"0.00000000\",\"cw\":\"0.00000000\"}],\"P\":[{\"s\":\"BTCUSDT\",\"pa\":\"0.000\",\"ep\":\"0.00000\",\"cr\":\"-0.05040000\",\"up\":\"0.00000000\",\"mt\":\"cross\",\"iw\":\"0.00000000\"},{\"s\":\"ETHUSDT\",\"pa\":\"0.000\",\"ep\":\"0.00000\",\"cr\":\"0.00000000\",\"up\":\"0.00000000\",\"mt\":\"cross\",\"iw\":\"0.00000000\"},{\"s\":\"BCHUSDT\",\"pa\":\"0.000\",\"ep\":\"0.00000\",\"cr\":\"0.00000000\",\"up\":\"0.00000000\",\"mt\":\"cross\",\"iw\":\"0.00000000\"},{\"s\":\"XRPUSDT\",\"pa\":\"0.0\",\"ep\":\"0.00000\",\"cr\":\"0.00000000\",\"up\":\"0.000000\",\"mt\":\"cross\",\"iw\":\"0.00000000\"},{\"s\":\"EOSUSDT\",\"pa\":\"0.0\",\"ep\":\"0.0000\",\"cr\":\"0.00000000\",\"up\":\"0.00000\",\"mt\":\"cross\",\"iw\":\"0.00000000\"},{\"s\":\"LTCUSDT\",\"pa\":\"0.000\",\"ep\":\"0.00000\",\"cr\":\"0.00000000\",\"up\":\"0.00000000\",\"mt\":\"cross\",\"iw\":\"0.00000000\"},{\"s\":\"TRXUSDT\",\"pa\":\"0\",\"ep\":\"0.00000\",\"cr\":\"0.00000000\",\"up\":\"0.00000\",\"mt\":\"cross\",\"iw\":\"0.00000000\"},{\"s\":\"ETCUSDT\",\"pa\":\"0.00\",\"ep\":\"0.00000\",\"cr\":\"0.00000000\",\"up\":\"0.0000000\",\"mt\":\"cross\",\"iw\":\"0.00000000\"},{\"s\":\"LINKUSDT\",\"pa\":\"0.00\",\"ep\":\"0.00000\",\"cr\":\"0.00000000\",\"up\":\"0.0000000\",\"mt\":\"cross\",\"iw\":\"0.00000000\"},{\"s\":\"XLMUSDT\",\"pa\":\"0\",\"ep\":\"0.00000\",\"cr\":\"0.00000000\",\"up\":\"0.00000\",\"mt\":\"cross\",\"iw\":\"0.00000000\"}]}}"

                            //LISTEN_KEY_EXPIRED
                            //"{\"e\": \"listenKeyExpired\", \"E\": 1653994245400}
                        }
                    }
                    else
                    {
                        if (_isDisposed)
                        {
                            return;
                        }
                        Thread.Sleep(1);
                    }
                }

                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }

            }
        }

        private static bool IsListenKeyExpiredEvent(string userDataMsg)
        {
            const string EVENT_NAME_KEY = "e";
            const string LISTEN_KEY_EXPIRED_EVENT_NAME = "listenKeyExpired";
            JObject userDataMsgJSON = ParseToJson(userDataMsg);
            if (userDataMsgJSON != null && userDataMsgJSON.Property(EVENT_NAME_KEY) != null)
            {
                string eventName = userDataMsgJSON.Value<string>(EVENT_NAME_KEY);
                return String.Equals(eventName, LISTEN_KEY_EXPIRED_EVENT_NAME, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private static JObject ParseToJson(string jsonMessage)
        {
            try
            {
                return JObject.Parse(jsonMessage);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// takes messages from the general queue, converts them to C # classes and sends them to up
        /// берет сообщения из общей очереди, конвертирует их в классы C# и отправляет на верх
        /// </summary>
        public void Converter()
        {
            while (true)
            {
                try
                {

                    if (!_newMessage.IsEmpty)
                    {
                        string mes;

                        if (_newMessage.TryDequeue(out mes))
                        {
                            if (mes.Contains("error"))
                            {
                                SendLogMessage(mes, LogMessageType.Error);
                            }

                            else if (mes.Contains("\"e\":\"trade\""))
                            {
                                var quotes = JsonConvert.DeserializeAnonymousType(mes, new TradeResponse());


                                if (quotes.data.X.ToString() != "MARKET")
                                {//INSURANCE_FUND
                                    continue;
                                }

                                if (NewTradesEvent != null)
                                {
                                    NewTradesEvent(quotes);
                                }
                                continue;
                            }

                            else if (mes.Contains("\"depthUpdate\""))
                            {
                                var quotes = JsonConvert.DeserializeAnonymousType(mes, new DepthResponseFutures());

                                if (UpdateMarketDepth != null)
                                {
                                    UpdateMarketDepth(quotes);
                                }
                                continue;
                            }
                        }
                    }
                    else
                    {
                        if (_isDisposed)
                        {
                            return;
                        }
                        Thread.Sleep(1);
                    }
                }

                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }
        }

        public bool HedgeMode
        {
            get { return _hedgeMode; }
            set
            {
                if (value == _hedgeMode)
                {
                    return;
                }
                _hedgeMode = value;
                SetPositionMode();
            }
        }
        private bool _hedgeMode;

        public void SetPositionMode()
        {
            try
            {
                if (IsConnected == false)
                {
                    return;
                }
                var rs = CreateQuery(Method.GET, "/" + type_str_selector + "/v1/positionSide/dual", new Dictionary<string, string>(), true);
                if (rs != null)
                {
                    var modeNow = JsonConvert.DeserializeAnonymousType(rs, new HedgeModeResponse());
                    if (modeNow.dualSidePosition != HedgeMode)
                    {
                        var param = new Dictionary<string, string>();
                        param.Add("dualSidePosition=", HedgeMode.ToString().ToLower());
                        CreateQuery(Method.POST, "/" + type_str_selector + "/v1/positionSide/dual", param, true);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);

            }

        }
        #region outgoing events / исходящие события

        /// <summary>
        /// my new orders
        /// новые мои ордера
        /// </summary>
        public event Action<Order> MyOrderEvent;

        /// <summary>
        /// my new trades
        /// новые мои сделки
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
        /// listen key which keeps socket connection alive has expired
        /// срок действия listen key, необходимого для жизни сокет коннекшена, истек
        /// </summary>
        public event Action<BinanceClientFutures> ListenKeyExpiredEvent;

        /// <summary>
        /// portfolios updated
        /// обновились портфели
        /// </summary>
        public event Action<AccountResponseFuturesFromWebSocket> UpdatePortfolio;

        /// <summary>
        /// new security in the system
        /// новые бумаги в системе
        /// </summary>
        public event Action<SecurityResponce> UpdatePairs;

        /// <summary>
        /// depth updated
        /// обновился стакан
        /// </summary>
        public event Action<DepthResponseFutures> UpdateMarketDepth;

        /// <summary>
        /// ticks updated
        /// обновились тики
        /// </summary>
        public event Action<TradeResponse> NewTradesEvent;

        /// <summary>
        /// API connection established
        /// соединение с API установлено
        /// </summary>
        public event Action Connected;

        /// <summary>
        /// API connection lost
        /// соединение с API разорвано
        /// </summary>
        public event Action Disconnected;

        #endregion

        #region log messages / сообщения для лога

        /// <summary>
        /// add a new log message
        /// добавить в лог новое сообщение
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// send exeptions
        /// отправляет исключения
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }

    public class BinanceUserMessage
    {
        public string MessageStr;
    }
}
