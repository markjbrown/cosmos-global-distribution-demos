using System;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace CosmosGlobalDistDemos
{
    //Simplified example for building Custom Synchronization
    public class CustomSyncSample
    {
        private DocumentClient writeClient; //Write to West US 2
        private DocumentClient readClient; //Read from West US

        public async Task Initialize(Uri accountEndpoint, string key)
        {
            //Write Policy
            ConnectionPolicy writeConnectionPolicy = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Direct,
                ConnectionProtocol = Protocol.Tcp,
                UseMultipleWriteLocations = true
            };
            //Write to West US 2
            writeConnectionPolicy.SetCurrentLocation(LocationNames.WestUS2);

            //Read Policy
            ConnectionPolicy readConnectionPolicy = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Direct,
                ConnectionProtocol = Protocol.Tcp
            };
            //Read from West US
            readConnectionPolicy.SetCurrentLocation(LocationNames.WestUS);

            writeClient = new DocumentClient(accountEndpoint, key, writeConnectionPolicy);
            readClient = new DocumentClient(accountEndpoint, key, readConnectionPolicy);

            await Task.WhenAll(new Task[]
            {
            writeClient.OpenAsync(),
            readClient.OpenAsync()
            });
        }

        public async Task CreateItem(string containerLink, Document document)
        {
            ResourceResponse<Document> response = await writeClient.CreateDocumentAsync(
                containerLink, document);

            await readClient.ReadDocumentAsync(response.Resource.SelfLink,
                new RequestOptions { SessionToken = response.SessionToken });
        }
    }
}
