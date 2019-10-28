using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CosmosGlobalDistDemosCore
{
    /*
     * Resources needed for this demo:
     * 
     *   Shared with SingleMultiMaster.cs
     *   Multi-Master => Cosmos DB account: Replication: Multi-Master, Write Region: East US 2, West US 2, North Europe, Consistency: Session
     *   
    */
    class ConflictResolution
    {
        private static List<ConflictGenerator> conflicts = null;

        public ConflictResolution()
        {
            try
            {
                Console.WriteLine("Starting Conflict Resolution");

                IConfigurationRoot configuration = new ConfigurationBuilder()
                        .AddJsonFile("appSettings.json")
                        .Build();


                conflicts = new List<ConflictGenerator>
                {
                    new ConflictGenerator
                    {
                        testName = "Generate insert conflicts on container with Last Writer Wins Policy (Max UserDefinedId Wins).",
                        conflictResolutionType = ConflictResolutionType.LastWriterWins,
                        endpoint = configuration["MultiMasterEndpoint"],
                        key = configuration["MultiMasterKey"],
                        databaseId = configuration["databaseId"],
                        containerId = configuration["LwwPolicyContainer"],
                        partitionKeyPath = configuration["partitionKeyPath"],
                        partitionKeyValue = configuration["partitionKeyValue"],
                        replicaRegions = new List<ReplicaRegion>()
                    },

                    new ConflictGenerator
                    {
                        testName = "Generate insert conflicts on container with User Defined Procedure Policy (Min UserDefinedId Wins).",
                        conflictResolutionType = ConflictResolutionType.MergeProcedure,
                        endpoint = configuration["MultiMasterEndpoint"],
                        key = configuration["MultiMasterKey"],
                        databaseId = configuration["databaseId"],
                        containerId = configuration["UdpPolicyContainer"],
                        partitionKeyPath = configuration["partitionKeyPath"],
                        partitionKeyValue = configuration["partitionKeyValue"],
                        replicaRegions = new List<ReplicaRegion>()
                    },

                    new ConflictGenerator
                    {
                        testName = "Generate update conficts on container with no Policy defined, write to Conflicts Feed.",
                        conflictResolutionType = ConflictResolutionType.None,
                        endpoint = configuration["MultiMasterEndpoint"],
                        key = configuration["MultiMasterKey"],
                        databaseId = configuration["databaseId"],
                        containerId = configuration["NoPolicyContainer"],
                        partitionKeyPath = configuration["partitionKeyPath"],
                        partitionKeyValue = configuration["partitionKeyValue"],
                        replicaRegions = new List<ReplicaRegion>()
                    }
                };
                //Load the regions and initialize the clients
                foreach (ConflictGenerator conflict in conflicts)
                {
                    List<string> regions = configuration["ConflictRegions"].Split(new char[] { ';' }).ToList();
                    foreach (string region in regions)
                    {
                        conflict.replicaRegions.Add(new ReplicaRegion
                        {
                            region = region,
                            client = new CosmosClient(conflict.endpoint, conflict.key, new CosmosClientOptions { ApplicationRegion = region })
                        });
                    }
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
                Console.WriteLine("MultiMaster Conflicts Initialize");

                foreach(ConflictGenerator conflict in conflicts)
                {
                    await conflict.InitializeConflicts(conflict);
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
                Console.WriteLine($"Multi Master Conflict Resolution\n{Helpers.Line}\n");

                foreach(ConflictGenerator conflict in conflicts)
                {
                    switch (conflict.conflictResolutionType)
                    {
                        case ConflictResolutionType.LastWriterWins:
                            await conflict.GenerateInsertConflicts(conflict);
                            break;
                        case ConflictResolutionType.MergeProcedure:
                            await conflict.GenerateInsertConflicts(conflict);
                            break;
                        case ConflictResolutionType.None:
                            await conflict.GenerateUpdateConflicts(conflict);
                            break;
                    }
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
            await ConflictGenerator.CleanUp(conflicts);
        }
    }
}
