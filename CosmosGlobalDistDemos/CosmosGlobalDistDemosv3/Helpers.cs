using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using NetFwTypeLib;
using System.Management.Automation;


namespace CosmosGlobalDistDemosCore
{
    static class Helpers
    {
        public static string Line = new string('-', 73);

        public static async Task<bool> VerifyContainerReplicated (Container container)
        {
            bool isReplicated = false;
            bool notifyOnce = false;

            while (!isReplicated)
            {
                try
                {
                    await container.ReadContainerAsync();
                    isReplicated = true; //hit this line then container has replicated to other regions.
                    if(notifyOnce)
                        Console.WriteLine("Resource is replicated and available");
                }
                catch
                {
                    if (!notifyOnce)
                    {
                        Console.WriteLine("Waiting for container to replicate in all regions");
                        notifyOnce = true;
                    }
                    //swallow any errors and wait 250ms to retry
                    await Task.Delay(250);
                }
            }
            return isReplicated;
        }

        public static string ParseRegionFromDiag(string json)
        {
            int start = json.IndexOf("//") + "//".Length;
            int end = json.IndexOf(".documents.");
            string accountName = json.Substring(start, end - start);
            int start2 = accountName.LastIndexOf("-") + "-".Length;
            string region = accountName.Substring(start2, accountName.Length - start2);

            return region;
        }

        public static string ParseEndpointFromDiag(string json)
        {
            string suffix = ".documents.azure.com";
            int start = json.IndexOf("//") + "//".Length;
            int end = json.IndexOf(suffix) + suffix.Length;
            string endpoint = json.Substring(start, end - start);

            return endpoint;
        }
    }

    public class SampleCustomer
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

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

        public static List<SampleCustomer> GenerateCustomers(string partitionKeyValue, int number)
        {
            //Generate fake customer data.
            Bogus.Faker<SampleCustomer> customerGenerator = new Bogus.Faker<SampleCustomer>().Rules((faker, customer) =>
            {
                customer.Id = Guid.NewGuid().ToString();
                customer.Name = faker.Name.FullName();
                customer.City = faker.Person.Address.City.ToString();
                customer.Region = faker.Person.Address.State.ToString();
                customer.PostalCode = faker.Person.Address.ZipCode.ToString();
                customer.MyPartitionKey = partitionKeyValue;
                customer.UserDefinedId = faker.Random.Int(0, 1000);
            });

            return customerGenerator.Generate(number);
        }
    }

    public class Firewall
    {

        public static void AddFirewallRule(string ruleName, string ipAddress)
        {
            try
            {
                INetFwRule firewallRule = (INetFwRule)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwRule"));
                firewallRule.Action = NET_FW_ACTION_.NET_FW_ACTION_BLOCK;
                firewallRule.Description = "Block Multi-master";
                firewallRule.Direction = NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_OUT;
                firewallRule.Enabled = true;
                firewallRule.InterfaceTypes = "All";
                firewallRule.Name = ruleName;
                //firewallRule.Protocol = 6;// NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP;
                firewallRule.RemoteAddresses = ipAddress;

                INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(
                    Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
                firewallPolicy.Rules.Add(firewallRule);

            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
            }
        }

        public static void AddFirewallRulePS(string ruleName, string ipAddress)
        {
            
            PowerShell ps = PowerShell.Create();

            string command = $"New-NetFirewallRule -DisplayName {ruleName} -Action Block -Direction Outbound -Enabled True -InterfaceType Any -RemoteAddress '{ipAddress}'";

            ps.AddScript(command).Invoke();
        }

        public static void RemoveFirewallRule(string ruleName)
        {
            try
            { 
                INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(
                        Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
                firewallPolicy.Rules.Remove(ruleName);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
            }
        }

        public static void RemoveFirewallRulePS(string ruleName)
        {
            PowerShell ps = PowerShell.Create();

            string command = $"Remove-NetFirewallRule -DisplayName MultiMasterFailover {ruleName}";

            ps.AddScript(command).Invoke();
        }

    }

}
