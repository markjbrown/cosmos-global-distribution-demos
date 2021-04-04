using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace CosmosGlobalDistDemosCore
{
    /*
     * Resources needed for this demo:
     * 
     *   Multi-Master Failover => Cosmos DB account: Replication: Multi-Master, Read/Write Region: East US 2, West US 2, North Europe, Consistency: Eventual
     *   
    */
    class MultiMasterFailover
    {
        //Benchmarks to run
        private static List<Benchmark> benchmarks = null;

        public MultiMasterFailover(IConfiguration configuration)
        {
            try
            {
                Console.WriteLine($"Starting Multi-Master Failover");

                //Define new Benchmarks
                benchmarks = new List<Benchmark>
                {
                    new Benchmark
                    {
                        benchmarkType = BenchmarkType.Read,
                        testName = "Multi Master Failover",
                        testDescription = $"Test Reads against Multi-Master account with failover from West US 2 to East US 2",
                        testRegion = configuration["testRegion"],
                        targetRegion = configuration["multiMasterReadRegion"],
                        endpoint = configuration["MultiMasterEndpoint"],
                        key = configuration["MultiMasterKey"],
                        writeRegion = configuration["multiMasterWriteRegion"],
                        readRegion = configuration["multiMasterReadRegion"],
                        databaseId = configuration["databaseId"],
                        containerId = configuration["containerId"],
                        partitionKeyPath = configuration["partitionKeyPath"],
                        partitionKeyValue = configuration["partitionKeyValue"]
                    }
                };

                foreach (Benchmark benchmark in benchmarks)
                {
                    benchmark.client = new CosmosClient(
                        benchmark.endpoint, 
                        benchmark.key, 
                        new CosmosClientOptions { ApplicationRegion = "East US 2" });
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message + "\nPress any key to continue");
                Console.ReadKey();
            }
        }

        public async Task Initialize()
        {
            try
            {
                Console.WriteLine("Multi Master Failover Initialize");

                foreach (Benchmark benchmark in benchmarks)
                {
                    await Benchmark.InitializeBenchmark(benchmark);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message + "\nPress any key to continue");
                Console.ReadKey();
            }
        }
        public async Task RunBenchmarks()
        {
            try
            {
                Console.WriteLine($"Test reads from Multi-Master account during failover\n{Helpers.Line}\nPlease Wait...");

                //Run benchmarks, collect results
                Benchmark benchmark = benchmarks[0];

                //Verify the benchmark is setup
                await Benchmark.InitializeBenchmark(benchmark);

                List<string> ids = await Benchmark.GetIds(benchmark);
                string id = ids[0];

                bool exit = false;
                int seconds = 0;

                string initialRegion = "";
                string failoverRegion;

                ItemResponse<SampleCustomer> response = await benchmark.container.ReadItemAsync<SampleCustomer>(id: id, partitionKey: new PartitionKey(benchmark.partitionKeyValue));
                initialRegion = Helpers.ParseRegionFromDiag(response.Diagnostics.ToString());
                failoverRegion = initialRegion;

                string regionalEndpoint = Helpers.ParseEndpointFromDiag(response.Diagnostics.ToString());
                string ipAddress = Dns.GetHostAddresses(regionalEndpoint)[0].ToString();

                Console.WriteLine($"Initiate Reads from {initialRegion}");

                Console.WriteLine($"Simulating regional failure by creating a Firewall Rule blocking the ip address for {initialRegion}");
                Firewall.AddFirewallRule("Failover Test", ipAddress);

                while (!exit)
                {
                    
                    response = await benchmark.container.ReadItemAsync<SampleCustomer>(id: id, partitionKey: new PartitionKey(benchmark.partitionKeyValue));
                    failoverRegion = Helpers.ParseRegionFromDiag(response.Diagnostics.ToString());
                    Console.WriteLine($"Read Region: {failoverRegion}, IpAddress: {ipAddress}");

                    //We need to do a create operation to force the diagnostic strings to flip in the SDK client
                    await benchmark.client.CreateDatabaseIfNotExistsAsync("newDB");

                    if (failoverRegion != initialRegion)
                    {
                        Console.WriteLine($"Region failover has occurred. Complete in {seconds} seconds\nPress any key to continue\n...");
                        Console.ReadKey(true);
                        Firewall.RemoveFirewallRule("Failover Test");
                        exit = true;
                    }

                    await Task.Delay(1000);
                    seconds++;

                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message + "\nPress any key to continue");
                Console.ReadKey();
            }
        }



        public async Task CleanUp()
        {
            await Benchmark.CleanUp(benchmarks);
        }
    }
}
