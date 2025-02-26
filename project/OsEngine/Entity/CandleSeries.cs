﻿/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Market.Servers.Tester;
using System;
using System.Collections.Generic;

namespace OsEngine.Entity
{

    /// <summary>
    /// a series of candles. The object in which the incoming data is collected candles
    /// серия свечек. Объект в котором из входящих данных собираются свечи
    /// </summary>
    public class CandleSeries
    {
        // сервис

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="timeFrameBuilder">/object that carries timeframe data/объект несущий в себе данные о таймФрейме</param>
        /// <param name="security">security we are subscribed to/бумага на которою мы подписаны</param>
        /// <param name="startProgram">the program that created the object/программа создавшая объект</param>
        public CandleSeries(TimeFrameBuilder timeFrameBuilder, Security security, StartProgram startProgram)
        {
            TimeFrameBuilder = timeFrameBuilder;
            Security = security;
            _startProgram = startProgram;

            UID = Guid.NewGuid();
        }

        public Guid UID;

        /// <summary>
        /// blocking empty constructor
        /// блокируем пустой конструктор
        /// </summary>
        private CandleSeries()
        {

        }

        /// <summary>
        /// программа создавшая объект
        /// </summary>
        private StartProgram _startProgram;

        /// <summary>
        /// данные из которых собираем свечи: из тиков или из стаканов
        /// </summary>
        public CandleMarketDataType CandleMarketDataType
        {
            get { return TimeFrameBuilder.CandleMarketDataType; }
        }

        /// <summary>
        /// тип сборки свечей: обычный, ренко, дельта, 
        /// </summary>
        public CandleCreateMethodType CandleCreateMethodType
        {
            get { return TimeFrameBuilder.CandleCreateMethodType; }
        }

        public TimeFrameBuilder TimeFrameBuilder;

        public string Specification
        {
            get
            {
                string _specification
                    = Security.NameFull + "_" + TimeFrameBuilder.Specification;

                _specification =
                    _specification.Replace("(", "")
                        .Replace(")", "")
                        .Replace(" ", "")
                        .Replace("\"", "")
                        .Replace("\\", "")
                        .Replace(";", "")
                        .Replace(":", "")
                        .Replace("/", "");


                return _specification;
            }
        }

        public bool IsMergedByCandlesFromFile;

        /// <summary>
        /// бумага по которой собираются свечи
        /// </summary>
        public Security Security;

        /// <summary>
        /// таймФрейм собираемых свечей в виде TimeSpan
        /// </summary>
        public TimeSpan TimeFrameSpan
        {
            get { return TimeFrameBuilder.TimeFrameTimeSpan; }
        }

        /// <summary>
        /// таймфрейм в виде перечисления
        /// </summary>
        public TimeFrame TimeFrame
        {
            get { return TimeFrameBuilder.TimeFrame; }
            set
            {
                TimeFrameBuilder.TimeFrame = value;
            }
        }

        /// <summary>
        /// все собранные свечи в этой серии
        /// </summary>
        public List<Candle> CandlesAll;

        public List<Candle> CandlesOnlyReady
        {
            get
            {
                if (CandlesAll == null)
                {
                    return null;
                }

                List<Candle> history = CandlesAll;

                if (CandlesAll == null ||
                    CandlesAll.Count == 0)
                {
                    return null;
                }

                if (CandlesAll[CandlesAll.Count - 1].State != CandleState.Finished)
                {
                    return CandlesAll.GetRange(0, CandlesAll.Count - 1);
                }

                return history;
            }
        }

        /// <summary>
        /// флаг. Прогружена ли серия первичными данными
        /// </summary>
        public bool IsStarted
        {
            get { return _isStarted; }
            set { _isStarted = value; }
        }

        private bool _isStarted;

        /// <summary>
        /// нужно ли продолжать прогружать объект
        /// </summary>
        private bool _isStoped;

        /// <summary>
        /// остановить расчёт серии
        /// </summary>
        public void Stop()
        {
            _isStoped = true;
        }

        /// <summary>
        /// очистить
        /// </summary>
        public void Clear()
        {
            _lastTradeIndex = 0;

            if (CandlesAll != null)
            {
                CandlesAll.Clear();
                CandlesAll = null;
            }
        }

        // приём изменившегося времени

        /// <summary>
        /// добавить в серию новое время сервера
        /// </summary>
        /// <param name="time">новое время</param>
        public void SetNewTime(DateTime time)
        {
            if (_isStoped || _isStarted == false)
            {
                return;
            }

            if (CandlesAll == null || CandlesAll.Count == 0)
            {
                return;
            }

            if (_startProgram != StartProgram.IsOsTrader)
            {
                return;
            }

            if (TimeFrameBuilder.CandleCreateMethodType != CandleCreateMethodType.Simple)
            {
                return;
            }

            Candle lastCandle = CandlesAll[CandlesAll.Count - 1];

            if (
                (
                    (lastCandle.TimeStart.Add(TimeFrameSpan) < time.AddSeconds(-5))
                    ||
                    (TimeFrame == TimeFrame.Day
                     && lastCandle.TimeStart.Date < time.Date)
                )
                &&
                lastCandle.State != CandleState.Finished)
            {
                // пришло время закрыть свечу
                lastCandle.State = CandleState.Finished;

                UpdateFinishCandle();
            }
        }

        // сбор свечек из тиков

