using System;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using System.Diagnostics;
using System.Collections.Generic;

namespace CosmosGlobalDistDemos
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
        private string databaseName;
        private string containerName;
        private Uri databaseUri;
        private Uri containerUri;
        private string PartitionKeyProperty = ConfigurationManager.AppSettings["PartitionKeyProperty"];
        private string PartitionKeyValue = ConfigurationManager.AppSettings["PartitionKeyValue"];
        private DocumentClient clientEventual;      //West US 2 => Central US (1000 miles)
        private DocumentClient clientStrong1kMiles; //West US 2 => Central US (1000 miles)
        private DocumentClient clientStrong2kMiles; //West US 2 => East Us 2 (2000 miles)

        private List<ResultData> results;
        private Bogus.Faker<SampleCustomer> customerGenerator = new Bogus.Faker<SampleCustomer>().Rules((faker, customer) =>
            {
                customer.Id = Guid.NewGuid().ToString();
                customer.Name = faker.Name.FullName();
                customer.City = faker.Person.Address.City.ToString();
                customer.Region = faker.Person.Address.State.ToString();
                customer.PostalCode = faker.Person.Address.ZipCode.ToString();
                customer.MyPartitionKey = ConfigurationManager.AppSettings["PartitionKeyValue"];
                customer.UserDefinedId = faker.Random.Int(0, 1000);
            });

        public ConsistencyLatency()
        {
            string endpoint, key, region;

            databaseName = ConfigurationManager.AppSettings["database"];
            containerName = ConfigurationManager.AppSettings["container"];
            databaseUri = UriFactory.CreateDatabaseUri(databaseName);
            containerUri = UriFactory.CreateDocumentCollectionUri(databaseName, containerName);
            region = ConfigurationManager.AppSettings["ConsistencyLatencyRegion"];

            Console.WriteLine("Latency vs Eventual and Strong Consistency");
            Console.WriteLine("-------------------------------------------------------------------------------------");

            //Shared connection policy
            ConnectionPolicy policy = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Direct,
                ConnectionProtocol = Protocol.Tcp,
            };
            policy.SetCurrentLocation(region);

            //Eventual consistency client
            endpoint = ConfigurationManager.AppSettings["EventualEndpoint"];
            key = ConfigurationManager.AppSettings["EventualKey"];
            clientEventual = new DocumentClient(new Uri(endpoint), key, policy, ConsistencyLevel.Eventual);
            clientEventual.OpenAsync();
            Console.WriteLine($"Created DocumentClient with Eventual consistency in: {region}.");

            //Strong consistency client 1K miles
            endpoint = ConfigurationManager.AppSettings["Strong1kMilesEndpoint"];
            key = ConfigurationManager.AppSettings["Strong1kMilesKey"];
            clientStrong1kMiles = new DocumentClient(new Uri(endpoint), key, policy, ConsistencyLevel.Strong);
            clientStrong1kMiles.OpenAsync();
            Console.WriteLine($"Created DocumentClient with Strong consistency in: {region} replicating 1000 miles.");

            //Strong consistency client 2K miles
            endpoint = ConfigurationManager.AppSettings["Strong2kMilesEndpoint"];
            key = ConfigurationManager.AppSettings["Strong2kMilesKey"];
            clientStrong2kMiles = new DocumentClient(new Uri(endpoint), key, policy, ConsistencyLevel.Strong);
            clientStrong2kMiles.OpenAsync();
            Console.WriteLine($"Created DocumentClient with Strong consistency in: {region} replicating 2000 miles.");
            Console.WriteLine();
        }
        public async Task Initalize()
        {
            try
            {
                Console.WriteLine("Consistency/Latency Initialize");
                //Database definition
                Database database = new Database { Id = databaseName };

                //Container definition
                PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition();
                partitionKeyDefinition.Paths.Add(PartitionKeyProperty);
                DocumentCollection container = new DocumentCollection { Id = containerName, PartitionKey = partitionKeyDefinition };

                //create the database for all three accounts
                await clientEventual.CreateDatabaseIfNotExistsAsync(database);
                await clientStrong1kMiles.CreateDatabaseIfNotExistsAsync(database);
                await clientStrong2kMiles.CreateDatabaseIfNotExistsAsync(database);

                //Throughput - RUs
                RequestOptions options = new RequestOptions { OfferThroughput = 1000 };

                //Create the container for all three accounts
                await clientEventual.CreateDocumentCollectionIfNotExistsAsync(databaseUri, container, options);
                await clientStrong1kMiles.CreateDocumentCollectionIfNotExistsAsync(databaseUri, container, options);
                await clientStrong2kMiles.CreateDocumentCollectionIfNotExistsAsync(databaseUri, container, options);
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
                results = new List<ResultData>();

                Console.WriteLine("Test Latency between Eventual Consistency vs Strong Consistency at 1000 and 2000 miles");
                Console.WriteLine("-------------------------------------------------------------------------------------");
                Console.WriteLine();

                await WriteBenchmark(clientEventual, "1000 miles");
                await WriteBenchmark(clientStrong1kMiles, "1000 miles");
                await WriteBenchmark(clientStrong2kMiles, "2000 miles", true);
            }
            catch (DocumentClientException dcx)
            {
                Console.WriteLine(dcx.Message);
            }
        }
        private async Task WriteBenchmark(DocumentClient client, string distance, bool final = false)
        {
            Stopwatch stopwatch = new Stopwatch();
            int i = 0;
            int total = 100;
            long lt = 0;
            double ru = 0;

            //Write tests for account with Eventual consistency
            string region = Helpers.ParseEndpoint(client.WriteEndpoint);
            string consistency = client.ConsistencyLevel.ToString();

            Console.WriteLine();
            Console.WriteLine($"Test {total} writes account in {region} with {consistency} consistency level, and replica {distance} away. \r\nPress any key to continue\r\n...");
            Console.ReadKey(true);

            Console.WriteLine();
            for (i = 0; i < total; i++)
            {
                SampleCustomer customer = customerGenerator.Generate();
                stopwatch.Start();
                    ResourceResponse<Document> response = await client.CreateDocumentAsync(containerUri, customer);
                stopwatch.Stop();
                Console.WriteLine($"Write: Item {i} of {total}, Region: {region}, Latency: {stopwatch.ElapsedMilliseconds} ms, Request Charge: {response.RequestCharge} RUs");
                    lt += stopwatch.ElapsedMilliseconds;
                    ru += response.RequestCharge;
                stopwatch.Reset();
            }
            results.Add(new ResultData
                {
                    Test = $"Test with {consistency} Consistency",
                    AvgLatency = (lt / total).ToString(),
                    AvgRU = Math.Round(ru / total).ToString()
                });
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Summary");
            Console.WriteLine("-----------------------------------------------------------------------------------------------------");
            Console.WriteLine($"Test 100 writes against account in {region} with {consistency} consistency level, with replica {distance} away");
            Console.WriteLine();
            Console.WriteLine($"Average Latency:\t{(lt / total)} ms");
            Console.WriteLine($"Average Request Units:\t{Math.Round(ru / total)} RUs");
            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);

            if(final)
            { 
                Console.WriteLine();
                Console.WriteLine("Summary");
                Console.WriteLine("-----------------------------------------------------------------------------------------------------");
                foreach (ResultData r in results)
                {
                    Console.WriteLine($"{r.Test}\tAvg Latency: {r.AvgLatency} ms\tAverage RU: {r.AvgRU}");
                }
                Console.WriteLine();
                Console.WriteLine($"Test concluded. Press any key to continue\r\n...");
                Console.ReadKey(true);
            }
        }
        public async Task CleanUp()
        {
            try
            { 
                await clientEventual.DeleteDatabaseAsync(databaseUri);
                await clientStrong1kMiles.DeleteDatabaseAsync(databaseUri);
                await clientStrong2kMiles.DeleteAttachmentAsync(databaseUri);
            }
            catch {}
        }
    }
}
