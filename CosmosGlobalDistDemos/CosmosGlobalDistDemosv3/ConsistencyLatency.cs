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
        * 
        *   Eventual => Cosmos DB account: Replication: Single-Master, Write Region: West US 2, Read Region: Central US, Consistency: Eventual
        *   Strong 1K Miles => Cosmos DB account: Replication: Single-Master, Write Region: West US 2, Read Region: Central US, Consistency: Strong
        *   Strong 2K Miles => Cosmos DB account: Replication: Single-Master, Write Region: West US 2, Read Region: East US 2, Consistency: Strong
        *   
    */
    class ConsistencyLatency
    {
        //Benchmarks to run
        private static List<Benchmark> benchmarks = null;

        public ConsistencyLatency(IConfiguration configuration)
        {
            try
            {
                Console.WriteLine("Starting Latency vs Eventual and Strong Consistency");

                //Define new Benchmarks
                benchmarks = new List<Benchmark>
                {
                    new Benchmark
                    {
                        benchmarkType = BenchmarkType.Write,
                        testName = "Eventual 1000 miles",
                        testDescription = $"Test 100 Writes against account with Eventual Consistency in West US 2 replicated to Central US",
                        testRegion = configuration["testRegion"],
                        targetRegion = configuration["EventualReplicaRegion"],
                        endpoint = configuration["EventualEndpoint"],
                        key = configuration["EventualKey"],
                        writeRegion = configuration["EventualWriteRegion"],
                        readRegion = configuration["EventualReadRegion"],
                        databaseId = configuration["databaseId"],
                        containerId = configuration["containerId"],
                        partitionKeyPath = configuration["partitionKeyPath"],
                        partitionKeyValue = configuration["partitionKeyValue"]
                    },

                    new Benchmark
                    {
                        benchmarkType = BenchmarkType.Write,
                        testName = "Strong 1000 miles",
                        testDescription = $"Test 100 Writes against account with Strong Consistency in West US 2 replicated to Central US",
                        testRegion = configuration["testRegion"],
                        targetRegion = configuration["Strong1kMilesReplicaRegion"],
                        endpoint = configuration["Strong1kMilesEndpoint"],
                        key = configuration["Strong1kMilesKey"],
                        writeRegion = configuration["Strong1kMilesWriteRegion"],
                        readRegion = configuration["Strong1kMilesReadRegion"],
                        databaseId = configuration["databaseId"],
                        containerId = configuration["containerId"],
                        partitionKeyPath = configuration["partitionKeyPath"],
                        partitionKeyValue = configuration["partitionKeyValue"]
                    },

                    new Benchmark
                    {
                        benchmarkType = BenchmarkType.Write,
                        testName = "Strong 2000 miles",
                        testDescription = $"Test 100 Writes against account with Strong Consistency in West US 2 replicated to East US 2",
                        testRegion = configuration["testRegion"],
                        targetRegion = configuration["Strong2kMilesReplicaRegion"],
                        endpoint = configuration["Strong2kMilesEndpoint"],
                        key = configuration["Strong2kMilesKey"],
                        writeRegion = configuration["Strong2kMilesWriteRegion"],
                        readRegion = configuration["Strong2kMilesReadRegion"],
                        databaseId = configuration["databaseId"],
                        containerId = configuration["containerId"],
                        partitionKeyPath = configuration["partitionKeyPath"],
                        partitionKeyValue = configuration["partitionKeyValue"]
                    },

                    new Benchmark
                    {
                        benchmarkType = BenchmarkType.Read,
                        testName = "Throughput Eventual",
                        testDescription = $"Test 100 Reads against account with Eventual Consistency to measure RU/s (Throughput) usage",
                        testRegion = configuration["testRegion"],
                        targetRegion = configuration["EventualReadRegion"],
                        endpoint = configuration["EventualEndpoint"],
                        key = configuration["EventualKey"],
                        writeRegion = configuration["EventualWriteRegion"],
                        readRegion = configuration["EventualReadRegion"],
                        databaseId = configuration["databaseId"],
                        containerId = configuration["containerId"],
                        partitionKeyPath = configuration["partitionKeyPath"],
                        partitionKeyValue = configuration["partitionKeyValue"]
                    },

                    new Benchmark
                    {
                        benchmarkType = BenchmarkType.Read,
                        testName = "Throughput Strong",
                        testRegion = configuration["testRegion"],
                        testDescription = $"Test 100 Reads against account with Strong Consistency to measure RU/s (Throughput) usage",
                        targetRegion = configuration["Strong1kMilesReadRegion"],
                        endpoint = configuration["Strong1kMilesEndpoint"],
                        key = configuration["Strong1kMilesKey"],
                        writeRegion = configuration["Strong1kMilesWriteRegion"],
                        readRegion = configuration["Strong1kMilesReadRegion"],
                        databaseId = configuration["databaseId"],
                        containerId = configuration["containerId"],
                        partitionKeyPath = configuration["partitionKeyPath"],
                        partitionKeyValue = configuration["partitionKeyValue"]
                    }
                };

                foreach (Benchmark benchmark in benchmarks)
                {
                    benchmark.client = new CosmosClient(benchmark.endpoint, benchmark.key, new CosmosClientOptions { ApplicationRegion = benchmark.targetRegion });
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
                Console.WriteLine("Consistency/Latency Initialize");

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
                Console.WriteLine($"Test Consistency vs. Latency and Consistency vs Throughput\n{Helpers.Line}\n");

                //Run benchmarks, collect results
                foreach (Benchmark benchmark in benchmarks)
                {
                    if (benchmark.benchmarkType == BenchmarkType.Write)
                        await Benchmark.WriteBenchmark(benchmark);
                    else
                        await Benchmark.ReadBenchmark(benchmark);
                }

                //Summarize the results
                Console.WriteLine($"\nOverall Summary\n{Helpers.Line}");

                foreach (Benchmark benchmark in benchmarks)
                {
                    ResultSummary r = benchmark.resultSummary;
                    Console.WriteLine("Test: {0,-26} Average Latency: {1,-4} Average RU: {2,-4}", r.testName, r.averageLatency, r.averageRu);
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
