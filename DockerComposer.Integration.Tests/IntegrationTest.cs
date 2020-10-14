using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace DockerComposer.Integration.Tests
{
    public class IntegrationTest
    {
        private readonly ITestOutputHelper _outputHelper;

        public IntegrationTest(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task FullIntegrationTest()
        {
            using var _ = DockerCompose
                .WithComposeFile("Integration.Tests.Compose.yml")
                .ForceBuild()
                .ForceReCreate()
                .Up();

            var client = CreateClient();
            await CreateTestTable(client);

            var tables = await client.ListTablesAsync();

            Assert.True(tables.TableNames.Count == 1, "Wrong number of tables.  Expected 1 but got " + tables.TableNames.Count);
        }

        private AmazonDynamoDBClient CreateClient()
        {
            var config = new AmazonDynamoDBConfig
            {
                ServiceURL = $"http://localhost:8000"
            };

            var credentials = new BasicAWSCredentials("accessKey", "secretKey");
            return new AmazonDynamoDBClient(credentials, config);
        }

        private async Task CreateTestTable(IAmazonDynamoDB dynamoClient)
        {
            _outputHelper.WriteLine("Creating tables.");
            var request = new CreateTableRequest
            {
                TableName = "testTable",
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition
                    {
                        AttributeName = "Id",
                        AttributeType = "N"
                    }
                },
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement
                    {
                        AttributeName = "Id",
                        KeyType = "HASH"
                    }
                },
                ProvisionedThroughput = new ProvisionedThroughput
                {
                    ReadCapacityUnits = 1,
                    WriteCapacityUnits = 1
                }
            };

            try
            {
                await dynamoClient.CreateTableAsync(request);
            }
            catch (ResourceInUseException)
            {
                _outputHelper.WriteLine("Table already exists.");
                throw;
            }
        }
    }
}