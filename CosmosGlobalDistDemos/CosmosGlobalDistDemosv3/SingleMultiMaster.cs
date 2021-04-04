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
     *   Single Master => Cosmos DB account: Replication: Single-Master, Write Region: East US 2, Read Region: West US 2, Consistency: Eventual
     *   Multi-Master => Cosmos DB account: Replication: Multi-Master, Read/Write Region: East US 2, West US 2, North Europe, Consistency: Eventual
     *   
    */
    class SingleMultiMaster
    {
        //Benchmarks to run
        private static List<Benchmark> benchmarks = null;

        public SingleMultiMaster(IConfiguration configuration)
        {
            try
            {
                
                Console.WriteLine($"Starting Single-Master vs Multi-Master Read/Write Latency");

                //Define new Benchmarks
                benchmarks = new List<Benchmark>
                {
                    new Benchmark
                    {
                        benchmarkType = BenchmarkType.Read,
                        testName = "Single Master Reads",
                        testDescription = $"Test 100 Reads against Single-Master account with read region in West US 2 from West US 2",
                        testRegion = configuration["testRegion"],
                        targetRegion = configuration["singleMasterReadRegion"],
                        endpoint = configuration["SingleMasterEndpoint"],
                        key = configuration["SingleMasterKey"],
                        writeRegion = configuration["singleMasterWriteRegion"],
                        readRegion = configuration["singleMasterReadRegion"],
                        databaseId = configuration["databaseId"],
                        containerId = configuration["containerId"],
                        partitionKeyPath = configuration["partitionKeyPath"],
                        partitionKeyValue = configuration["partitionKeyValue"]
                    },

                    new Benchmark
                    {
                        benchmarkType = BenchmarkType.Write,
                        testName = "Single Master Writes",
                        testDescription = $"Test 100 Writes against Single-Master account with write region in East US 2 from West US 2",
                        testRegion = configuration["testRegion"],
                        targetRegion = configuration["singleMasterWriteRegion"],
                        endpoint = configuration["SingleMasterEndpoint"],
                        key = configuration["SingleMasterKey"],
                        writeRegion = configuration["singleMasterWriteRegion"],
                        readRegion = configuration["singleMasterReadRegion"],
                        databaseId = configuration["databaseId"],
                        containerId = configuration["containerId"],
                        partitionKeyPath = configuration["partitionKeyPath"],
                        partitionKeyValue = configuration["partitionKeyValue"]
                    },

                    new Benchmark
                    {
                        benchmarkType = BenchmarkType.Read,
                        testName = "Multi Master Reads",
                        testDescription = $"Test 100 Reads against Multi-Master account with read region in West US 2 from West US 2",
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
                    },

                    new Benchmark
                    {
                        benchmarkType = BenchmarkType.Write,
                        testName = "Multi Master Writes",
                        testRegion = configuration["testRegion"],
                        testDescription = $"Test 100 Writes against Multi-Master account with write region in West US 2 from West US 2",
                        targetRegion = configuration["multiMasterWriteRegion"],
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
                Console.WriteLine($"Test read and write latency between a Single-Master and Multi-Master account\n{Helpers.Line}\nPlease Wait...");

                //Run benchmarks, collect results
                foreach (Benchmark benchmark in benchmarks)
                {
                    if(benchmark.benchmarkType == BenchmarkType.Read)
                        await Benchmark.ReadBenchmark(benchmark);
                    else
                        await Benchmark.WriteBenchmark(benchmark);
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
