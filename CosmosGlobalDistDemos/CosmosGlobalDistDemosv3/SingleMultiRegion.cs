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
    * Shared for all demos in this solution
    * - Windows VM, West US 2, Standard B4 (4 core, 16GB), RDP enabled. This solution gets run from the VM.
    * 
    *   Single Region => Cosmos DB account: Replication: Single-Master, Write Region: East US 2, Consistency: Eventual
    *   Multi-Region => Cosmos DB account: Replication: Single-Master, Write Region: East US 2, Read Region: West US 2, Consistency: Eventual
    *   
*/
    class SingleMultiRegion
    {
        //Benchmarks to run
        private static List<Benchmark> benchmarks = null;

        public SingleMultiRegion(IConfiguration configuration)
        {
            try
            { 
                //Console.WriteLine($"Single Region vs Multi-Region Read Latency\n{Helpers.Line}");
                Console.WriteLine($"Starting Single Region vs Multi-Region Read Latency");

                //Define new Benchmarks
                benchmarks = new List<Benchmark>
                {
                    new Benchmark
                    {
                        benchmarkType = BenchmarkType.Read,
                        testName = "Single Region Reads",
                        testDescription = $"Test 100 Reads against Single-Master account with read region in East US 2 from West US 2",
                        testRegion = configuration["testRegion"],
                        targetRegion = configuration["singleRegionReadRegion"],
                        endpoint = configuration["singleRegionEndpoint"],
                        key = configuration["singleRegionKey"],
                        writeRegion = configuration["singleRegionWriteRegion"],
                        readRegion = configuration["singleRegionReadRegion"],
                        databaseId = configuration["databaseId"],
                        containerId = configuration["containerId"],
                        partitionKeyPath = configuration["partitionKeyPath"],
                        partitionKeyValue = configuration["partitionKeyValue"]
                    },

                    new Benchmark
                    {
                        benchmarkType = BenchmarkType.Read,
                        testName = "Multi Region Reads",
                        testDescription = $"Test 100 Reads against Single-Master account with read region in West US 2 from West US 2",
                        testRegion = configuration["testRegion"],
                        targetRegion = configuration["MultiRegionReadRegion"],
                        endpoint = configuration["multiRegionEndpoint"],
                        key = configuration["multiRegionKey"],
                        writeRegion = configuration["MultiRegionWriteRegion"],
                        readRegion = configuration["MultiRegionReadRegion"],
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
            catch(Exception e)
            {
                Console.WriteLine(e.Message + "\nPress any key to continue");
                Console.ReadKey();
            }
        }
        public async Task Initialize()
        {
            try
            {
                Console.WriteLine("Single/Multi Region Initialize");

                foreach (Benchmark benchmark in benchmarks)
                {
                    await Benchmark.InitializeBenchmark(benchmark);
                }
            }
            catch (Exception e) {
                Console.WriteLine(e.Message + "\nPress any key to continue");
                Console.ReadKey();
            }
        }
        public async Task RunBenchmarks()
        {
            try
            {
                Console.WriteLine($"Test Read Latency between a Single Region Account vs Multi-Region Account\n{Helpers.Line}\nPlease wait...");
                
                //Run benchmarks, collect results
                foreach (Benchmark benchmark in benchmarks)
                {
                    await Benchmark.ReadBenchmark(benchmark);
                }


                //Summarize the results
                Console.WriteLine($"\nOverall Summary\n{Helpers.Line}");

                foreach (Benchmark benchmark in benchmarks)
                {
                    ResultSummary r = benchmark.resultSummary;
                    Console.WriteLine("Test: {0,-20} Average Latency: {1,-4} Average RU: {2,-4}", r.testName, r.averageLatency, r.averageRu );
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
