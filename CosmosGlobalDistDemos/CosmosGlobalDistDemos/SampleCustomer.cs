using Newtonsoft.Json;
using Microsoft.Azure.Documents;
using System;

namespace CosmosGlobalDistDemos
{
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

}
