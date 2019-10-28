using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CosmosGlobalDistDemosCore
{
    /*
        * Resources needed for this demo:
        * 
        *   Custom => Cosmos DB account: Replication: Single-Master, Write Region: West US 2, East US 2, West US, East US, Consistency: Session
        *   Strong => Cosmos DB account: Replication: Single-Master, Write Region: West US 2, East US 2, West US, East US, Consistency: Strong
        *   
    */
    class CustomSynchronization
    {
        //Benchmarks to run
        private static List<Benchmark> benchmarks = null;

        public CustomSynchronization()
        {
            try
            {
                //Console.WriteLine($"Single-Master vs Multi-Master Read/Write Latency\n{Helpers.Line}");
                Console.WriteLine($"Starting Custom Synchronization");

                IConfigurationRoot configuration = new ConfigurationBuilder()
                        .AddJsonFile("appSettings.json")
                        .Build();

                //Define new Benchmarks
                benchmarks = new List<Benchmark>
                {
                    new Benchmark
                    {
                        benchmarkType = BenchmarkType.Write,
                        testName = "Strong Consistency Writes",
                        testDescription = $"Test 100 Writes against account with Strong Consistency in West US 2 replicated to East US 2 and North Europe",
                        testRegion = configuration["testRegion"],
                        targetRegion = configuration["StrongSyncReplicaRegion"],
                        endpoint = configuration["StrongEndpoint"],
                        key = configuration["StrongKey"],
                        writeRegion = configuration["StrongWriteRegion"],
                        readRegion = configuration["StrongReadRegion"],
                        databaseId = configuration["databaseId"],
                        containerId = configuration["containerId"],
                        partitionKeyPath = configuration["partitionKeyPath"],
                        partitionKeyValue = configuration["partitionKeyValue"]
                    },

                    new Benchmark
                    {
                        benchmarkType = BenchmarkType.Custom,
                        testName = "Custom Sync Writes",
                        testDescription = $"Test 100 Writes against account with Session consistency and Custom Syncrhonization between West US 2 and West US",
                        testRegion = configuration["testRegion"],
                        targetRegion = configuration["CustomSyncReplicaRegion"],
                        endpoint = configuration["CustomSyncEndpoint"],
                        key = configuration["CustomSyncKey"],
                        writeRegion = configuration["CustomSyncWriteRegion"],
                        readRegion = configuration["CustomSyncReadRegion"],
                        databaseId = configuration["databaseId"],
                        containerId = configuration["containerId"],
                        partitionKeyPath = configuration["partitionKeyPath"],
                        partitionKeyValue = configuration["partitionKeyValue"]
                    }
                };

                foreach (Benchmark benchmark in benchmarks)
                {
                    benchmark.client = new CosmosClient(benchmark.endpoint, benchmark.key, new CosmosClientOptions { ApplicationRegion = benchmark.writeRegion });
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
                Console.WriteLine("Single/Multi Master Initialize");

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
                Console.WriteLine($"Test Latency between Strong Consistency in all regions vs. Custom Synchronization in two regions\n{Helpers.Line}\n");

                //Run benchmarks, collect results
                foreach (Benchmark benchmark in benchmarks)
                {
                    if (benchmark.benchmarkType == BenchmarkType.Write)
                        await Benchmark.WriteBenchmark(benchmark);
                    else if (benchmark.benchmarkType == BenchmarkType.Custom)
                        await Benchmark.CustomSyncBenchmark(benchmark);
                }

                //Summarize the results
                Console.WriteLine($"\nOverall Summary\n{Helpers.Line}\n");

                foreach (Benchmark benchmark in benchmarks)
                {
                    ResultSummary r = benchmark.resultSummary;
                    Console.WriteLine("Test: {0,-30} Average Latency: {1,-4} Average RU: {2,-4}", r.testName, r.averageLatency, r.averageRu);
                }

                Console.WriteLine($"\nTest concluded. Press any key to continue\n...");
                Console.ReadKey(true);

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
