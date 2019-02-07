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
     *   Single Master => Cosmos DB account: Replication: Single-Master, Write Region: East US 2, Read Region: West US 2, Consistency: Session
     *   Multi-Master => Cosmos DB account: Replication: Multi-Master, Write Region: East US 2, West US 2, North Europe, Consistency: Session
     *   
    */
    class SingleMultiMaster
    {
        private string databaseName;
        private string containerName;
        private Uri databaseUri;
        private Uri containerUri;
        private string PartitionKeyProperty = ConfigurationManager.AppSettings["PartitionKeyProperty"];
        private string PartitionKeyValue = ConfigurationManager.AppSettings["PartitionKeyValue"];
        private DocumentClient clientSingle;
        private DocumentClient clientMulti;

        private List<ResultData> results;

        private Bogus.Faker<SampleCustomer> customerGenerator = new Bogus.Faker<SampleCustomer>().Rules(
            (faker, customer) =>
            {
                customer.Id = Guid.NewGuid().ToString();
                customer.Name = faker.Name.FullName();
                customer.City = faker.Person.Address.City.ToString();
                customer.Region = faker.Person.Address.State.ToString();
                customer.PostalCode = faker.Person.Address.ZipCode.ToString();
                customer.MyPartitionKey = ConfigurationManager.AppSettings["PartitionKeyValue"];
                customer.UserDefinedId = faker.Random.Int(0, 1000);
            }
        );

        public SingleMultiMaster()
        {
            string endpoint, key, region;

            //Database and Container definitions for Single and Multi-Master accounts
            databaseName = ConfigurationManager.AppSettings["database"];
            containerName = ConfigurationManager.AppSettings["container"];

            databaseUri = UriFactory.CreateDatabaseUri(databaseName);
            containerUri = UriFactory.CreateDocumentCollectionUri(databaseName, containerName);

            Console.WriteLine($"Single-Master vs Multi-Master Latency");
            Console.WriteLine("--------------------------------------");

            //Single-Master Connection Policy
            ConnectionPolicy policySingleMaster = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Direct,
                ConnectionProtocol = Protocol.Tcp,
            };
            region = ConfigurationManager.AppSettings["SingleMultiMasterRegion"];
            policySingleMaster.SetCurrentLocation(region);

            // Create the Single-Master account client
            endpoint = ConfigurationManager.AppSettings["SingleMasterEndpoint"];
            key = ConfigurationManager.AppSettings["SingleMasterKey"];
            clientSingle = new DocumentClient(new Uri(endpoint), key, policySingleMaster, ConsistencyLevel.Eventual);
            clientSingle.OpenAsync();

            Console.WriteLine($"Created DocumentClient for Single-Master account in: {region}.");


            //Multi-Master Connection Policy
            ConnectionPolicy policyMultiMaster = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Direct,
                ConnectionProtocol = Protocol.Tcp,
                UseMultipleWriteLocations = true //Required for Multi-Master
            };
            region = ConfigurationManager.AppSettings["SingleMultiMasterRegion"];
            policyMultiMaster.SetCurrentLocation(region); //Enable multi-homing


            // Create the Multi-Master account client
            endpoint = ConfigurationManager.AppSettings["MultiMasterEndpoint"];
            key = ConfigurationManager.AppSettings["MultiMasterKey"];
            clientMulti = new DocumentClient(new Uri(endpoint), key, policyMultiMaster, ConsistencyLevel.Eventual);
            clientMulti.OpenAsync();

            Console.WriteLine($"Created DocumentClient for Multi-Master account in: {region}.");
            Console.WriteLine();
        }
        public async Task Initalize()
        {
            //Database definition
            Database database = new Database { Id = databaseName };

            //Container properties
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add(PartitionKeyProperty);
            RequestOptions options = new RequestOptions { OfferThroughput = 1000,  };

            //Container definition
            DocumentCollection container = new DocumentCollection { Id = containerName, PartitionKey = partitionKeyDefinition };
            

            //Single-Master
            await clientSingle.CreateDatabaseIfNotExistsAsync(database);
            await clientSingle.CreateDocumentCollectionIfNotExistsAsync(databaseUri, container, options);
        
            //Multi-Master
            await clientMulti.CreateDatabaseIfNotExistsAsync(database);
            await clientMulti.CreateDocumentCollectionIfNotExistsAsync(databaseUri, container, options);

            //Pre-Load data
            await Populate(clientSingle, "Single-Master");
            await Populate(clientMulti, "Multi-Master");
        }
        private async Task Populate(DocumentClient client, string accountType)
        {
            var sql = "SELECT * FROM c";

            FeedOptions feedOptions = new FeedOptions
            {
                PartitionKey = new PartitionKey(PartitionKeyValue)
            };

            var documents = client.CreateDocumentQuery(containerUri, sql, feedOptions).ToList();
            if (documents.Count == 0)
            {
                Console.WriteLine($"Populating {accountType} container for Read-Latency Test");
                for (int i = 0; i < 100; i++)
                {
                    SampleCustomer customer = customerGenerator.Generate();
                    await client.CreateDocumentAsync(containerUri, customer);
                }
                Console.WriteLine("Populating complete");
            }
        }

        public async Task RunDemo()
        {
            results = new List<ResultData>();

            Console.WriteLine($"Test read and write latency between a Single-Master and Multi-Master account");
            Console.WriteLine("-----------------------------------------------------------------------------");
            Console.WriteLine();

            await ReadBenchmark(clientSingle, "Single-Master");
            await WriteBenchmark(clientSingle, "Single-Master");
            await ReadBenchmark(clientMulti, "Multi-Master");
            await WriteBenchmark(clientMulti, "Multi-Master", true);
        }

        private async Task ReadBenchmark(DocumentClient client, string accountType, bool final = false)
        {
            string region = ParseEndpoint(client.ReadEndpoint);
            Stopwatch stopwatch = new Stopwatch();

            FeedOptions feedOptions = new FeedOptions
            {
                PartitionKey = new PartitionKey(PartitionKeyValue)
            };
            string sql = "SELECT * FROM c";
            var items = client.CreateDocumentQuery(containerUri, sql, feedOptions).ToList();

            int i = 0;
            int total = items.Count;
            long ms = 0;
            int lt = 0;
            double ru = 0;

            Console.WriteLine();
            Console.WriteLine($"Test {total} reads against {accountType} account in {region}\r\nPress any key to continue\r\n...");
            Console.ReadKey(true);

            RequestOptions requestOptions = new RequestOptions
            {
                PartitionKey = new PartitionKey(PartitionKeyValue)
            };

            foreach (Document item in items)
            {
                stopwatch.Start();
                    ResourceResponse<Document> response = await client.ReadDocumentAsync(item.SelfLink, requestOptions);
                stopwatch.Stop();
                Console.WriteLine($"Read {i} of {total}, region: {region}, Latency: {response.RequestLatency.Milliseconds} ms, Request Charge: {response.RequestCharge} RUs");
                ms += stopwatch.ElapsedMilliseconds;
                lt += response.RequestLatency.Milliseconds;
                ru += response.RequestCharge;
                i++;
                stopwatch.Reset();
            }
            results.Add(new ResultData
            {
                Test = $"Test reads against {accountType} account in {region}",
                AvgLatency = (lt / total).ToString(),
                AvgRU = Math.Round(ru / total).ToString()
            });
            Console.WriteLine();
            Console.WriteLine("Summary");
            Console.WriteLine("-----------------------------------------------------------------------------------------------------");
            Console.WriteLine($"Test {total} reads against {accountType} account in {region}");
            Console.WriteLine();
            Console.WriteLine($"Average Latency:\t{(lt / total)} ms");
            Console.WriteLine($"Average Request Units:\t{Math.Round(ru / total)} RUs");
            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);

            if (final)
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

        private async Task WriteBenchmark(DocumentClient client, string accountType, bool final = false)
        {
            string region = ParseEndpoint(client.WriteEndpoint);
            Stopwatch stopwatch = new Stopwatch();

            int i = 0;
            int total = 100;
            int lt = 0;
            double ru = 0;

            Console.WriteLine();
            Console.WriteLine($"Test {total} writes against {accountType} account in {region}\r\nPress any key to continue\r\n...");
            Console.ReadKey(true);

            for(i=0; i < total; i++)
            {
                SampleCustomer customer = customerGenerator.Generate();
                stopwatch.Start();
                    ResourceResponse<Document> response = await client.CreateDocumentAsync(containerUri, customer);
                stopwatch.Stop();
                Console.WriteLine($"Write {i} of {total}, to region: {region}, Latency: {response.RequestLatency.Milliseconds} ms, Request Charge: {response.RequestCharge} RUs");
                lt += response.RequestLatency.Milliseconds;
                ru += response.RequestCharge;
                stopwatch.Reset();
            }
            results.Add(new ResultData
            {
                Test = $"Test writes against {accountType} account in {region}",
                AvgLatency = (lt / total).ToString(),
                AvgRU = Math.Round(ru / total).ToString()
            });
            Console.WriteLine();
            Console.WriteLine("Summary");
            Console.WriteLine("-----------------------------------------------------------------------------------------------------");
            Console.WriteLine($"Test {total} writes against {accountType} account in {region}");
            Console.WriteLine();
            Console.WriteLine($"Average Latency:\t{(lt / total)} ms");
            Console.WriteLine($"Average Request Units:\t{Math.Round(ru / total)} RUs");
            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);

            if (final)
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
            await clientSingle.DeleteDatabaseAsync(databaseUri);
            await clientMulti.DeleteDatabaseAsync(databaseUri);
        }

        private string ParseEndpoint(Uri endPoint)
        {
            //"https://mjb-latency-multi-region-southeastasia.documents.azure.com/";

            string x = endPoint.ToString();

            int tail = x.IndexOf(".documents.azure.com");
            int head = x.LastIndexOf("-") + 1;

            return x.Substring(head, (tail - head));
        }
    }
}