        /// <summary>
        /// добавить в серию новые тики
        /// </summary>
        /// <param name="trades">новые тики</param>
        public void SetNewTicks(List<Trade> trades)
        {
            if (_isStoped || _isStarted == false)
            {
                return;
            }
            if (TimeFrameBuilder.CandleMarketDataType == CandleMarketDataType.MarketDepth)
            {
                return;
            }

            if (_startProgram != StartProgram.IsOsTrader &&
                _startProgram != StartProgram.IsOsData &&
                _startProgram != StartProgram.IsOsConverter &&
                TypeTesterData == TesterDataType.Candle)
            {
                return;
            }

            try
            {
                Trade lastTrade = trades[trades.Count - 1];

                if (lastTrade == null)
                {
                    return;
                }

                if (_lastTradeTime > lastTrade.Time)
                {
                    return;
                }
            }
            catch
            {
                return;
            }

            List<Trade> newTrades = GetActualTrades(trades);

            if (newTrades == null)
            {
                return;
            }

            // обновилось неизвесное кол-во тиков
            for (int i = 0; i < newTrades.Count; i++)
            {
                Trade trade = newTrades[i];

                if (trade == null)
                {
                    continue;
                }

                if (CandlesAll != null &&
                   CandlesAll[CandlesAll.Count - 1].TimeStart > trade.Time)
                {
                    continue;
                }

                if (TimeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Simple)
                { // при классической сборке свечек. Когда мы точно знаем когда у свечи закрытие
                    bool saveInNextCandle = true;

                    if (TimeFrameBuilder.SaveTradesInCandles &&
                        CandlesAll[CandlesAll.Count - 1].TimeStart.Add(TimeFrameSpan) > trade.Time)
                    {
                        CandlesAll[CandlesAll.Count - 1].Trades.Add(trade);
                        saveInNextCandle = false;
                    }

                    UpDateCandle(trade.Time, trade.Price, trade.Volume, true, trade.Side);

                    if (TimeFrameBuilder.SaveTradesInCandles
                        && saveInNextCandle)
                    {
                        CandlesAll[CandlesAll.Count - 1].Trades.Add(trade);
                    }
                }
                else
                { // при любым другом виде свечек

                    UpDateCandle(trade.Time, trade.Price, trade.Volume, true, trade.Side);

                    if (TimeFrameBuilder.SaveTradesInCandles)
                    {
                        CandlesAll[CandlesAll.Count - 1].Trades.Add(trade);
                    }
                }
            }
        }

        /// <summary>
        /// индекс тика на последней итерации
        /// </summary>
        private int _lastTradeIndex;

        private DateTime _lastTradeTime;

