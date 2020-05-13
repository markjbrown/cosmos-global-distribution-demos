using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net;
using System.IO;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Scripts;
using System.Linq;

namespace CosmosGlobalDistDemosCore
{
    public enum ConflictResolutionType
    {
        LastWriterWins = 0,
        MergeProcedure = 1,
        None = 2
    }

    class ReplicaRegion
    {
        public string region;
        public CosmosClient client;
        public Container container;
    }

    class ConflictGenerator
    {
        public string testName;
        public ConflictResolutionType conflictResolutionType;
        public string endpoint;
        public string key;
        public string databaseId;
        public string containerId;
        public List<ReplicaRegion> replicaRegions;
        public string partitionKeyPath;
        public string partitionKeyValue;

        public async Task InitializeConflicts(ConflictGenerator conflict)
        {
            //Use West US 2 region to test if containers have been created and create them if needed.
            ReplicaRegion replicaRegion = conflict.replicaRegions.Find(s => s.region == "West US 2");

            
            if (replicaRegion.container == null)
            { //Create the containers
                try
                {
                    replicaRegion.container = replicaRegion.client.GetContainer(conflict.databaseId, conflict.containerId);
                    await replicaRegion.container.ReadContainerAsync(); //ReadContainer to see if it is created
                }
                catch
                {
                    DatabaseResponse dbResponse = await replicaRegion.client.CreateDatabaseIfNotExistsAsync(conflict.databaseId);
                    Database database = dbResponse.Database;
                    ContainerResponse cResponse;

                    //Create containers with different conflict resolution policies
                    switch(conflict.conflictResolutionType)
                    {
                        case ConflictResolutionType.LastWriterWins:
                            cResponse = await database.CreateContainerIfNotExistsAsync(new ContainerProperties(conflict.containerId, conflict.partitionKeyPath)
                            {
                                ConflictResolutionPolicy = new ConflictResolutionPolicy()
                                {
                                    Mode = ConflictResolutionMode.LastWriterWins,
                                    ResolutionPath = "/userDefinedId"
                                }
                            }, 400);
                            break;
                        case ConflictResolutionType.MergeProcedure:
                            string scriptId = "MergeProcedure";
                            cResponse = await database.CreateContainerIfNotExistsAsync(new ContainerProperties(conflict.containerId, conflict.partitionKeyPath)
                            {
                                ConflictResolutionPolicy = new ConflictResolutionPolicy()
                                {
                                    Mode = ConflictResolutionMode.Custom,
                                    ResolutionProcedure = $"dbs/{conflict.databaseId}/colls/{conflict.containerId}/sprocs/{scriptId}"
                                }
                            }, 400);

                            //Conflict Merge Procedure
                            string body = File.ReadAllText(@"spConflictUDP.js");
                            StoredProcedureResponse sproc = await cResponse.Container.Scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(scriptId, body));
                            break;

                        case ConflictResolutionType.None:
                            cResponse = await database.CreateContainerIfNotExistsAsync(new ContainerProperties(conflict.containerId, conflict.partitionKeyPath)
                            {
                                ConflictResolutionPolicy = new ConflictResolutionPolicy()
                                {
                                    Mode = ConflictResolutionMode.Custom
                                }
                            }, 400);
                            break;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    replicaRegion.container = cResponse.Container;
                }
            }

            //Initialize and warm up all regional container references
            foreach(ReplicaRegion region in conflict.replicaRegions)
            {
                region.container = region.client.GetContainer(conflict.databaseId, conflict.containerId);
                //Verify container has replicated
                await Helpers.VerifyContainerReplicated(region.container);
                await WarmUp(region.container);
            }
        }
        public async Task GenerateInsertConflicts(ConflictGenerator conflict)
        {
            try
            {
                bool isConflicts = false;

                Console.WriteLine($"{conflict.testName}\nPress any key to continue...\n");
                Console.ReadKey(true);

                //Verify the containers are created and referenced
                await InitializeConflicts(conflict);

                while (!isConflicts)
                {
                    //Generate a sample customer
                    SampleCustomer customer = SampleCustomer.GenerateCustomers(conflict.partitionKeyValue, 1)[0];

                    //Task list for async inserts
                    List<Task<SampleCustomer>> tasks = new List<Task<SampleCustomer>>();

                    //Insert same customer into every region
                    foreach (ReplicaRegion replicaRegion in conflict.replicaRegions)
                    {
                        tasks.Add(InsertItemAsync(replicaRegion.container, replicaRegion.region, customer));
                    }
                    //await tasks
                    SampleCustomer[] insertedItems = await Task.WhenAll(tasks);

                    //Verify conflicts. If two or more items returned then conflicts occurred
                    isConflicts = IsConflict(insertedItems);
                }
            }
            catch (CosmosException e)
            {
                Console.WriteLine(e.Message + "\nPress any key to continue");
                Console.ReadKey();
            }
        }
        private async Task<SampleCustomer> InsertItemAsync(Container container, string region, SampleCustomer item)
        {
            //DeepCopy the item
            item = ConflictGenerator.Clone(item);

            //Update the region
            item.Region = region;
            //Update UserDefinedId to random number for Conflict Resolution
            item.UserDefinedId = ConflictGenerator.RandomNext(0, 1000);

            Console.WriteLine($"Attempting insert - Name: {item.Name}, City: {item.City}, UserDefId: {item.UserDefinedId}, Region: {item.Region}");

            try
            {
                ItemResponse<SampleCustomer> response = await container.CreateItemAsync<SampleCustomer>(item, new PartitionKey(item.MyPartitionKey));

                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                    return null;  //item has already replicated so not possible to generate conflict
                else
                    return response.Resource;
            }
            catch (CosmosException e)
            {
                //Conflict error
                if (e.StatusCode == HttpStatusCode.Conflict)
                    return null;
                else
                    throw e;
            }
        }
        public async Task GenerateUpdateConflicts(ConflictGenerator conflict)
        {
            try
            {
                //Verify the containers are created and referenced
                await InitializeConflicts(conflict);

                bool isConflicts = false;

                Console.WriteLine($"\n{conflict.testName}\nPress any key to continue...");
                Console.ReadKey(true);
                Console.WriteLine($"Insert an item to create an update conflict on.\n");

                //Get reference to West US 2 region to insert a customer
                ReplicaRegion insertRegion = conflict.replicaRegions.Find(s => s.region == "West US 2");

                //Container to insert customer
                Container insertContainer = insertRegion.container;

                //Generate a new customer
                SampleCustomer seedCustomer = SampleCustomer.GenerateCustomers(conflict.partitionKeyValue, 1)[0];

                //Insert Customer
                seedCustomer = await InsertItemAsync(insertContainer, insertRegion.region, seedCustomer);

                //Wait for item to replicate globally
                Console.WriteLine($"\nAllow item to replicate.\n");
                //await Task.Delay(5000);
                await VerifyItemReplicated(conflict, seedCustomer);

                //Task list for async updates
                IList<Task<SampleCustomer>> tasks = new List<Task<SampleCustomer>>();

                //Read back the replicated customer and get the ETag
                ItemResponse<SampleCustomer> customerResponse = await insertContainer.ReadItemAsync<SampleCustomer>(seedCustomer.Id, new PartitionKey(seedCustomer.MyPartitionKey));

                Console.WriteLine($"Attempting simultaneous update in {replicaRegions.Count} regions\n");

                while (!isConflicts)
                {
                    foreach (ReplicaRegion updateRegion in conflict.replicaRegions)
                    {
                        //DeepCopy the item
                        SampleCustomer updateCustomer = ConflictGenerator.Clone(seedCustomer);
                        //Update region to where update is made and provide new UserDefinedId value
                        updateCustomer.Region = updateRegion.region;
                        updateCustomer.UserDefinedId = ConflictGenerator.RandomNext(0, 1000);

                        //Add update to Task List
                        tasks.Add(UpdateItemAsync(updateRegion.container, updateCustomer));
                    }

                    SampleCustomer[] updateCustomers = await Task.WhenAll(tasks);

                    isConflicts = IsConflict(updateCustomers);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message + "\nPress any key to continue");
                Console.ReadKey();
            }
        }
        private async Task<SampleCustomer> UpdateItemAsync(Container container, SampleCustomer customer)
        {
            Console.WriteLine($"Update - Name: {customer.Name}, City: {customer.City}, UserDefId: {customer.UserDefinedId}, Region: {customer.Region}");

            try
            {
                ItemResponse<SampleCustomer> response = await container.ReplaceItemAsync<SampleCustomer>(
                    customer,
                    customer.Id,
                    new PartitionKey(customer.MyPartitionKey));

                return response.Resource;
            }
            catch
            {
                //Unable to create conflict
                return null;
            }
        }
        private async Task WarmUp(Container container)
        {
            try
            {   //Warm up container with point read for item that doesn't exist
                await container.ReadItemAsync<dynamic>("0", new PartitionKey("0"));
            }
            catch { }
        }
        private async Task VerifyItemReplicated(ConflictGenerator conflict, SampleCustomer customer)
        {
            bool notifyOnce = false;

            foreach (ReplicaRegion region in conflict.replicaRegions)
            {
                Container container = region.container;
                bool isReplicated = false;
                while(!isReplicated)
                { 
                    try
                    {   //Issue a read for an item that doesn't exist to warm the connection
                        await container.ReadItemAsync<SampleCustomer>(customer.Id, new PartitionKey(customer.MyPartitionKey));
                        isReplicated = true;
                        if (notifyOnce)
                            Console.WriteLine("Item is replicated");
                    }
                    catch
                    {
                        if (!notifyOnce)
                        {
                            Console.WriteLine("Waiting for item to replicate in all regions");
                            notifyOnce = true;
                        }
                        //swallow any errors and wait 250ms to retry
                        await Task.Delay(250);
                    }
                }
            }

        }
        private bool IsConflict(SampleCustomer[] items)
        {
            int operations = items.Count(s => s != null);

            if (operations > 1)
            {
                Console.WriteLine($"\nConflicts generated. Confirm in Portal. Press any key to continue...\n");
                Console.ReadKey(true);
                return true;
            }
            else
            {
                Console.WriteLine($"\nNo conflicts generated. Retrying to induce conflicts\n");
            }
            return false;
        }
        public async Task ProcessConflicts(ConflictGenerator conflictGenerator)
        {
            Console.WriteLine($"\nReading conflicts feed to process any conflicts.\n{Helpers.Line}\nPress any key to continue...\n");

            //Use West US 2 region to review conflicts
            ReplicaRegion replicaRegion = conflictGenerator.replicaRegions.Find(s => s.region == "West US 2");
            Container container = replicaRegion.client.GetContainer(conflictGenerator.databaseId, conflictGenerator.containerId);

            FeedIterator<ConflictProperties> conflictFeed = container.Conflicts.GetConflictQueryIterator<ConflictProperties>();

            while (conflictFeed.HasMoreResults)
            {
                FeedResponse<ConflictProperties> conflictFeedResponse = await conflictFeed.ReadNextAsync();

                Console.WriteLine($"There are {conflictFeedResponse.Count} conflict(s) to process.\nPress any key to continue\n");
                Console.ReadKey(true);


                foreach (ConflictProperties conflictItem in conflictFeedResponse)
                {
                    //Read the conflict and committed item
                    SampleCustomer conflict = container.Conflicts.ReadConflictContent<SampleCustomer>(conflictItem);
                    SampleCustomer committed = await container.Conflicts.ReadCurrentAsync<SampleCustomer>(conflictItem, new PartitionKey(conflict.MyPartitionKey));

                    Console.WriteLine($"Processing conflict on customer: {committed.Name}, in {committed.Region} region with conflict in {conflict.Region} region.\n{Helpers.Line}\n");
                    Console.WriteLine($"Conflict UserDefined Id = {conflict.UserDefinedId}. Committed UserDefined Id = {committed.UserDefinedId}");

                    switch (conflictItem.OperationKind)
                    {
                        case OperationKind.Create:
                            //For Inserts make the higher UserDefinedId value the winner
                            Console.WriteLine($"Processing insert conflict.\nReplace committed item if conflict has >= UserDefinedId.");
                            
                            if (conflict.UserDefinedId >= committed.UserDefinedId)
                            {
                                Console.WriteLine($"Conflict is the winner. Press any key to replace committed item with conflict.\n");
                                Console.ReadKey(true);
                                await container.ReplaceItemAsync<SampleCustomer>(conflict, conflict.Id, new PartitionKey(conflict.MyPartitionKey));
                            }
                            else
                            {
                                Console.WriteLine($"Committed item is the winner. Press any key to continue.\n");
                                Console.ReadKey(true);
                            }
                            break;
                        case OperationKind.Replace:
                            //For Updates make the lower UserDefinedId value the winner
                            Console.WriteLine($"Processing update conflict.\nUpdate committed item if conflict has a <= UserDefinedId.");
                            
                            if (conflict.UserDefinedId <= committed.UserDefinedId)
                            {
                                Console.WriteLine($"Conflict is the winner. Press any key to replace committed item with conflict.\n");
                                Console.ReadKey(true);
                                await container.ReplaceItemAsync<SampleCustomer>(conflict, conflict.Id, new PartitionKey(conflict.MyPartitionKey));
                            }
                            else
                            {
                                Console.WriteLine($"Committed item is the winner. Press any key to continue.\n");
                                Console.ReadKey(true);
                            }
                            break;
                        case OperationKind.Delete:
                            //Generally don't resolve deleted items so do nothing
                            break;
                    }
                    // Delete the conflict
                    await container.Conflicts.DeleteAsync(conflictItem, new PartitionKey(conflict.MyPartitionKey));
                }
            }
        }
        private static SampleCustomer Clone(SampleCustomer source)
        {   //Deep copy Document object
            return JsonConvert.DeserializeObject<SampleCustomer>(JsonConvert.SerializeObject(source));
        }
        //Thread-safe random number generator
        private static Random _global = new Random();
        [ThreadStatic]
        private static Random _local;
        public static int RandomNext()
        {
            Random inst = _local;
            if (inst == null)
            {
                int seed;
                lock (_global) seed = _global.Next();
                _local = inst = new Random(seed);
            }
            return inst.Next();
        }
        public static int RandomNext(int minValue, int maxValue)
        {
            Random inst = _local;
            if (inst == null)
            {
                int seed;
                lock (_global) seed = _global.Next(minValue, maxValue);
                _local = inst = new Random(seed);
            }
            return inst.Next(minValue, maxValue);
        }
        public static async Task CleanUp(List<ConflictGenerator> conflicts)
        {
            try
            {
                foreach (ConflictGenerator conflict in conflicts)
                {
                    //Get reference to West US 2 region
                    ReplicaRegion region = conflict.replicaRegions.Find(replicaRegion => replicaRegion.region == "West US 2");
                    await region.client.GetDatabase(conflict.databaseId).DeleteAsync();
                }
            }
            catch { }
        }

        public static async Task CleanUp(ConflictGenerator conflict)
        {
            try
            {
                //Get reference to West US 2 region
                ReplicaRegion region = conflict.replicaRegions.Find(replicaRegion => replicaRegion.region == "West US 2");
                await region.client.GetDatabase(conflict.databaseId).DeleteAsync();   
            }
            catch { }
        }
    }
}
