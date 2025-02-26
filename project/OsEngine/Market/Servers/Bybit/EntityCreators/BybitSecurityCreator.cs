﻿using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using System;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.Bybit.EntityCreators
{
    public static class BybitSecurityCreator
    {
        public static List<Security> Create(JToken data, string futures_type)
        {
            var securities = new List<Security>();
            var jProperties = data.Children();

            foreach (var jProperty in jProperties)
            {
                try
                {
                    var security = new Security();

                    security.State = SecurityStateType.Activ;
                    security.SecurityType = SecurityType.Futures;

                    var name = jProperty.SelectToken("name").Value<string>();

                    if (!name.Contains("USDT") && futures_type == "Inverse Perpetual")
                    {
                        security.NameId = name;
                        security.NameFull = name;
                        security.Name = name;
                        security.NameClass = jProperty.SelectToken("quote_currency").Value<string>();
                        security.PriceStep = jProperty.SelectToken("price_filter").SelectToken("tick_size").Value<decimal>();
                        security.PriceStepCost = jProperty.SelectToken("price_filter").SelectToken("tick_size").Value<decimal>();
                        security.Lot = 1;
                        security.Decimals = jProperty.SelectToken("price_scale").Value<int>();

                        decimal volumeStep = jProperty.SelectToken("lot_size_filter").SelectToken("qty_step").Value<decimal>();
                        string volumeInstr = volumeStep.ToString().Replace(",", ".");
                        if (volumeInstr.Split('.').Length > 1)
                        {
                            int dv = volumeStep.ToString().Replace(",", ".").Split('.')[1].Length;
                            security.DecimalsVolume = dv;
                        }

                        securities.Add(security);
                    }

                    if (name.Contains("USDT") && futures_type != "Inverse Perpetual")
                    {
                        security.NameId = name;
                        security.NameFull = name;
                        security.Name = name;
                        security.NameClass = jProperty.SelectToken("quote_currency").Value<string>();
                        security.PriceStep = jProperty.SelectToken("price_filter").SelectToken("tick_size").Value<decimal>();
                        security.PriceStepCost = jProperty.SelectToken("price_filter").SelectToken("tick_size").Value<decimal>();

                        decimal volumeStep = jProperty.SelectToken("lot_size_filter").SelectToken("qty_step").Value<decimal>();
                        string volumeInstr = volumeStep.ToString().Replace(",", ".");
                        if (volumeInstr.Split('.').Length > 1)
                        {
                            int dv = volumeStep.ToString().Replace(",", ".").Split('.')[1].Length;
                            security.DecimalsVolume = dv;
                        }

                        security.Lot = 1;
                        security.Decimals = jProperty.SelectToken("price_scale").Value<int>();

                        securities.Add(security);
                    }


                }
                catch (Exception error)
                {
                    throw new Exception("Security creation error \n" + error.ToString());
                }
            }

            return securities;
        }
    }
}
