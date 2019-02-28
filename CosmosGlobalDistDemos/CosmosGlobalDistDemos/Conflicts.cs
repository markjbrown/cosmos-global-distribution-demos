using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Diagnostics;

namespace CosmosGlobalDistDemos
{

    /*
     * Resources needed for this demo:
     * 
     *   Shared with SingleMultiMaster.cs
     *   Multi-Master => Cosmos DB account: Replication: Multi-Master, Write Region: East US 2, West US 2, North Europe, Consistency: Session
     *   
    */

    class Conflicts
    {
        private List<DocumentClient> clients;
        private string databaseName;
        private Uri databaseUri;
        private string lwwContainerName;
        private Uri lwwContainerUri;
        private string udpContainerName;
        private Uri udpContainerUri;
        private string noneContainerName;
        private Uri noneContainerUri;
        private string PartitionKeyProperty = ConfigurationManager.AppSettings["PartitionKeyProperty"];
        private string PartitionKeyValue = ConfigurationManager.AppSettings["PartitionKeyValue"];

        private Bogus.Faker<SampleCustomer> customerGenerator = new Bogus.Faker<SampleCustomer>().Rules((faker, customer) =>
        {
            customer.Id = Guid.NewGuid().ToString();
            customer.Name = faker.Name.FullName();
            customer.City = faker.Person.Address.City.ToString();
            customer.Region = faker.Person.Address.State.ToString(); //replaced by code below for inserts/updates
            customer.PostalCode = faker.Person.Address.ZipCode.ToString();
            customer.MyPartitionKey = ConfigurationManager.AppSettings["PartitionKeyValue"];
            customer.UserDefinedId = faker.Random.Int(0, 1000);
        });

