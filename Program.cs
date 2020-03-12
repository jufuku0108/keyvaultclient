using System;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Identity.Client;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using Azure.Storage.Blobs;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Azure.Storage.Blobs.Models;
using System.Net;
using Microsoft.Azure.Storage;

namespace KeyVaultClient
{
    class Program
    {
        static void Main(string[] args)
        {
            RunAsyncProgram().GetAwaiter().GetResult();
        }
        private static async Task RunAsyncProgram()
        {
            // Settings for Msal
            var tenantId = "************************************";
            var clientId = "************************************";
            var clientSecret = "************************************";

            // Setting for getting blob credential from Key Vault
            var keyVault = "https://myvault.vault.azure.net/";
            var secretForAccountKey = "************************************";
            var secretForSas = "************************************";

            // Setting for Blob
            string blobEndpoint = "https://mystorage.blob.core.windows.net/";
            string containerName = "************************************";



            // Acuire Token
            /* Use Managed ID
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("=== Getting Access Token using Managed Id ===");
            Console.ResetColor();

            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create("http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=https://vault.azure.net");
            httpWebRequest.Headers["Metadata"] = "true";
            httpWebRequest.Method = "GET";
            HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();

            StreamReader streamReader = new StreamReader(httpWebResponse.GetResponseStream());
            string stringResponse = streamReader.ReadToEnd();
            JObject jobjResponse = JObject.Parse(stringResponse);
            string accessToken = (string)jobjResponse["access_token"];
            Console.WriteLine(stringResponse);
             */


            /* Use MSAL with client credential
             */
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("=== Getting Access Token using Msal ===");
            Console.ResetColor();

            IConfidentialClientApplication app;
            app = ConfidentialClientApplicationBuilder.Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority("https://login.microsoftonline.com/" + tenantId)
                .Build();

            string[] scopes = new string[] { "https://vault.azure.net/.default" };

            AuthenticationResult authenticationResult = await app.AcquireTokenForClient(scopes).ExecuteAsync();
            var accessToken = authenticationResult.AccessToken;
            Console.WriteLine(authenticationResult.AccessToken);


            // Call KeyVault to get connection string
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("=== Get Access Key from KeyVault ===");
            Console.ResetColor();

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);


            var keyVaultUriForAccessKey = keyVault + "secrets/" + secretForAccountKey + "?api-version=7.0";
            HttpResponseMessage httpResponseForAccountKey = await httpClient.GetAsync(keyVaultUriForAccessKey);
            string jsonForAccountKey = await httpResponseForAccountKey.Content.ReadAsStringAsync();
            Console.WriteLine(jsonForAccountKey);

            // Upload file to Blob using BlobClient
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("=== Upload file to Blob using Access Key ===");
            Console.ResetColor();

            JObject jObjectForAccontKey = JObject.Parse(jsonForAccountKey);
            string connectionString = (string)jObjectForAccontKey["value"];
            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
            BlobContainerClient blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);

            string localPath = "./";
            string fileName = "quickStart" + Guid.NewGuid().ToString() + ".txt";
            string localFilePath = Path.Combine(localPath, fileName);
            await File.WriteAllTextAsync(localFilePath, "Hello world");
            var blobClient = blobContainerClient.GetBlobClient(fileName);

            FileStream fileStream = File.OpenRead(localFilePath);
            await blobClient.UploadAsync(fileStream);
            fileStream.Close();
            Console.WriteLine("Uploaded file to Blob storage as blob:\n\t {0}\n", blobClient.Uri);


            // Call KeyVault to get Sas Token
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("=== Get Sas Token from KeyVault ===");
            Console.ResetColor();

            var keyVaultUriForSas = keyVault + "secrets/" + secretForSas + "?api-version=7.0";
            HttpResponseMessage httpResponseForSas = await httpClient.GetAsync(keyVaultUriForSas);
            string jsonForSas = await httpResponseForSas.Content.ReadAsStringAsync();
            Console.WriteLine(jsonForSas);

            // List files to Blob using SasClient
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("=== List blob files using Sas ===");
            Console.ResetColor();

            JObject jObjectForSas = JObject.Parse(jsonForSas);
            string sasToken = (string)jObjectForSas["value"];
            var accountSasCredential = new StorageCredentials(sasToken);
            var accountWithSas = new CloudStorageAccount(accountSasCredential, new Uri(blobEndpoint), null, null, null);
            var blobClientWithSas = accountWithSas.CreateCloudBlobClient();
            var cloudBlobContainer = blobClientWithSas.GetContainerReference(containerName);
            var listBlobItems = cloudBlobContainer.ListBlobs();

            Console.WriteLine("Listing blobs.....");
            foreach (var blobItem in listBlobItems)
            {
                Console.WriteLine("\t" + blobItem.Uri);
            }
        }
    }
}
