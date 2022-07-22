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
        public Arrival(string vehicleId, string expectedArrival, string stopCode, string stationName)
        {
            this.vehicleId = vehicleId;
            this.expectedArrival = expectedArrival;
            this.bus_stop = new BusStop(stopCode, stationName);
        }
        public string vehicleId { get; set; }
        public string expectedArrival { get; set; }
        public BusStop bus_stop { get; set; }
    }

    public class BusStop
    {
        public BusStop(string stopCode, string stationName)
        {
            this.stop_code = stopCode;
            this.stationName = stationName;
        }

        public string stop_code { get; set; }
        public string stationName { get; set; }
    }

    public class Location
    {
        public Location(string lat, string longi)
        {
            this.latitude = lat;
            this.longitude = longi;
        }

        public string longitude { get; set; }
        public string latitude { get; set; }
    }

    public class WebRequests
    {
        private static List<Exception> log;
        private static WebRequests instance = new WebRequests();

        private WebRequests()
        {
            log = new List<Exception>();
        }

        public static WebRequests getInstance()
        {
            return instance;
        }
        
        public static string GetJsonResponse(string url)
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
                log.Add(e);
                return null;
            }
        }

        public static void listErrorLog()
        {
            if(log.Count!=0)
                Console.WriteLine("Error log : ");
            foreach (var error in log)
            {
                Console.WriteLine(error.Message);
            }
        }
    }

    public class PostCodeApi
    {
        private static string GetPostCodeApiRequestString(string post_code)
        {
            return $@"https://api.postcodes.io/postcodes/{post_code}";
        }

        public static Location GetLocationForGivenPostCode(string post_code)
        {
            var jsonResponse = WebRequests.GetJsonResponse(GetPostCodeApiRequestString(post_code));

            var postCodeDetailsJson = JObject.Parse(jsonResponse)["result"];

            var longitude = postCodeDetailsJson["longitude"].ToString();
            var latitude = postCodeDetailsJson["latitude"].ToString();
            return new Location( latitude, longitude);
        }
    }

    public class StopPointApi
    {
        private static List<BusStop> GetClosestBusStopsNearLocation(int how_many_bus_stops, Location location)
        {
            List<BusStop> busStops = new List<BusStop>();

            var jsonResponse = WebRequests.GetJsonResponse(GetStopPointClosestStopsToLocationRequestString(location, "NaptanPublicBusCoachTram"));

            if (jsonResponse == null)
                return null;

            var postCodeDetailsJson = JObject.Parse(jsonResponse)["stopPoints"];

            int counter = 0;

            foreach (var stopPoint in postCodeDetailsJson)
            {
                if (counter == how_many_bus_stops)
                    break;
                busStops.Add(new BusStop(stopPoint["naptanId"].ToString(),
                    stopPoint["commonName"].ToString()));
                    counter++;
            }

            return busStops;
        }

        private static string GetStopPointClosestStopsToLocationRequestString(Location location, string StopTypes)
        {
            return $@"https://api.tfl.gov.uk/StopPoint/?lat={location.latitude}&lon={location.longitude}&stopTypes={StopTypes}";
        }

        private static string GetStopPointArrivalPredictionsIDRequestString(string id)
        {
            return $@"https://api.tfl.gov.uk/StopPoint/{id}/Arrivals";
        }

        public static List<Arrival> GetNextArrivalsAtStop(string stop_code, int number_of_buses)
        {
            var arrivals_needed = new List<Arrival>();
            var all_arrivals = new List<Arrival>();

            var jsonResponse = WebRequests.GetJsonResponse(StopPointApi.GetStopPointArrivalPredictionsIDRequestString(stop_code));

            var postCodeDetailsJson = JArray.Parse(jsonResponse);

            foreach (var arrival in postCodeDetailsJson)
                all_arrivals.Add((new Arrival(arrival["vehicleId"].ToString(), arrival["expectedArrival"].ToString(),stop_code, arrival["stationName"].ToString())));

            List<Arrival> sorted = (from a in all_arrivals
                orderby DateTime.Parse(a.expectedArrival)
                select a).ToList();

            int counter = 0;

            foreach (var arrival in sorted)
            {
                if (counter == number_of_buses)
                    break;

                arrivals_needed.Add((new Arrival(arrival.vehicleId,arrival.expectedArrival,stop_code,arrival.bus_stop.stationName)));

                counter++;
            }

            return arrivals_needed;
        }

        public static List<Arrival> GetNextArrivalsForNearestTwoStops(string postcode, int number_of_buses)
        {
            Location location = PostCodeApi.GetLocationForGivenPostCode(postcode);

            var arrivals_needed = new List<Arrival>();
            var all_arrivals = new List<Arrival>();

            List<BusStop> busStops = GetClosestBusStopsNearLocation(2, location);

            if (busStops == null)
                return null;

            foreach (var busStop in busStops)
                all_arrivals.AddRange(GetNextArrivalsAtStop(busStop.stop_code, number_of_buses));

            List<Arrival> sorted = (from a in all_arrivals
                orderby DateTime.Parse(a.expectedArrival)
                select a).ToList();

            int counter = 0;

            foreach (var arrival in sorted)
            {
                if (counter == number_of_buses)
                    break;

                arrivals_needed.Add((new Arrival(arrival.vehicleId, arrival.expectedArrival, arrival.bus_stop.stop_code, arrival.bus_stop.stationName)));

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

            List<Arrival> arrivals = StopPointApi.GetNextArrivalsForNearestTwoStops("NW5 1TX", 5);

            if (arrivals != null)
                foreach (var arrival in arrivals)
                {
                    Console.WriteLine("Bus with vehicleId : " + arrival.vehicleId + " will arrive at :" +
                                      arrival.expectedArrival + " at station : " + arrival.bus_stop.stationName);
                }

            WebRequests.listErrorLog();

            Console.ReadKey();
        }
    }
}