        public Conflicts()
        {
            databaseName = ConfigurationManager.AppSettings["database"];
            lwwContainerName = ConfigurationManager.AppSettings["LwwPolicyContainer"];
            udpContainerName = ConfigurationManager.AppSettings["UdpPolicyContainer"];
            noneContainerName = ConfigurationManager.AppSettings["NoPolicyContainer"];

            databaseUri = UriFactory.CreateDatabaseUri(databaseName);
            lwwContainerUri = UriFactory.CreateDocumentCollectionUri(databaseName, lwwContainerName);
            udpContainerUri = UriFactory.CreateDocumentCollectionUri(databaseName, udpContainerName);
            noneContainerUri = UriFactory.CreateDocumentCollectionUri(databaseName, noneContainerName);

            Console.WriteLine("Multi Master: Conflict Resolution");
            Console.WriteLine("---------------------------------");

            string endpoint = ConfigurationManager.AppSettings["MultiMasterEndpoint"];
            string key = ConfigurationManager.AppSettings["MultiMasterKey"];
            List<string> regions = ConfigurationManager.AppSettings["ConflictRegions"].Split(';').ToList();

            clients = new List<DocumentClient>();
            foreach (string region in regions)
            {
                ConnectionPolicy policy = new ConnectionPolicy
                {
                    ConnectionMode = ConnectionMode.Direct,
                    ConnectionProtocol = Protocol.Tcp,
                    UseMultipleWriteLocations = true //Multiple write locations
                };
                policy.SetCurrentLocation(region);
                DocumentClient client = new DocumentClient(new Uri(endpoint), key, policy, ConsistencyLevel.Eventual);
                client.OpenAsync();
                clients.Add(client);
                Console.WriteLine($"Created Multi-Master DocumentClient in: {region}");
            }
            Console.WriteLine();
        }
        public async Task Initalize()
        {
            try
            {
                Console.WriteLine("MultiMaster Conflicts Initialize");

                //Database definition
                Database database = new Database { Id = databaseName };

                //create the database
                await clients[0].CreateDatabaseIfNotExistsAsync(database);

                //Shared Container properties
                PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition();
                partitionKeyDefinition.Paths.Add(PartitionKeyProperty);

                //Conflict Policy for Container using Last Writer Wins
                ConflictResolutionPolicy lwwPolicy = new ConflictResolutionPolicy
                {
                    Mode = ConflictResolutionMode.LastWriterWins,
                    ConflictResolutionPath = "/userDefinedId"
                };

                //Throughput - RUs
                RequestOptions options = new RequestOptions { OfferThroughput = 1000 };

                DocumentCollection containerLww = new DocumentCollection
                {
                    Id = lwwContainerName,
                    PartitionKey = partitionKeyDefinition,
                    ConflictResolutionPolicy = lwwPolicy
                };
                await clients[0].CreateDocumentCollectionIfNotExistsAsync(databaseUri, containerLww, options);

                string udpStoredProcName = "spConflictUDP";
                Uri spUri = UriFactory.CreateStoredProcedureUri(databaseName, udpContainerName, udpStoredProcName);

                //Conflict Policy for Container with User-Defined Stored Procedure
                ConflictResolutionPolicy udpPolicy = new ConflictResolutionPolicy
                {
                    Mode = ConflictResolutionMode.Custom,
                    ConflictResolutionProcedure = spUri.ToString()
                };

                DocumentCollection containerUdp = new DocumentCollection
                {
                    Id = udpContainerName,
                    PartitionKey = partitionKeyDefinition,
                    ConflictResolutionPolicy = udpPolicy
                };
                await clients[0].CreateDocumentCollectionIfNotExistsAsync(databaseUri, containerUdp, options);

                //Stored Procedure definition
                StoredProcedure spConflictUdp = new StoredProcedure
                {
                    Id = udpStoredProcName,
                    Body = File.ReadAllText($@"{udpStoredProcName}.js")
                };

                //Create the Conflict Resolution stored procedure
                await clients[0].CreateStoredProcedureAsync(udpContainerUri, spConflictUdp);


                //Conflict Policy for Container with no Policy and writing to Conflicts Feed
                ConflictResolutionPolicy nonePolicy = new ConflictResolutionPolicy
                {
                    Mode = ConflictResolutionMode.Custom
                };

                DocumentCollection containerNone = new DocumentCollection
                {
                    Id = noneContainerName,
                    PartitionKey = partitionKeyDefinition,
                    ConflictResolutionPolicy = nonePolicy
                };
                await clients[0].CreateDocumentCollectionIfNotExistsAsync(databaseUri, containerNone, options);
            }
            catch (DocumentClientException dcx)
            {
                Console.WriteLine(dcx.Message);
                Debug.Assert(false);
            }
        }
        public async Task RunDemo()
        {
            try
            {
                Console.WriteLine("Multi Master Conflict Resolution");
                Console.WriteLine("--------------------------------");
                await GenerateInsertConflicts(lwwContainerUri, "Generate insert conflicts on container with Last Writer Wins Policy (Max UserDefinedId Wins).");
                await GenerateInsertConflicts(udpContainerUri, "Generate insert conflicts on container with User Defined Procedure Policy (Min UserDefinedId Wins).");
                await GenerateUpdateConflicts(noneContainerUri, "Generate update conficts on container with no Policy defined, write to Conflicts Feed.");

                Console.WriteLine($"Test concluded. Press any key to continue\r\n...");
                Console.ReadKey(true);
            }
            catch (DocumentClientException dcx)
            {
                Console.WriteLine(dcx.Message);
            }
        }
        private async Task GenerateInsertConflicts(Uri collectionUri, string test)
        {
            try
            {
                bool isConflicts = false;

                Console.WriteLine($"{test}\r\nPress any key to continue...");
                Console.ReadKey(true);

                while (!isConflicts)
                {
                    List<Task<SampleCustomer>> tasks = new List<Task<SampleCustomer>>();

                    SampleCustomer customer = customerGenerator.Generate();

                    foreach (DocumentClient client in clients)
                    {
                        tasks.Add(InsertItemAsync(client, collectionUri, customer));
                    }

                    SampleCustomer[] insertedItems = await Task.WhenAll(tasks);

                    isConflicts = IsConflicts(insertedItems);
                }
            }
            catch (DocumentClientException dcx)
            {
                Console.WriteLine(dcx.Message);
                Console.ReadKey();
            }
        }
        private async Task<SampleCustomer> InsertItemAsync(DocumentClient client, Uri collectionUri, SampleCustomer item)
        {
            //DeepCopy the item
            item = Helpers.Clone(item);

            //Update UserDefinedId for each item to random number for Conflict Resolution
            item.UserDefinedId = Helpers.RandomNext(0, 1000);
            //Update the write region to the client regions so we know which client wrote the item
            item.Region = Helpers.ParseEndpoint(client.WriteEndpoint);

            Console.WriteLine($"Attempting insert - Name: {item.Name}, City: {item.City}, UserDefId: {item.UserDefinedId}, Region: {item.Region}");

            try
            {
                var response =  await client.CreateDocumentAsync(collectionUri, item);
                return (SampleCustomer)(dynamic)response.Resource;
            }
            catch (DocumentClientException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    //Item has already replicated so return null
                    return null;
                }
                throw;
            }
        }
        private async Task GenerateUpdateConflicts(Uri collectionUri, string test)
        {
            try
            {
                bool isConflicts = false;

                Console.WriteLine();
                Console.WriteLine($"{test}\r\nPress any key to continue...");
                Console.ReadKey(true);
                Console.WriteLine($"Inserting an item to create an update conflict on.");

                //Generate a new customer, set the region property
                SampleCustomer customer = customerGenerator.Generate();

                SampleCustomer insertedItem = await InsertItemAsync(clients[0], collectionUri, customer);

                Console.WriteLine($"Wait 2 seconds to allow item to replicate.");
                await Task.Delay(2000);

                RequestOptions requestOptions = new RequestOptions
                {
                    PartitionKey = new PartitionKey(PartitionKeyValue)
                };

                while (!isConflicts)
                {
                    IList<Task<SampleCustomer>> tasks = new List<Task<SampleCustomer>>();

                    SampleCustomer item = await clients[0].ReadDocumentAsync<SampleCustomer>(insertedItem.SelfLink, requestOptions);
                    Console.WriteLine($"Original - Name: {item.Name}, City: {item.City}, UserDefId: {item.UserDefinedId}, Region: {item.Region}");

                    foreach (DocumentClient client in clients)
                    {
                        tasks.Add(UpdateItemAsync(client, collectionUri, item));
                    }

                    SampleCustomer[] updatedItems = await Task.WhenAll(tasks);

                    //Delay to allow data to replicate
                    await Task.Delay(2000);

                    isConflicts = IsConflicts(updatedItems);
                }
            }
            catch(DocumentClientException dcx)
            {
                Console.WriteLine(dcx.Message);
                Console.ReadKey();
            }
        }
        private async Task<SampleCustomer> UpdateItemAsync(DocumentClient client, Uri collectionUri, SampleCustomer item)
        {
            //DeepCopy the item
            item = Helpers.Clone(item);

            //Make a change to the item to update.
            item.Region = Helpers.ParseEndpoint(client.WriteEndpoint);
            item.UserDefinedId = Helpers.RandomNext(0, 1000);

            Console.WriteLine($"Update - Name: {item.Name}, City: {item.City}, UserDefId: {item.UserDefinedId}, Region: {item.Region}");

            try
            {
                var response = await client.ReplaceDocumentAsync(item.SelfLink, item, new RequestOptions
                {
                    AccessCondition = new AccessCondition
                    {
                        Type = AccessConditionType.IfMatch,
                        Condition = item.ETag
                    }
                });
                return (SampleCustomer)(dynamic)response.Resource;
            }
            catch (DocumentClientException ex)
            {
                if (ex.StatusCode == HttpStatusCode.PreconditionFailed || ex.StatusCode == HttpStatusCode.NotFound)
                {
                    //No conflict is induced.
                    return null;
                }
                throw;
            }
        }
        private bool IsConflicts(SampleCustomer[] items)
        {
            int operations = 0;
            //Non-null items are successful conflicts
            foreach (var item in items)
            {
                if (item != null)
                {
                    ++operations;
                }
            }

            if (operations > 1)
            {
                Console.WriteLine();
                Console.WriteLine($"Conflicts generated. Confirm in Portal. Press any key to continue...");
                Console.WriteLine();
                Console.ReadKey(true);
                return true;
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine($"No conflicts generated. Retrying to induce conflicts");
                Console.WriteLine();
            }
            return false;
        }
        public async Task CleanUp()
        {
            try
            {
                await clients[0].DeleteDatabaseAsync(databaseUri);
            }
            catch { }
        }
    }
}
