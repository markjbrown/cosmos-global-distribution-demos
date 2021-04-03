using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace CosmosGlobalDistDemosCore
{
    class Program
    {

        static SingleMultiRegion singleMultiRegion;
        static SingleMultiMaster singleMultiMaster;
        static ConsistencyLatency consistencyLatency;
        static ConflictResolution conflictResolution;
        static MultiMasterFailover multiMasterFailover;

        public static async Task Main(string[] args)
        {
            /*
            * Resources needed for this demo:
            * 
            * Demo is designed to run from a VM in West US 2
            * 
            * Windows VM, West US 2, Standard B4 (4 core, 16GB), RDP enabled. This solution gets run from the VM.
            * 
            * Each class has instructions on Cosmos accounts to provision
            * Fill endpoints and keys from all provisioned Cosmos DB accounts in app.config, do not modify anything else.
            * 
            * Run the Initialize before executing any of the demos to provision database and container resources
            * Run clean up to remove databaser and containers. Use portal to delete accounts.
           */

            
            IConfiguration configuration = new ConfigurationBuilder()
                .AddJsonFile("appSettings.json")
                .Build();

            singleMultiRegion = new SingleMultiRegion(configuration);
            singleMultiMaster = new SingleMultiMaster(configuration);
            consistencyLatency = new ConsistencyLatency(configuration);
            conflictResolution = new ConflictResolution(configuration);
            multiMasterFailover = new MultiMasterFailover(configuration);

            //await benchmarks.RunBenchmark();
            await RunApp();
        }

        public static async Task RunApp()
        {
            bool exit = false;


            while (exit == false)
            {
                Console.Clear();
                Console.WriteLine($"Cosmos DB Global Distribution and Multi-Master Benchmarks");
                Console.WriteLine($"-----------------------------------------------------------");
                Console.WriteLine($"[1]   Single-Region vs. Multi-Region Read Latency");
                Console.WriteLine($"[2]   Read/Write Latency for Single-Master vs. Multi-Master");
                Console.WriteLine($"[3]   Consistency vs. Latency");
                Console.WriteLine($"[4]   Multi-Master Conflict Resolution");
                Console.WriteLine($"[5]   Multi-Master Failover");
                Console.WriteLine($"[6]   Initialize");
                Console.WriteLine($"[7]   Clean up");
                Console.WriteLine($"[8]   Exit");

                ConsoleKeyInfo result = Console.ReadKey(true);

                if (result.KeyChar == '1')
                {
                    Console.Clear();
                    await singleMultiRegion.RunBenchmarks();
                }
                else if (result.KeyChar == '2')
                {
                    Console.Clear();
                    await singleMultiMaster.RunBenchmarks();
                }
                else if (result.KeyChar == '3')
                {
                    Console.Clear();
                    await consistencyLatency.RunBenchmarks();
                }
                else if (result.KeyChar == '4')
                {
                    Console.Clear();
                    await conflictResolution.RunBenchmarks();
                }
                else if (result.KeyChar == '5')
                {
                    Console.Clear();
                    await multiMasterFailover.RunBenchmarks();
                }
                else if (result.KeyChar == '6')
                {
                    Console.WriteLine("Running Set up Routines");
                    await singleMultiRegion.Initialize();
                    await singleMultiMaster.Initialize();
                    await consistencyLatency.Initialize();
                    await conflictResolution.Initialize();
                    await multiMasterFailover.Initialize();
                }
                else if (result.KeyChar == '7')
                {
                    Console.WriteLine("Running Clean up Routines");
                    await singleMultiRegion.CleanUp();
                    await singleMultiMaster.CleanUp();
                    await consistencyLatency.CleanUp();
                    await conflictResolution.CleanUp();
                    await multiMasterFailover.CleanUp();
                }
                else if (result.KeyChar == '8')
                {
                    exit = true;
                }
            }
        }
    }   
}
