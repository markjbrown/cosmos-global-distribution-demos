using Microsoft.Azure.Documents;
using Newtonsoft.Json;
using System;

namespace CosmosGlobalDistDemos
{
    static class Helpers
    {
        //Thread-safe random number generator
        private static Random _global = new Random();
        [ThreadStatic]
        private static Random _local;
        public static int RandomNext()
        {
            Random inst = _local;
            if (inst == null)
            {
                int seed;
                lock (_global) seed = _global.Next();
                _local = inst = new Random(seed);
            }
            return inst.Next();
        }
        public static int RandomNext(int minValue, int maxValue)
        {
            Random inst = _local;
            if (inst == null)
            {
                int seed;
                lock (_global) seed = _global.Next(minValue, maxValue);
                _local = inst = new Random(seed);
            }
            return inst.Next(minValue, maxValue);
        }

        //Return region from Cosmos endpoint
        public static string ParseEndpoint(Uri endPoint)
        {
            string x = endPoint.ToString();

            int tail = x.IndexOf(".documents.azure.com");
            int head = x.LastIndexOf("-") + 1;

            return x.Substring(head, (tail - head));
        }

        //Deep copy Document object
        public static SampleCustomer Clone(SampleCustomer source)
        {
            return JsonConvert.DeserializeObject<SampleCustomer>(JsonConvert.SerializeObject(source));
        }
    }

    public class SampleCustomer : Resource
    {
        [JsonProperty(PropertyName = "id")]
        override public string Id { get; set; }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "city")]
        public string City { get; set; }

        [JsonProperty(PropertyName = "postalcode")]
        public string PostalCode { get; set; }

        [JsonProperty(PropertyName = "region")]
        public string Region { get; set; }

        [JsonProperty(PropertyName = "myPartitionKey")]
        public string MyPartitionKey { get; set; }

        [JsonProperty(PropertyName = "userDefinedId")]
        public int UserDefinedId { get; set; }
    }

    class ResultData
    {
        public string Test;
        public string AvgLatency;
        public string AvgRU;
    }
}
