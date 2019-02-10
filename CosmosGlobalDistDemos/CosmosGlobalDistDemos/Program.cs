using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CosmosGlobalDistDemos
{
    class Program
    {
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

            Demo demo = new Demo();
            await demo.RunDemo();
        }
    }
    class Demo
    {
        SingleMultiRegion singleMultiRegion = new SingleMultiRegion();
        ConsistencyLatency consistencyLatency = new ConsistencyLatency();
        SingleMultiMaster singleMultiMaster = new SingleMultiMaster();
        Conflicts conflicts = new Conflicts();
        CustomSynchronization customSync = new CustomSynchronization();

        public async Task RunDemo()
        {
            bool exit = false;

            
            while (exit == false)
            {
                Console.Clear();
                Console.WriteLine($"Cosmos DB Global Distribution and Multi-Master Tests");
                Console.WriteLine($"--------------------------------------------------");
                Console.WriteLine($"[1]   Single-Region vs. Multi-Region Read Latency");
                Console.WriteLine($"[2]   Consistency vs. Latency");
                Console.WriteLine($"[3]   Latency for Single-Master vs. Multi-Master");
                Console.WriteLine($"[4]   Multi-Master Conflict Resolution");
                Console.WriteLine($"[5]   Custom Synchronization");
                Console.WriteLine($"[6]   Initialize");
                Console.WriteLine($"[7]   Clean up");
                Console.WriteLine($"[8]   Exit");

                ConsoleKeyInfo result = Console.ReadKey(true);

                if (result.KeyChar == '1')
                {
                    Console.Clear();
                    await singleMultiRegion.RunDemo();
                }
                else if (result.KeyChar == '2')
                {
                    Console.Clear();
                    await consistencyLatency.RunDemo();

                }
                else if (result.KeyChar == '3')
                {
                    Console.Clear();
                    await singleMultiMaster.RunDemo();
                }
                else if (result.KeyChar == '4')
                {
                    Console.Clear();
                    await conflicts.RunDemo();
                }
                else if (result.KeyChar == '5')
                {
                    Console.Clear();
                    await customSync.RunDemo();
                }
                else if (result.KeyChar == '6')
                {
                    Console.WriteLine("Running Set up Routines");
                    await singleMultiRegion.Initalize();
                    await consistencyLatency.Initalize();
                    await singleMultiMaster.Initalize();
                    await conflicts.Initalize();
                    await customSync.Initalize();

                    Console.WriteLine("Preloading data");
                    await singleMultiRegion.LoadData();
                    await singleMultiMaster.LoadData();
                }
                else if (result.KeyChar == '7')
                {
                    Console.WriteLine("Running Clean up Routines");
                    await singleMultiRegion.CleanUp();
                    await consistencyLatency.CleanUp();
                    await singleMultiMaster.CleanUp();
                    await conflicts.CleanUp();
                    await customSync.CleanUp();
                }
                else if (result.KeyChar == '8')
                {
                    exit = true;
                }
            }
        }
    }
}
