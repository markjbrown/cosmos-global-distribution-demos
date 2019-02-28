using System;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;

namespace CosmosGlobalDistDemos
{

    /*
        * Resources needed for this demo:
        * 
        * Shared for all demos in this solution
        * - Windows VM, West US 2, Standard B4 (4 core, 16GB), RDP enabled. This solution gets run from the VM.
        * 
        *   Single Region => Cosmos DB account: Replication: Single-Master, Write Region: Southeast Asia, Consistency: Eventual
        *   Multi-Region => Cosmos DB account: Replication: Single-Master, Write Region: Southeast Asia, Read Region: West US 2, Consistency: Eventual
        *   
    */



    class SingleMultiRegion
    {
        private string databaseName;
        private string containerName;
        private string storedProcName;
        private Uri databaseUri;
        private Uri containerUri;
        private Uri storedProcUri;
        private string PartitionKeyProperty = ConfigurationManager.AppSettings["PartitionKeyProperty"];
        private string PartitionKeyValue = ConfigurationManager.AppSettings["PartitionKeyValue"];
        private DocumentClient clientSingle;
        private DocumentClient clientMulti;

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

        public SingleMultiRegion()
        {
            Console.WriteLine("Single Region vs Multi-Region Read Latency");
            Console.WriteLine("-------------------------------------------------------------------------");

            string endpoint, key, region;
            
            databaseName = ConfigurationManager.AppSettings["database"];
            containerName = ConfigurationManager.AppSettings["container"];
            storedProcName = ConfigurationManager.AppSettings["storedproc"];
            databaseUri = UriFactory.CreateDatabaseUri(databaseName);
            containerUri = UriFactory.CreateDocumentCollectionUri(databaseName, containerName);
            storedProcUri = UriFactory.CreateStoredProcedureUri(databaseName, containerName, storedProcName);

            //Single-Region account client
            endpoint = ConfigurationManager.AppSettings["SingleRegionEndpoint"];
            key = ConfigurationManager.AppSettings["SingleRegionKey"];
            region = ConfigurationManager.AppSettings["SingleRegionRegion"];

            ConnectionPolicy policy = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Direct,
                ConnectionProtocol = Protocol.Tcp,
            };

            policy.SetCurrentLocation(region);
            clientSingle = new DocumentClient(new Uri(endpoint), key, policy, ConsistencyLevel.Eventual);
            clientSingle.OpenAsync();

            Console.WriteLine($"Created DocumentClient for Single-Region account in: {region}.");

            //Multi-Region account client
            endpoint = ConfigurationManager.AppSettings["MultiRegionEndpoint"];
            key = ConfigurationManager.AppSettings["MultiRegionKey"];
            region = ConfigurationManager.AppSettings["MultiRegionRegion"];

            policy.SetCurrentLocation(region);
            clientMulti = new DocumentClient(new Uri(endpoint), key, policy, ConsistencyLevel.Eventual);
            clientMulti.OpenAsync();

            Console.WriteLine($"Created DocumentClient for Multi-Region account in: {region}.");
            Console.WriteLine();
        }
        public async Task Initalize()
        {
            try
            {
                Console.WriteLine("Single/Multi Region Initialize");
                //Database definition
                Database database = new Database { Id = databaseName };

                //Throughput - RUs
                RequestOptions options = new RequestOptions { OfferThroughput = 1000 };

                //Container definition
                PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition();
                partitionKeyDefinition.Paths.Add(PartitionKeyProperty);
                DocumentCollection container = new DocumentCollection { Id = containerName, PartitionKey = partitionKeyDefinition };

                //Stored Procedure definition
                StoredProcedure spBulkUpload = new StoredProcedure
                {
                    Id = "spBulkUpload",
                    Body = File.ReadAllText($@"spBulkUpload.js")
                };

                //Single Region
                await clientSingle.CreateDatabaseIfNotExistsAsync(database);
                await clientSingle.CreateDocumentCollectionIfNotExistsAsync(databaseUri, container, options);
                await clientSingle.CreateStoredProcedureAsync(containerUri, spBulkUpload);

                //Multi Region
                await clientMulti.CreateDatabaseIfNotExistsAsync(database);
                await clientMulti.CreateDocumentCollectionIfNotExistsAsync(databaseUri, container, options);
                await clientMulti.CreateStoredProcedureAsync(containerUri, spBulkUpload);                
            }
            catch (DocumentClientException dcx)
            {
                Console.WriteLine(dcx.Message);
                Debug.Assert(false);
            }
        }
        public async Task LoadData()
        {
            await Populate(clientSingle);
            await Populate(clientMulti);
        }
        private async Task Populate(DocumentClient client)
        {
            List<SampleCustomer> sampleCustomers = customerGenerator.Generate(100);

            int inserted = 0;

            RequestOptions options = new RequestOptions
            {
                PartitionKey = new PartitionKey(PartitionKeyValue)
            };

            while(inserted < sampleCustomers.Count)
            {
                StoredProcedureResponse<int> result = await client.ExecuteStoredProcedureAsync<int>(storedProcUri, options, sampleCustomers.Skip(inserted));
                inserted += result.Response;
                Console.WriteLine($"Inserted {inserted} items.");
            }
        }
        public async Task RunDemo()
        {
            try
            {
                results = new List<ResultData>();

                Console.WriteLine("Test Read Latency between a Single Region Account vs Multi-Region Account");
                Console.WriteLine("-------------------------------------------------------------------------");
                Console.WriteLine();

                await ReadBenchmark(clientSingle, "Single-Region");
                await ReadBenchmark(clientMulti, "Multi-Region", true);
            }
            catch (DocumentClientException dcx)
            {
                Console.WriteLine(dcx.Message);
            }
        }
        private async Task ReadBenchmark(DocumentClient client, string replicaType, bool final = false)
        {
            Stopwatch stopwatch = new Stopwatch();

            string region = Helpers.ParseEndpoint(client.ReadEndpoint);

            FeedOptions feedOptions = new FeedOptions
            {
                PartitionKey = new PartitionKey(PartitionKeyValue)
            };
            string sql = "SELECT c._self FROM c";
            var items = client.CreateDocumentQuery(containerUri, sql, feedOptions).ToList();

            int i = 0;
            int total = items.Count;
            long lt = 0;
            double ru = 0;

            Console.WriteLine();
            Console.WriteLine($"Test {total} reads against {replicaType} account in {region} from West US 2\r\nPress any key to continue\r\n...");
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
                Console.WriteLine($"Read {i} of {total}, region: {region}, Latency: {stopwatch.ElapsedMilliseconds} ms, Request Charge: {response.RequestCharge} RUs");
                lt += stopwatch.ElapsedMilliseconds;
                ru += response.RequestCharge;
                i++;
                stopwatch.Reset();
            }
            results.Add(new ResultData
            {
                Test = $"Test {replicaType}",
                //Test = $"{replicaType} account in {region} from West US 2",
                AvgLatency = (lt / total).ToString(),
                AvgRU = Math.Round(ru / total).ToString()
            });
            Console.WriteLine();
            Console.WriteLine("Summary");
            Console.WriteLine("-----------------------------------------------------------------------------------------------------");
            Console.WriteLine($"Test {total} reads against {replicaType} account in {region}");
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
            try
            {
                await clientSingle.DeleteDatabaseAsync(databaseUri);
                await clientMulti.DeleteDatabaseAsync(databaseUri);
            }
            catch { }
        }
    }
}
