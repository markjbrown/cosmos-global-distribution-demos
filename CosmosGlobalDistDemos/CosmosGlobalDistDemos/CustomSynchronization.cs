using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace CosmosGlobalDistDemos
{
    /*
    * Resources needed for this demo:
    * 
    *   Custom => Cosmos DB account: Replication: Multi-Master, Write Region: West US 2, East US 2, West US, East US, Consistency: Session
    *   Strong => Cosmos DB account: Replication: Multi-Master, Write Region: West US 2, East US 2, West US, East US, Consistency: Strong
    *   
*/
    public class CustomSynchronization
    {
        private string databaseName;
        private string containerName;
        private Uri databaseUri;
        private Uri containerUri;
        private string PartitionKeyProperty = ConfigurationManager.AppSettings["PartitionKeyProperty"];
        private string PartitionKeyValue = ConfigurationManager.AppSettings["PartitionKeyValue"];
        private DocumentClient readClient;
        private DocumentClient writeClient;
        private DocumentClient strongClient;

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

        public CustomSynchronization()
        {
            string endpoint, key, writeRegion, readRegion;

            databaseName = ConfigurationManager.AppSettings["database"];
            containerName = ConfigurationManager.AppSettings["container"];
            databaseUri = UriFactory.CreateDatabaseUri(databaseName);
            containerUri = UriFactory.CreateDocumentCollectionUri(databaseName, containerName);
            writeRegion = ConfigurationManager.AppSettings["WriteRegion"];
            readRegion = ConfigurationManager.AppSettings["readRegion"];

            Console.WriteLine("Custom Synchronization for Stronger Consistency without Latency");
            Console.WriteLine("---------------------------------------------------------------");

            //Shared endpoint and key
            endpoint = ConfigurationManager.AppSettings["CustomSyncEndpoint"];
            key = ConfigurationManager.AppSettings["CustomSyncKey"];

            //Write client
            ConnectionPolicy writePolicy = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Direct,
                ConnectionProtocol = Protocol.Tcp,
                UseMultipleWriteLocations = true
            };
            writePolicy.SetCurrentLocation(writeRegion);
            writeClient = new DocumentClient(new Uri(endpoint), key, writePolicy);
            Console.WriteLine($"Created Write Client with Session consistency in: {writeRegion}.");

            //Read client policy
            ConnectionPolicy readPolicy = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Direct,
                ConnectionProtocol = Protocol.Tcp
            };
            readPolicy.SetCurrentLocation(readRegion);
            readClient = new DocumentClient(new Uri(endpoint), key, readPolicy, ConsistencyLevel.Session);
            Console.WriteLine($"Created Read Client with Session consistency in: {readRegion}.");

            //Strong consistency client
            ConnectionPolicy strongPolicy = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Direct,
                ConnectionProtocol = Protocol.Tcp,
            };
            strongPolicy.SetCurrentLocation(writeRegion);
            endpoint = ConfigurationManager.AppSettings["StrongEndpoint"];
            key = ConfigurationManager.AppSettings["StrongKey"];
            strongClient = new DocumentClient(new Uri(endpoint), key, strongPolicy, ConsistencyLevel.Strong);
            Console.WriteLine($"Created client with Strong consistency in: {writeRegion}.");
            Console.WriteLine();

            writeClient.OpenAsync();
            readClient.OpenAsync();
            strongClient.OpenAsync();
        }
        public async Task Initalize()
        {
            try
            {
                Console.WriteLine("Custom Synchronization Initialize");
                //Database definition
                Database database = new Database { Id = databaseName };

                //Container definition
                PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition();
                partitionKeyDefinition.Paths.Add(PartitionKeyProperty);
                DocumentCollection container = new DocumentCollection { Id = containerName, PartitionKey = partitionKeyDefinition };

                //create the database for all accounts
                await writeClient.CreateDatabaseIfNotExistsAsync(database);
                await strongClient.CreateDatabaseIfNotExistsAsync(database);

                //Throughput - RUs
                RequestOptions options = new RequestOptions { OfferThroughput = 1000 };

                //Create the container for all accounts
                await writeClient.CreateDocumentCollectionIfNotExistsAsync(databaseUri, container, options);
                await strongClient.CreateDocumentCollectionIfNotExistsAsync(databaseUri, container, options);
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

                Console.WriteLine("Test Latency between Strong Consistency all regions vs. single region");
                Console.WriteLine("---------------------------------------------------------------------");
                Console.WriteLine();

                await WriteBenchmarkStrong(strongClient);
                await WriteBenchmarkCustomSync(writeClient, readClient);
            }
            catch (DocumentClientException dcx)
            {
                Console.WriteLine(dcx.Message);
            }
        }
        private async Task WriteBenchmarkStrong(DocumentClient client)
        {
            Stopwatch stopwatch = new Stopwatch();

            int i = 0;
            int total = 100;
            long lt = 0;
            double ru = 0;

            string region = Helpers.ParseEndpoint(client.WriteEndpoint);
            string consistency = client.ConsistencyLevel.ToString();

            Console.WriteLine($"Test {total} writes account in {region} with {consistency} consistency between all replicas. \r\nPress any key to continue\r\n...");
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
                Test = $"Test with all {consistency} Consistency",
                AvgLatency = (lt / total).ToString(),
                AvgRU = Math.Round(ru / total).ToString()
            });
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Test Summary");
            Console.WriteLine("-----------------------------------------------------------------------------------------------------");
            Console.WriteLine($"Test {total} writes account in {region} with {consistency} consistency between all replicas");
            Console.WriteLine();
            Console.WriteLine($"Average Latency:\t{(lt / total)} ms");
            Console.WriteLine($"Average Request Units:\t{Math.Round(ru / total)} RUs");
            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }
        private async Task WriteBenchmarkCustomSync(DocumentClient writeClient, DocumentClient readClient)
        {
            Stopwatch stopwatch = new Stopwatch();

            int i = 0;
            int total = 100;
            long lt = 0;
            double ru = 0;
            long ltAgg = 0;
            double ruAgg = 0;

            string writeRegion = Helpers.ParseEndpoint(writeClient.WriteEndpoint);
            string readRegion = Helpers.ParseEndpoint(readClient.ReadEndpoint);
            string consistency = writeClient.ConsistencyLevel.ToString();

            Console.WriteLine();
            Console.WriteLine($"Test {total} writes in {writeRegion} with {consistency} consistency between all replicas except {readRegion} with Strong consistency. \r\nPress any key to continue\r\n...");
            Console.ReadKey(true);

            PartitionKey partitionKeyValue = new PartitionKey(PartitionKeyValue);

            Console.WriteLine();
            for (i = 0; i < total; i++)
            {
                SampleCustomer customer = customerGenerator.Generate();

                stopwatch.Start();
                    ResourceResponse<Document> writeResponse = await writeClient.CreateDocumentAsync(containerUri, customer);
                stopwatch.Stop();
                        lt += stopwatch.ElapsedMilliseconds;
                        ru += writeResponse.RequestCharge;
                stopwatch.Reset();

                stopwatch.Start();
                    ResourceResponse<Document> readResponse = await readClient.ReadDocumentAsync(writeResponse.Resource.SelfLink, 
                        new RequestOptions { PartitionKey = partitionKeyValue, SessionToken = writeResponse.SessionToken});
                stopwatch.Stop();
                        lt += stopwatch.ElapsedMilliseconds;
                        ru += readResponse.RequestCharge;
                stopwatch.Reset();
                Console.WriteLine($"Write/Read: Item {i} of {total}, Region: {writeRegion}, Latency: {lt} ms, Request Charge: {ru} RUs");

                ltAgg += lt;
                ruAgg += ru;
                lt = 0;
                ru = 0;
            }
            results.Add(new ResultData
            {
                Test = $"Test with Custom Synchronization",
                AvgLatency = (ltAgg / total).ToString(),
                AvgRU = Math.Round(ruAgg / total).ToString()
            });
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Test Summary");
            Console.WriteLine("-------------------------------------------------------------------------------------------------------------------------------------");
            Console.WriteLine($"Test {total} writes in {writeRegion} with {consistency} consistency between all replicas except {readRegion} with Strong consistency");
            Console.WriteLine();
            Console.WriteLine($"Average Latency:\t{(ltAgg / total)} ms");
            Console.WriteLine($"Average Request Units:\t{Math.Round(ruAgg / total)} RUs");
            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
             
            Console.WriteLine();
            Console.WriteLine("All Tests Summary");
            Console.WriteLine("-----------------------------------------------------------------------------------------------------");
            foreach (ResultData r in results)
            {
                Console.WriteLine($"{r.Test}\tAvg Latency: {r.AvgLatency} ms\tAverage RU: {r.AvgRU}");
            }
            Console.WriteLine();
            Console.WriteLine($"Test concluded. Press any key to continue\r\n...");
            Console.ReadKey(true);
        }
        public async Task CleanUp()
        {
            try
            {
                await writeClient.DeleteDatabaseAsync(databaseUri);
                await strongClient.DeleteDatabaseAsync(databaseUri);
            }
            catch { }
        }
    }
}