        private List<Trade> GetActualTrades(List<Trade> trades)
        {

            List<Trade> newTrades = new List<Trade>();

            if (trades.Count > 1000)
            { // если удаление трейдов из системы выключено

                int newTradesCount = trades.Count - _lastTradeIndex;

                if (newTradesCount <= 0)
                {
                    return null;
                }

                newTrades = trades.GetRange(_lastTradeIndex, newTradesCount);
            }
            else
            {
                if (_lastTradeTime == DateTime.MinValue)
                {
                    newTrades = trades;
                }
                else
                {
                    for (int i = 0; i < trades.Count; i++)
                    {
                        try
                        {
                            if (trades[i] == null)
                            {
                                continue;
                            }
                            if (trades[i].Time < _lastTradeTime)
                            {
                                continue;
                            }
                            if (trades[i].Time == _lastTradeTime)
                            {
                                if (string.IsNullOrEmpty(trades[i].Id))
                                {
                                    // если IDшников нет - просто игнорируем трейды с идентичным временем
                                    continue;
                                }
                                else
                                {
                                    if (IsInArrayTradeIds(trades[i].Id))
                                    {// если IDшник в последних 100 трейдах
                                        continue;
                                    }
                                }
                            }

                            newTrades.Add(trades[i]);
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
            }

            _lastTradeIndex = trades.Count;

            for (int i2 = 0; i2 < newTrades.Count; i2++)
            {
                if (newTrades[i2] == null)
                {
                    newTrades.RemoveAt(i2);
                    i2--;
                }
                if (string.IsNullOrEmpty(newTrades[i2].Id) == false)
                {
                    AddInListTradeIds(newTrades[i2].Id);
                }
            }

            if (newTrades.Count == 0)
            {
                return null;
            }

            _lastTradeTime = newTrades[newTrades.Count - 1].Time;

            return newTrades;
        }

        List<string> _lastTradeIds = new List<string>();

        private void AddInListTradeIds(string id)
        {
            _lastTradeIds.Add(id);

            if (_lastTradeIds.Count > 50)
            {
                _lastTradeIds.RemoveAt(0);
            }
        }

        private bool IsInArrayTradeIds(string id)
        {
            bool isInArray = false;

            for (int i = 0; i < _lastTradeIds.Count; i++)
            {
                if (_lastTradeIds[i].Equals(id))
                {
                    return true;
                }
            }

            return isInArray;
        }

        /// <summary>
        /// добавить в серию новые тики, но свечи прогрузить только один раз, в конце
        /// </summary>
        /// <param name="trades">тики</param>
        public void PreLoad(List<Trade> trades)
        {
            if (TimeFrameBuilder.CandleMarketDataType == CandleMarketDataType.MarketDepth)
            {
                return;
            }
            if (trades == null || trades.Count == 0)
            {
                _isStarted = true;
                return;
            }

            for (int i = 0; i < trades.Count; i++)
            {
                if (trades[i] == null)
                {
                    continue;
                }
                UpDateCandle(trades[i].Time, trades[i].Price, trades[i].Volume, false, trades[i].Side);

                List<Trade> tradesInCandle = CandlesAll[CandlesAll.Count - 1].Trades;

                tradesInCandle.Add(trades[i]);

                CandlesAll[CandlesAll.Count - 1].Trades = tradesInCandle;
            }
            UpdateChangeCandle();

            _isStarted = true;
        }

        /// <summary>
        /// прогрузить свечу новыми данными
        /// </summary>
        /// <param name="time">время новых данных</param>
        /// <param name="price">цена</param>
        /// <param name="volume">объём</param>
        /// <param name="canPushUp">можно ли передовать сведения о свечках выше</param>
        /// <param name="side">сторона в которую прошла последняя сделка</param>
        private void UpDateCandle(DateTime time, decimal price, decimal volume, bool canPushUp, Side side)
        {
            if (TimeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Simple)
            {
                UpDateSimpleTimeFrame(time, price, volume, canPushUp);
            }
            else if (TimeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Delta)
            {
                UpDateDeltaTimeFrame(time, price, volume, canPushUp, side);
            }
            else if (TimeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Ticks)
            {
                UpDateTickTimeFrame(time, price, volume, canPushUp);
            }
            else if (TimeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Volume)
            {
                UpDateVolumeTimeFrame(time, price, volume, canPushUp);
            }
            else if (TimeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Renko)
            {
                UpDateRencoTimeFrame(time, price, volume, canPushUp);
            }
            else if (TimeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.HeikenAshi)
            {
                UpDateHeikenAshiCandle(time, price, volume, canPushUp);
            }
            else if (TimeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Range)
            {
                UpDateRangeCandles(time, price, volume, canPushUp);
            }
            else if (TimeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Rеvers)
            {
                UpDateReversCandles(time, price, volume, canPushUp);
            }
        }

        private void UpDateRangeCandles(DateTime time, decimal price, decimal volume, bool canPushUp)
        {
            if (CandlesAll != null && CandlesAll.Count > 0 && CandlesAll[CandlesAll.Count - 1] != null &&
              CandlesAll[CandlesAll.Count - 1].TimeStart > time)
            {// если пришли старые данные
                return;
            }

            if (CandlesAll == null)
            {
                // пришла первая сделка
                CandlesAll = new List<Candle>();

                DateTime timeNextCandle = time;

                while (timeNextCandle.Second % 1 != 0)
                {
                    timeNextCandle = timeNextCandle.AddSeconds(-1);
                }


                while (timeNextCandle.Millisecond != 0)
                {
                    timeNextCandle = timeNextCandle.AddMilliseconds(-1);
                }

                timeNextCandle = new DateTime(timeNextCandle.Year, timeNextCandle.Month, timeNextCandle.Day,
                    timeNextCandle.Hour, timeNextCandle.Minute, timeNextCandle.Second, timeNextCandle.Millisecond);

                Candle candle = new Candle()
                {
                    Close = price,
                    High = price,
                    Low = price,
                    Open = price,
                    State = CandleState.Started,
                    TimeStart = timeNextCandle,
                    Volume = volume
                };

                CandlesAll.Add(candle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }



            if (CandlesAll != null &&
                CandlesAll[CandlesAll.Count - 1].High - CandlesAll[CandlesAll.Count - 1].Low >= TimeFrameBuilder.RangeCandlesPunkts)
            {
                // если пришли данные из новой свечки

                if (CandlesAll[CandlesAll.Count - 1].State != CandleState.Finished)
                {
                    // если последнюю свечку ещё не закрыли и не отправили
                    CandlesAll[CandlesAll.Count - 1].State = CandleState.Finished;

                    if (canPushUp)
                    {
                        UpdateFinishCandle();
                    }
                }

                DateTime timeNextCandle = time;

                while (timeNextCandle.Millisecond != 0)
                {
                    timeNextCandle = timeNextCandle.AddMilliseconds(-1);
                }

                timeNextCandle = new DateTime(timeNextCandle.Year, timeNextCandle.Month, timeNextCandle.Day,
                    timeNextCandle.Hour, timeNextCandle.Minute, timeNextCandle.Second, timeNextCandle.Millisecond);

                Candle newCandle = new Candle()
                {
                    Close = price,
                    High = price,
                    Low = price,
                    Open = price,
                    State = CandleState.Started,
                    TimeStart = timeNextCandle,
                    Volume = volume
                };

                CandlesAll.Add(newCandle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }

            if (CandlesAll != null &&
                CandlesAll[CandlesAll.Count - 1].High - CandlesAll[CandlesAll.Count - 1].Low < TimeFrameBuilder.RangeCandlesPunkts)
            {
                // если пришли данные внутри свечи

                CandlesAll[CandlesAll.Count - 1].Volume += volume;
                CandlesAll[CandlesAll.Count - 1].Close = price;

                if (CandlesAll[CandlesAll.Count - 1].High < price)
                {
                    CandlesAll[CandlesAll.Count - 1].High = price;
                }

                if (CandlesAll[CandlesAll.Count - 1].Low > price)
                {
                    CandlesAll[CandlesAll.Count - 1].Low = price;
                }

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }
            }
        }

        private void UpDateReversCandles(DateTime time, decimal price, decimal volume, bool canPushUp)
        {
            if (CandlesAll != null && CandlesAll.Count > 0 && CandlesAll[CandlesAll.Count - 1] != null &&
             CandlesAll[CandlesAll.Count - 1].TimeStart > time)
            {// если пришли старые данные
                return;
            }

            if (CandlesAll == null)
            {
                // пришла первая сделка
                CandlesAll = new List<Candle>();

                DateTime timeNextCandle = time;

                while (timeNextCandle.Second % 1 != 0)
                {
                    timeNextCandle = timeNextCandle.AddSeconds(-1);
                }


                while (timeNextCandle.Millisecond != 0)
                {
                    timeNextCandle = timeNextCandle.AddMilliseconds(-1);
                }

                timeNextCandle = new DateTime(timeNextCandle.Year, timeNextCandle.Month, timeNextCandle.Day,
                    timeNextCandle.Hour, timeNextCandle.Minute, timeNextCandle.Second, timeNextCandle.Millisecond);

                Candle candle = new Candle()
                {
                    Close = price,
                    High = price,
                    Low = price,
                    Open = price,
                    State = CandleState.Started,
                    TimeStart = timeNextCandle,
                    Volume = volume
                };

                CandlesAll.Add(candle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }

            bool candleReady = false;

            Candle lastCandle = CandlesAll[CandlesAll.Count - 1];

            if (lastCandle.High - lastCandle.Open >= TimeFrameBuilder.ReversCandlesPunktsMinMove && //движение нужное есть
                lastCandle.High - lastCandle.Close >= TimeFrameBuilder.ReversCandlesPunktsBackMove) // откат имеется
            { // есть откат от хая
                candleReady = true;
            }

            if (lastCandle.Open - lastCandle.Low >= TimeFrameBuilder.ReversCandlesPunktsMinMove && //движение нужное есть
                lastCandle.Close - lastCandle.Low >= TimeFrameBuilder.ReversCandlesPunktsBackMove) // откат имеется
            { // есть откат от лоя
                candleReady = true;
            }

            if (CandlesAll != null &&
                candleReady)
            {
                // если пришли данные из новой свечки

                if (CandlesAll[CandlesAll.Count - 1].State != CandleState.Finished)
                {
                    // если последнюю свечку ещё не закрыли и не отправили
                    CandlesAll[CandlesAll.Count - 1].State = CandleState.Finished;

                    if (canPushUp)
                    {
                        UpdateFinishCandle();
                    }
                }

                DateTime timeNextCandle = time;

                while (timeNextCandle.Millisecond != 0)
                {
                    timeNextCandle = timeNextCandle.AddMilliseconds(-1);
                }

                timeNextCandle = new DateTime(timeNextCandle.Year, timeNextCandle.Month, timeNextCandle.Day,
                    timeNextCandle.Hour, timeNextCandle.Minute, timeNextCandle.Second, timeNextCandle.Millisecond);

                Candle newCandle = new Candle()
                {
                    Close = price,
                    High = price,
                    Low = price,
                    Open = price,
                    State = CandleState.Started,
                    TimeStart = timeNextCandle,
                    Volume = volume
                };

                CandlesAll.Add(newCandle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }

            if (CandlesAll != null &&
                candleReady == false)
            {
                // если пришли данные внутри свечи

                CandlesAll[CandlesAll.Count - 1].Volume += volume;
                CandlesAll[CandlesAll.Count - 1].Close = price;

                if (CandlesAll[CandlesAll.Count - 1].High < price)
                {
                    CandlesAll[CandlesAll.Count - 1].High = price;
                }

                if (CandlesAll[CandlesAll.Count - 1].Low > price)
                {
                    CandlesAll[CandlesAll.Count - 1].Low = price;
                }

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }
            }
        }

        /// <summary>
        /// обновить свечи Heiken Ashi
        /// </summary>
        private void UpDateHeikenAshiCandle(DateTime time, decimal price, decimal volume, bool canPushUp)
        {
            if (CandlesAll != null && CandlesAll.Count > 0 && CandlesAll[CandlesAll.Count - 1] != null &&
                CandlesAll[CandlesAll.Count - 1].TimeStart > time)
            {
                // если пришли старые данные
                return;
            }

            if (CandlesAll == null)
            {
                // пришла первая сделка
                CandlesAll = new List<Candle>();

                DateTime timeNextCandle = time;

                if (TimeFrameSpan.TotalMinutes >= 1)
                {
                    timeNextCandle = time.AddSeconds(-time.Second);

                    while (timeNextCandle.Minute % TimeFrameSpan.TotalMinutes != 0)
                    {
                        timeNextCandle = timeNextCandle.AddMinutes(-1);
                    }

                    while (timeNextCandle.Second != 0)
                    {
                        timeNextCandle = timeNextCandle.AddSeconds(-1);
                    }
                }
                else
                {
                    while (timeNextCandle.Second % TimeFrameSpan.TotalSeconds != 0)
                    {
                        timeNextCandle = timeNextCandle.AddSeconds(-1);
                    }
                }

                while (timeNextCandle.Millisecond != 0)
                {
                    timeNextCandle = timeNextCandle.AddMilliseconds(-1);
                }

                timeNextCandle = new DateTime(timeNextCandle.Year, timeNextCandle.Month, timeNextCandle.Day,
                    timeNextCandle.Hour, timeNextCandle.Minute, timeNextCandle.Second, timeNextCandle.Millisecond);

                Candle candle = new Candle()
                {
                    Close = price,
                    High = price,
                    Low = price,
                    Open = price,
                    State = CandleState.Started,
                    TimeStart = timeNextCandle,
                    Volume = volume
                };

                CandlesAll.Add(candle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }

            if (CandlesAll[CandlesAll.Count - 1].TimeStart.Add(TimeFrameSpan + TimeFrameSpan) <= time &&
                TimeFrameBuilder.SetForeign)
            {
                // произошёл пропуск данных в результате клиринга или перерыва в торгах
                SetForeign(time);
            }

            if (
                (
                  CandlesAll != null &&
                  CandlesAll[CandlesAll.Count - 1].TimeStart < time &&
                  CandlesAll[CandlesAll.Count - 1].TimeStart.Add(TimeFrameSpan) <= time
                )
                ||
                (
                  TimeFrame == TimeFrame.Day &&
                  CandlesAll[CandlesAll.Count - 1].TimeStart.Date < time.Date
                )
                )
            {
                // если пришли данные из новой свечки
                CandlesAll[CandlesAll.Count - 1].Close = Math.Round((CandlesAll[CandlesAll.Count - 1].Open +
                                                          CandlesAll[CandlesAll.Count - 1].High +
                                                          CandlesAll[CandlesAll.Count - 1].Low +
                                                          CandlesAll[CandlesAll.Count - 1].Close) / 4, Security.Decimals);

                if (CandlesAll[CandlesAll.Count - 1].State != CandleState.Finished)
                {
                    // если последнюю свечку ещё не закрыли и не отправили
                    CandlesAll[CandlesAll.Count - 1].State = CandleState.Finished;

                    if (canPushUp)
                    {
                        UpdateFinishCandle();
                    }
                }

                DateTime timeNextCandle = time;

                if (TimeFrameSpan.TotalMinutes >= 1)
                {
                    timeNextCandle = time.AddSeconds(-time.Second);

                    while (timeNextCandle.Minute % TimeFrameSpan.TotalMinutes != 0 &&
                        TimeFrame != TimeFrame.Min45 && TimeFrame != TimeFrame.Min3)
                    {
                        timeNextCandle = timeNextCandle.AddMinutes(-1);
                    }

                    while (timeNextCandle.Second != 0)
                    {
                        timeNextCandle = timeNextCandle.AddSeconds(-1);
                    }
                }
                else
                {
                    while (timeNextCandle.Second % TimeFrameSpan.TotalSeconds != 0)
                    {
                        timeNextCandle = timeNextCandle.AddSeconds(-1);
                    }
                }

                while (timeNextCandle.Millisecond != 0)
                {
                    timeNextCandle = timeNextCandle.AddMilliseconds(-1);
                }

                timeNextCandle = new DateTime(timeNextCandle.Year, timeNextCandle.Month, timeNextCandle.Day,
                    timeNextCandle.Hour, timeNextCandle.Minute, timeNextCandle.Second, timeNextCandle.Millisecond);

                Candle newCandle = new Candle()
                {
                    Close = price,
                    High = price,
                    Low = price,
                    Open = Math.Round((CandlesAll[CandlesAll.Count - 1].Open +
                            CandlesAll[CandlesAll.Count - 1].Close) / 2, Security.Decimals),
                    State = CandleState.Started,
                    TimeStart = timeNextCandle,
                    Volume = volume
                };

                CandlesAll.Add(newCandle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }

            if (CandlesAll != null &&
                CandlesAll[CandlesAll.Count - 1].TimeStart <= time &&
                CandlesAll[CandlesAll.Count - 1].TimeStart.Add(TimeFrameSpan) > time)
            {
                // если пришли данные внутри свечи

                CandlesAll[CandlesAll.Count - 1].Volume += volume;
                CandlesAll[CandlesAll.Count - 1].Close = price;

                if (CandlesAll[CandlesAll.Count - 1].High < price)
                {
                    CandlesAll[CandlesAll.Count - 1].High = price;
                }

                if (CandlesAll[CandlesAll.Count - 1].Low > price)
                {
                    CandlesAll[CandlesAll.Count - 1].Low = price;
                }

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }
            }
        }

        /// <summary>
        /// обновить свечи с обычным ТФ
        /// </summary>
        private void UpDateSimpleTimeFrame(DateTime time, decimal price, decimal volume, bool canPushUp)
        {
            if (CandlesAll != null
                && CandlesAll.Count > 0
                && CandlesAll[CandlesAll.Count - 1] != null
                &&
                CandlesAll[CandlesAll.Count - 1].TimeStart > time)
            {
                // если пришли старые данные
                return;
            }

            if (CandlesAll == null)
            {
                // пришла первая сделка
                CandlesAll = new List<Candle>();

                DateTime timeNextCandle = time;

                if (TimeFrameSpan.TotalMinutes >= 1)
                {
                    timeNextCandle = time.AddSeconds(-time.Second);

                    while (timeNextCandle.Minute % TimeFrameSpan.TotalMinutes != 0)
                    {
                        timeNextCandle = timeNextCandle.AddMinutes(-1);
                    }

                    while (timeNextCandle.Second != 0)
                    {
                        timeNextCandle = timeNextCandle.AddSeconds(-1);
                    }
                }
                else
                {
                    while (timeNextCandle.Second % TimeFrameSpan.TotalSeconds != 0)
                    {
                        timeNextCandle = timeNextCandle.AddSeconds(-1);
                    }
                }

                while (timeNextCandle.Millisecond != 0)
                {
                    timeNextCandle = timeNextCandle.AddMilliseconds(-1);
                }

                timeNextCandle = new DateTime(timeNextCandle.Year, timeNextCandle.Month, timeNextCandle.Day,
                    timeNextCandle.Hour, timeNextCandle.Minute, timeNextCandle.Second, timeNextCandle.Millisecond);

                Candle candle = new Candle()
                {
                    Close = price,
                    High = price,
                    Low = price,
                    Open = price,
                    State = CandleState.Started,
                    TimeStart = timeNextCandle,
                    Volume = volume
                };

                CandlesAll.Add(candle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }

            if (CandlesAll[CandlesAll.Count - 1].TimeStart.Add(TimeFrameSpan + TimeFrameSpan) <= time &&
                TimeFrameBuilder.SetForeign)
            {
                // произошёл пропуск данных в результате клиринга или перерыва в торгах
                SetForeign(time);
            }

            if (
                (
                  CandlesAll != null &&
                  CandlesAll[CandlesAll.Count - 1].TimeStart < time &&
                  CandlesAll[CandlesAll.Count - 1].TimeStart.Add(TimeFrameSpan) <= time
                )
                ||
                (
                  TimeFrame == TimeFrame.Day &&
                  CandlesAll[CandlesAll.Count - 1].TimeStart.Date < time.Date
                )
                )
            {
                // если пришли данные из новой свечки

                if (CandlesAll[CandlesAll.Count - 1].State != CandleState.Finished)
                {
                    // если последнюю свечку ещё не закрыли и не отправили
                    CandlesAll[CandlesAll.Count - 1].State = CandleState.Finished;

                    if (canPushUp)
                    {
                        UpdateFinishCandle();
                    }
                }

                DateTime timeNextCandle = time;

                if (TimeFrameSpan.TotalMinutes >= 1)
                {
                    timeNextCandle = time.AddSeconds(-time.Second);

                    while (timeNextCandle.Minute % TimeFrameSpan.TotalMinutes != 0 &&
                        TimeFrame != TimeFrame.Min45 && TimeFrame != TimeFrame.Min3)
                    {
                        timeNextCandle = timeNextCandle.AddMinutes(-1);
                    }

                    while (timeNextCandle.Second != 0)
                    {
                        timeNextCandle = timeNextCandle.AddSeconds(-1);
                    }
                }
                else
                {
                    while (timeNextCandle.Second % TimeFrameSpan.TotalSeconds != 0)
                    {
                        timeNextCandle = timeNextCandle.AddSeconds(-1);
                    }
                }

                while (timeNextCandle.Millisecond != 0)
                {
                    timeNextCandle = timeNextCandle.AddMilliseconds(-1);
                }

                timeNextCandle = new DateTime(timeNextCandle.Year, timeNextCandle.Month, timeNextCandle.Day,
                    timeNextCandle.Hour, timeNextCandle.Minute, timeNextCandle.Second, timeNextCandle.Millisecond);

                Candle newCandle = new Candle()
                {
                    Close = price,
                    High = price,
                    Low = price,
                    Open = price,
                    State = CandleState.Started,
                    TimeStart = timeNextCandle,
                    Volume = volume
                };

                CandlesAll.Add(newCandle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }

            if (CandlesAll != null &&
                CandlesAll[CandlesAll.Count - 1].TimeStart <= time &&
                CandlesAll[CandlesAll.Count - 1].TimeStart.Add(TimeFrameSpan) > time)
            {
                // если пришли данные внутри свечи

                if (CandlesAll[CandlesAll.Count - 1].State == CandleState.Finished)
                {
                    CandlesAll[CandlesAll.Count - 1].State = CandleState.Started;
                }

                CandlesAll[CandlesAll.Count - 1].Volume += volume;
                CandlesAll[CandlesAll.Count - 1].Close = price;

                if (CandlesAll[CandlesAll.Count - 1].High < price)
                {
                    CandlesAll[CandlesAll.Count - 1].High = price;
                }

                if (CandlesAll[CandlesAll.Count - 1].Low > price)
                {
                    CandlesAll[CandlesAll.Count - 1].Low = price;
                }

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }
            }
        }

        /// <summary>
        /// текущее накопление дельты
        /// </summary>
        private decimal _currentDelta;

        /// <summary>
        /// обновить свечи с дельтой
        /// </summary>
        private void UpDateDeltaTimeFrame(DateTime time, decimal price, decimal volume, bool canPushUp, Side side)
        {
            // Формула кумулятивной дельты 
            //Delta= ∑_i▒vBuy- ∑_i▒vSell 

            if (CandlesAll != null && CandlesAll.Count > 0 && CandlesAll[CandlesAll.Count - 1] != null &&
               CandlesAll[CandlesAll.Count - 1].TimeStart > time)
            {// если пришли старые данные
                return;
            }

            if (CandlesAll == null)
            {
                // пришла первая сделка
                CandlesAll = new List<Candle>();

                DateTime timeNextCandle = time;

                while (timeNextCandle.Millisecond != 0)
                {
                    timeNextCandle = timeNextCandle.AddMilliseconds(-1);
                }

                timeNextCandle = new DateTime(timeNextCandle.Year, timeNextCandle.Month, timeNextCandle.Day,
                    timeNextCandle.Hour, timeNextCandle.Minute, timeNextCandle.Second, timeNextCandle.Millisecond);

                Candle candle = new Candle()
                {
                    Close = price,
                    High = price,
                    Low = price,
                    Open = price,
                    State = CandleState.Started,
                    TimeStart = timeNextCandle,
                    Volume = volume
                };

                CandlesAll.Add(candle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }

            if (side == Side.Buy)
            {
                _currentDelta += volume;
            }
            else
            {
                _currentDelta -= volume;
            }


            if (CandlesAll != null &&
                Math.Abs(_currentDelta) >= TimeFrameBuilder.DeltaPeriods)
            {
                // если пришли данные из новой свечки

                _currentDelta = 0;

                if (CandlesAll[CandlesAll.Count - 1].State != CandleState.Finished)
                {
                    // если последнюю свечку ещё не закрыли и не отправили
                    CandlesAll[CandlesAll.Count - 1].State = CandleState.Finished;

                    if (canPushUp)
                    {
                        UpdateFinishCandle();
                    }
                }

                DateTime timeNextCandle = time;

                while (timeNextCandle.Millisecond != 0)
                {
                    timeNextCandle = timeNextCandle.AddMilliseconds(-1);
                }

                timeNextCandle = new DateTime(timeNextCandle.Year, timeNextCandle.Month, timeNextCandle.Day,
                    timeNextCandle.Hour, timeNextCandle.Minute, timeNextCandle.Second, timeNextCandle.Millisecond);

                Candle newCandle = new Candle()
                {
                    Close = price,
                    High = price,
                    Low = price,
                    Open = price,
                    State = CandleState.Started,
                    TimeStart = timeNextCandle,
                    Volume = volume
                };

                CandlesAll.Add(newCandle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }

            if (CandlesAll != null)
            {
                // если пришли данные внутри свечи

                CandlesAll[CandlesAll.Count - 1].Volume += volume;
                CandlesAll[CandlesAll.Count - 1].Close = price;

                if (CandlesAll[CandlesAll.Count - 1].High < price)
                {
                    CandlesAll[CandlesAll.Count - 1].High = price;
                }

                if (CandlesAll[CandlesAll.Count - 1].Low > price)
                {
                    CandlesAll[CandlesAll.Count - 1].Low = price;
                }

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }
            }

        }

        /// <summary>
        /// сколько у нас в последней строящейся свечке тиков
        /// </summary>
        private int _lastCandleTickCount;

        /// <summary>
        /// обновить свечи по кол-ву обезличенных сделок в свече
        /// </summary>
        private void UpDateTickTimeFrame(DateTime time, decimal price, decimal volume, bool canPushUp)
        {
            if (CandlesAll != null && CandlesAll.Count > 0 && CandlesAll[CandlesAll.Count - 1] != null &&
               CandlesAll[CandlesAll.Count - 1].TimeStart > time)
            {// если пришли старые данные
                return;
            }

            if (CandlesAll == null)
            {
                // пришла первая сделка
                CandlesAll = new List<Candle>();

                DateTime timeNextCandle = time;

                while (timeNextCandle.Second % 1 != 0)
                {
                    timeNextCandle = timeNextCandle.AddSeconds(-1);
                }


                while (timeNextCandle.Millisecond != 0)
                {
                    timeNextCandle = timeNextCandle.AddMilliseconds(-1);
                }

                Candle candle = new Candle()
                {
                    Close = price,
                    High = price,
                    Low = price,
                    Open = price,
                    State = CandleState.Started,
                    TimeStart = timeNextCandle,
                    Volume = volume
                };

                CandlesAll.Add(candle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                _lastCandleTickCount = 1;
                return;
            }

            if (CandlesAll != null &&
                _lastCandleTickCount >= TimeFrameBuilder.TradeCount)
            {
                // если пришли данные из новой свечки

                if (CandlesAll[CandlesAll.Count - 1].State != CandleState.Finished)
                {
                    // если последнюю свечку ещё не закрыли и не отправили
                    CandlesAll[CandlesAll.Count - 1].State = CandleState.Finished;

                    if (canPushUp)
                    {
                        UpdateFinishCandle();
                    }
                }

                DateTime timeNextCandle = time;

                while (timeNextCandle.Millisecond != 0)
                {
                    timeNextCandle = timeNextCandle.AddMilliseconds(-1);
                }

                Candle newCandle = new Candle()
                {
                    Close = price,
                    High = price,
                    Low = price,
                    Open = price,
                    State = CandleState.Started,
                    TimeStart = timeNextCandle,
                    Volume = volume
                };

                CandlesAll.Add(newCandle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                _lastCandleTickCount = 1;

                return;
            }

            if (CandlesAll != null &&
                 _lastCandleTickCount < TimeFrameBuilder.TradeCount)
            {
                // если пришли данные внутри свечи
                _lastCandleTickCount++;

                CandlesAll[CandlesAll.Count - 1].Volume += volume;
                CandlesAll[CandlesAll.Count - 1].Close = price;

                if (CandlesAll[CandlesAll.Count - 1].High < price)
                {
                    CandlesAll[CandlesAll.Count - 1].High = price;
                }

                if (CandlesAll[CandlesAll.Count - 1].Low > price)
                {
                    CandlesAll[CandlesAll.Count - 1].Low = price;
                }

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }
            }
        }

        /// <summary>
        /// обновить свечи по объёму в свече
        /// </summary>
        private void UpDateVolumeTimeFrame(DateTime time, decimal price, decimal volume, bool canPushUp)
        {
            if (CandlesAll != null && CandlesAll.Count > 0 && CandlesAll[CandlesAll.Count - 1] != null &&
               CandlesAll[CandlesAll.Count - 1].TimeStart > time)
            {// если пришли старые данные
                return;
            }

            if (CandlesAll == null)
            {
                // пришла первая сделка
                CandlesAll = new List<Candle>();

                DateTime timeNextCandle = time;

                while (timeNextCandle.Millisecond != 0)
                {
                    timeNextCandle = timeNextCandle.AddMilliseconds(-1);
                }

                Candle candle = new Candle()
                {
                    Close = price,
                    High = price,
                    Low = price,
                    Open = price,
                    State = CandleState.Started,
                    TimeStart = timeNextCandle,
                    Volume = volume
                };

                CandlesAll.Add(candle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }

            if (CandlesAll != null &&
                CandlesAll[CandlesAll.Count - 1].Volume >= TimeFrameBuilder.VolumeToCloseCandleInVolumeType)
            {
                // если пришли данные из новой свечки

                if (CandlesAll[CandlesAll.Count - 1].State != CandleState.Finished)
                {
                    // если последнюю свечку ещё не закрыли и не отправили
                    CandlesAll[CandlesAll.Count - 1].State = CandleState.Finished;

                    if (canPushUp)
                    {
                        UpdateFinishCandle();
                    }
                }

                DateTime timeNextCandle = time;

                while (timeNextCandle.Millisecond != 0)
                {
                    timeNextCandle = timeNextCandle.AddMilliseconds(-1);
                }


                Candle newCandle = new Candle()
                {
                    Close = price,
                    High = price,
                    Low = price,
                    Open = price,
                    State = CandleState.Started,
                    TimeStart = timeNextCandle,
                    Volume = volume
                };

                CandlesAll.Add(newCandle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }

            if (CandlesAll != null)
            {
                // если пришли данные внутри свечи

                CandlesAll[CandlesAll.Count - 1].Volume += volume;
                CandlesAll[CandlesAll.Count - 1].Close = price;

                if (CandlesAll[CandlesAll.Count - 1].High < price)
                {
                    CandlesAll[CandlesAll.Count - 1].High = price;
                }

                if (CandlesAll[CandlesAll.Count - 1].Low > price)
                {
                    CandlesAll[CandlesAll.Count - 1].Low = price;
                }

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }
            }
        }

        private decimal _rencoStartPrice;

        private Side _rencoLastSide;

        private bool _rencoIsBuildShadows;

        private void UpDateRencoTimeFrame(DateTime time, decimal price, decimal volume, bool canPushUp)
        {
            if (CandlesAll != null && CandlesAll.Count > 0 && CandlesAll[CandlesAll.Count - 1] != null &&
               CandlesAll[CandlesAll.Count - 1].TimeStart > time)
            {// если пришли старые данные
                return;
            }

            if (CandlesAll == null)
            {
                _rencoStartPrice = price;
                _rencoLastSide = Side.None;
                // пришла первая сделка
                CandlesAll = new List<Candle>();

                DateTime timeNextCandle = time;

                while (timeNextCandle.Millisecond != 0)
                {
                    timeNextCandle = timeNextCandle.AddMilliseconds(-1);
                }

                Candle candle = new Candle()
                {
                    Close = price,
                    High = price,
                    Low = price,
                    Open = price,
                    State = CandleState.Started,
                    TimeStart = timeNextCandle,
                    Volume = volume
                };

                CandlesAll.Add(candle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }

            decimal renDist = TimeFrameBuilder.RencoPunktsToCloseCandleInRencoType;

            if (
                (_rencoLastSide == Side.None && Math.Abs(_rencoStartPrice - price) >= renDist)
                ||
                (_rencoLastSide == Side.Buy && price - _rencoStartPrice >= renDist)
                ||
                (_rencoLastSide == Side.Buy && _rencoStartPrice - price >= renDist * 2)
                ||
                (_rencoLastSide == Side.Sell && _rencoStartPrice - price >= renDist)
                ||
                (_rencoLastSide == Side.Sell && price - _rencoStartPrice >= renDist * 2)
                )
            {
                // если пришли данные из новой свечки

                Candle lastCandle = CandlesAll[CandlesAll.Count - 1];



                if (
                    (_rencoLastSide == Side.None && price - _rencoStartPrice >= renDist)
                    ||
                    (_rencoLastSide == Side.Buy && price - _rencoStartPrice >= renDist)
                    )
                {
                    _rencoLastSide = Side.Buy;
                    _rencoStartPrice = _rencoStartPrice + renDist;
                    lastCandle.High = _rencoStartPrice;
                }
                else if (
                (_rencoLastSide == Side.None && _rencoStartPrice - price >= renDist)
                ||
                (_rencoLastSide == Side.Sell && _rencoStartPrice - price >= renDist)
                )
                {
                    _rencoLastSide = Side.Sell;
                    _rencoStartPrice = _rencoStartPrice - renDist;
                    lastCandle.Low = _rencoStartPrice;
                }
                else if (
                    _rencoLastSide == Side.Buy && _rencoStartPrice - price >= renDist * 2)
                {
                    _rencoLastSide = Side.Sell;
                    lastCandle.Open = _rencoStartPrice - renDist;
                    _rencoStartPrice = _rencoStartPrice - renDist * 2;
                    lastCandle.Low = _rencoStartPrice;
                }
                else if (
                    _rencoLastSide == Side.Sell && price - _rencoStartPrice >= renDist * 2)
                {
                    _rencoLastSide = Side.Buy;
                    lastCandle.Open = _rencoStartPrice + renDist;
                    _rencoStartPrice = _rencoStartPrice + renDist * 2;
                    lastCandle.High = _rencoStartPrice;
                }

                lastCandle.Close = _rencoStartPrice;

                if (TimeFrameBuilder.RencoIsBuildShadows == false)
                {
                    if (lastCandle.IsUp)
                    {
                        lastCandle.Low = lastCandle.Open;
                        lastCandle.High = lastCandle.Close;
                    }
                    else
                    {
                        lastCandle.High = lastCandle.Open;
                        lastCandle.Low = lastCandle.Close;
                    }
                }

                if (CandlesAll[CandlesAll.Count - 1].State != CandleState.Finished)
                {
                    // если последнюю свечку ещё не закрыли и не отправили
                    CandlesAll[CandlesAll.Count - 1].State = CandleState.Finished;

                    if (canPushUp)
                    {
                        UpdateFinishCandle();
                    }
                }

                DateTime timeNextCandle = time;

                while (timeNextCandle.Millisecond != 0)
                {
                    timeNextCandle = timeNextCandle.AddMilliseconds(-1);
                }


                Candle newCandle = new Candle()
                {
                    Close = _rencoStartPrice,
                    High = _rencoStartPrice,
                    Low = _rencoStartPrice,
                    Open = _rencoStartPrice,
                    State = CandleState.Started,
                    TimeStart = timeNextCandle,
                    Volume = volume
                };

                CandlesAll.Add(newCandle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }

            if (CandlesAll != null)
            {
                // если пришли данные внутри свечи

                CandlesAll[CandlesAll.Count - 1].Volume += volume;
                CandlesAll[CandlesAll.Count - 1].Close = price;

                if (CandlesAll[CandlesAll.Count - 1].High < price)
                {
                    CandlesAll[CandlesAll.Count - 1].High = price;
                }

                if (CandlesAll[CandlesAll.Count - 1].Low > price)
                {
                    CandlesAll[CandlesAll.Count - 1].Low = price;
                }

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }
            }

        }

        /// <summary>
        /// заставляет выслать на верх все имеющиеся свечки
        /// </summary>
        public void UpdateAllCandles()
        {
            if (CandlesAll == null)
            {
                return;
            }

            UpdateChangeCandle();

        }

        /// <summary>
        /// метод пересылающий готовые свечи выше
        /// </summary>
        private void UpdateChangeCandle()
        {
            if (_startProgram == StartProgram.IsTester &&
                (TypeTesterData == TesterDataType.MarketDepthOnlyReadyCandle ||
                TypeTesterData == TesterDataType.TickOnlyReadyCandle))
            {
                return;
            }
            if (СandleUpdeteEvent != null)
            {
                СandleUpdeteEvent(this);
            }
        }

        private void UpdateFinishCandle()
        {
            if (СandleFinishedEvent != null)
            {
                СandleFinishedEvent(this);
            }
        }

        /// <summary>
        /// добавить свечи в неторговые периоды
        /// </summary>
        private void SetForeign(DateTime now)
        {
            if (CandlesAll == null ||
                CandlesAll.Count == 1)
            {
                return;
            }

            for (int i = 0; i < CandlesAll.Count; i++)
            {
                if ((i + 1 < CandlesAll.Count &&
                     CandlesAll[i].TimeStart.Add(TimeFrameSpan) < CandlesAll[i + 1].TimeStart)
                    ||
                    (i + 1 == CandlesAll.Count &&
                     CandlesAll[i].TimeStart.Add(TimeFrameSpan) < now))
                {
                    Candle candle = new Candle();
                    candle.TimeStart = CandlesAll[i].TimeStart.Add(TimeFrameSpan);
                    candle.High = CandlesAll[i].Close;
                    candle.Open = CandlesAll[i].Close;
                    candle.Low = CandlesAll[i].Close;
                    candle.Close = CandlesAll[i].Close;
                    candle.Volume = 1;

                    CandlesAll.Insert(i + 1, candle);
                }
            }
        }

        // прямая загрузка серии из свечек

        /// <summary>
        /// загрузить в серию новую свечку
        /// </summary>
        /// <param name="candle"></param>
        public void SetNewCandleInArray(Candle candle)
        {
            if (TimeFrameBuilder.CandleMarketDataType == CandleMarketDataType.MarketDepth)
            {
                return;
            }

            if (CandlesAll == null)
            {
                CandlesAll = new List<Candle>();
            }

            if (CandlesAll.Count == 0)
            {
                CandlesAll.Add(candle);
                return;
            }

            if (CandlesAll[CandlesAll.Count - 1].TimeStart > candle.TimeStart)
            {
                return;
            }

            if (CandlesAll[CandlesAll.Count - 1].TimeStart == candle.TimeStart)
            {
                CandlesAll[CandlesAll.Count - 1] = candle;

                UpdateFinishCandle();
                return;
            }

            CandlesAll.Add(candle);

            UpdateFinishCandle();

        }

        /// <summary>
        /// в серии изменилась последняя свеча
        /// </summary>
        public event Action<CandleSeries> СandleUpdeteEvent;

        /// <summary>
        /// в серии завершилась последняя свеча
        /// </summary>
        public event Action<CandleSeries> СandleFinishedEvent;

        // создание свечек из Стакана

        public void SetNewMarketDepth(MarketDepth marketDepth)
        {
            if (_isStarted == false)
            {
                return;
            }

            if (_isStoped)
            {
                return;
            }

            if (TimeFrameBuilder.CandleMarketDataType != CandleMarketDataType.MarketDepth)
            {
                return;
            }


            if (marketDepth.Bids == null ||
                marketDepth.Bids.Count == 0 ||
                marketDepth.Bids[0].Price == 0)
            {
                return;
            }

            if (marketDepth.Asks == null ||
                marketDepth.Asks.Count == 0 ||
                marketDepth.Asks[0].Price == 0)
            {
                return;
            }

            decimal price = marketDepth.Bids[0].Price + (marketDepth.Asks[0].Price - marketDepth.Bids[0].Price) / 2;

            UpDateCandle(marketDepth.Time, price, 1, true, Side.None);
        }


        // для тестера

        public TesterDataType TypeTesterData;
    }

    /// <summary>
    /// тип данных для рассчёта свечек в серии свечей
    /// </summary>
    public enum CandleMarketDataType
    {
        /// <summary>
        /// создание свечей из тиков
        /// </summary>
        Tick,

        /// <summary>
        /// создание свечей из центра стакана
        /// </summary>
        MarketDepth
    }

    /// <summary>
    /// метод создания свечей
    /// </summary>
    public enum CandleCreateMethodType
    {
        /// <summary>
        /// свечи с обычным ТФ от 1 секунды и выше
        /// </summary>
        Simple,

        /// <summary>
        /// свечи ренко
        /// </summary>
        Renko,

        /// <summary>
        /// свечи набираются из объёма
        /// </summary>
        Volume,

        /// <summary>
        /// свечи набираемые из тиков
        /// </summary>
        Ticks,

        /// <summary>
        /// свечи завершением которых служит изменение дельты на N открытого интереса
        /// </summary>
        Delta,

        /// <summary>
        /// свечи хейкен аши
        /// </summary>
        HeikenAshi,

        /// <summary>
        /// реверсивные свечи
        /// </summary>
        Rеvers,

        /// <summary>
        /// рендж бары
        /// </summary>
        Range
    }
}
