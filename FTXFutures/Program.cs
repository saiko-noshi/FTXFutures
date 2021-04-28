using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace FTXFutures
{
    class Program
    {
        static decimal Truncate(decimal d, byte decimals)
        {
            decimal r = Math.Round(d, decimals);

            if (d > 0 && r > d)
            {
                return r - new decimal(1, 0, 0, false, decimals);
            }
            else if (d < 0 && r < d)
            {
                return r + new decimal(1, 0, 0, false, decimals);
            }

            return r;
        }

        static async Task<bool> GetFutures(string month)
        {
            var client = new HttpClient();

            var method = HttpMethod.Get;
            var endpoint = $"/api/futures";
            var request = new HttpRequestMessage(method, "https://ftx.com" + endpoint);

            var response = client.SendAsync(request).Result;
            if (response != null)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                dynamic json = JsonConvert.DeserializeObject<object>(jsonString);
                var premiums = new List<dynamic>();
                foreach (var obj in json["result"]) // loop through futures
                {
                    if (obj["perpetual"] == "true" || Convert.ToDateTime(obj["expiry"].ToString()).Month.ToString() != month || obj["bid"] == null
                    || obj["ask"] == null || obj["name"].ToString().Contains("MOVE")) continue; // filter out perp contracts

                    decimal premium = (((Convert.ToDecimal(obj["index"]) - Convert.ToDecimal(obj["bid"])) /
                                   Math.Abs(Convert.ToDecimal(obj["bid"]))) * 100) * -1;
                    obj["premium"] = premium; // save premium

                    decimal discount = (((Convert.ToDecimal(obj["index"]) - Convert.ToDecimal(obj["ask"])) /
                                        Math.Abs(Convert.ToDecimal(obj["ask"]))) * 100) * -1;
                    obj["discount"] = discount; // save discount
                    premiums.Add(obj); // add future to list
                }

                Console.WriteLine("{0," + (Console.WindowWidth / 2 + 3) + "}", "//// PREMIUMS ////");
                // print the biggest premiums
                for (int i = 0; i < 5; i++)
                {
                    var biggestPremium = premiums.Max(r => r["premium"]); // get the biggest premium from list
                    if (biggestPremium > 0)
                    {
                        //var future = dict.FirstOrDefault(x => x.Value["premium"] == biggestPremium).Key.ToString();
                        var future = premiums.FirstOrDefault(x => x["premium"] == biggestPremium);
                        var daysToExpiry = (365 - new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day)
                                               .Subtract(new DateTime(DateTime.Now.Year, 1, 1)).TotalDays)
                                           / (DateTime.Now - Convert.ToDateTime(future["expiry"].ToString())).TotalDays;
                        Console.WriteLine((i + 1) + ") " + "Biggest premium found in future " +
                                          future["name"] + " with a premium of " +
                                          Truncate(Convert.ToDecimal(biggestPremium), 4) + "%" +
                                          " - annualised (from today): "
                                          + Truncate(Convert.ToDecimal(
                                              (biggestPremium * Math.Floor(daysToExpiry) * -1)), 4) +
                                          "% return"); // annually: premium * (amount of days left in the year / days from now to futures expiry), rounded down
                        premiums.Remove(future);
                    }
                    else
                    {
                        if(i <= 0)
                            Console.WriteLine("No premiums found.");
                        break;
                    }
                }

                // print the biggest discounts
                Console.WriteLine();
                Console.WriteLine("{0," + (Console.WindowWidth / 2 + 3) + "}", "//// DISCOUNTS ////");
                for (int i = 0; i < 5; i++)
                {
                    var biggestDiscount = premiums.Min(r => r["discount"]); // get the biggest discount from list

                    if (biggestDiscount < 0)
                    {
                        var future = premiums.FirstOrDefault(x => x["discount"] == biggestDiscount);
                        var daysToExpiry = (365 - new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day)
                                                .Subtract(new DateTime(DateTime.Now.Year, 1, 1)).TotalDays)
                                            / (DateTime.Now - Convert.ToDateTime(future["expiry"].ToString()))
                                            .TotalDays;
                        Console.WriteLine((i + 1) + ") " + "Biggest discount found in future " +
                                          future["name"] + " with a discount of " +
                                          Truncate(Convert.ToDecimal(biggestDiscount * -1), 4) + "%" +
                                          " - annualised (from today): "
                                          + Truncate(Convert.ToDecimal(
                                              (biggestDiscount * Math.Floor(daysToExpiry))), 4) +
                                          "% return"); // annually: discount * (amount of days left in the year / days from now to futures expiry), rounded down
                        premiums.Remove(future);
                    }
                    else
                    {
                        if (i <= 0)
                            Console.WriteLine("No discounts found.");
                        break;
                    }
                }

                Console.WriteLine();
            }

            return response.IsSuccessStatusCode;
        }
        static void Main(string[] args)
        {
            while (true)
            {
                Console.WriteLine("Month to filter for: ");
                GetFutures(Console.ReadLine());
            }
        }
    }
}
