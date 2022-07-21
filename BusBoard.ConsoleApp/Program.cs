using RestSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Newtonsoft.Json.Linq;

namespace BusBoard.ConsoleApp
{
    public class Arrival
    {
        public Arrival(string vehicleId, string expectedArrival)
        {
            this.vehicleId = vehicleId;
            this.expectedArrival = expectedArrival;
        }
        public string vehicleId { get; set; }
        public string expectedArrival { get; set; }
    }

    public class StopPointApi
    {
        private static string GetStopPointArrivalPredictionsIDRequestString(string id)
        {
            return $@"https://api.tfl.gov.uk/StopPoint/{id}/Arrivals";
        }
        private static string GetJsonResponse(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            var responseJson = string.Empty;

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    responseJson = reader.ReadToEnd();
                }

                return responseJson;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public static List<Arrival> GetNextBusesAtStop(string stop_code, int number_of_buses)
        {
            var arrivals_needed = new List<Arrival>();
            var all_arrivals = new List<Arrival>();

            var jsonResponse = StopPointApi.GetJsonResponse(StopPointApi.GetStopPointArrivalPredictionsIDRequestString(stop_code));

            var postCodeDetailsJson = JArray.Parse(jsonResponse);

            foreach (var arrival in postCodeDetailsJson)
                all_arrivals.Add((new Arrival(arrival["vehicleId"].ToString(), arrival["expectedArrival"].ToString())));

            List<Arrival> sorted = (from a in all_arrivals
                orderby DateTime.Parse(a.expectedArrival)
                select a).ToList();

            int counter = 0;

            foreach (var arrival in sorted)
            {
                if (counter == number_of_buses)
                    break;

                arrivals_needed.Add((new Arrival(arrival.vehicleId,arrival.expectedArrival)));

                counter++;
            }

            return arrivals_needed;
        }

    }

    class Program
    {
        static void Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            List<Arrival> buses = StopPointApi.GetNextBusesAtStop("490008660N", 5);

            foreach (var arrival in buses)
            {
                Console.WriteLine("Bus with vehicleId : " + arrival.vehicleId + " will arrive at " + arrival.expectedArrival);
            }

            Console.ReadKey();
        }
    }
}
