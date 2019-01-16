using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace WebMathTraining.Utilities
{
  public class CloudBlobUtility
  {
    public CloudBlobUtility()
    {
      if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production")
      {
        _loginStr = Environment.GetEnvironmentVariable("STORAGE_CONNSTR");
      }
      else
      {
        throw new NotImplementedException();      
      }
      StorageAccount = CloudStorageAccount.Parse(_loginStr);
    }

    public CloudBlobUtility(string acctName, string acctKey) 
      : this($"DefaultEndpointsProtocol=https;AccountName={acctName};AccountKey={acctKey}")
    {
    }

    public CloudBlobUtility(string userConnStr)
    {
      StorageAccount = CloudStorageAccount.Parse(userConnStr);
    }

    //public async CloudBlockBlob UploadBlob(string BlobName, string ContainerName, Stream stream)
    //{

    //  CloudBlobClient blobClient = StorageAccount.CreateCloudBlobClient();
    //  CloudBlobContainer container = blobClient.GetContainerReference(ContainerName.ToLower());
    //  CloudBlockBlob blockBlob = container.GetBlockBlobReference(BlobName);
    //  try
    //  {
    //    await blockBlob.UploadFromStreamAsync(stream);
    //    return blockBlob;
    //  }
    //  catch (Exception e)
    //  {
    //    var r = e.Message;
    //    return null;
    //  }
    //}

    public void DeleteBlob(string BlobName, string ContainerName)
    {
      throw new NotImplementedException();
      //CloudBlobClient blobClient = StorageAccount.CreateCloudBlobClient();
      //CloudBlobContainer container = blobClient.GetContainerReference(ContainerName);
      //CloudBlockBlob blockBlob = container.GetBlockBlobReference(BlobName);
      //blockBlob.Delete();
    }

    public async Task<Tuple<byte[],string>> DownloadBlobToByteArrayAsync(string BlobName, string ContainerName = _containerName)
    {
      CloudBlobClient blobClient = StorageAccount.CreateCloudBlobClient();
      CloudBlobContainer container = blobClient.GetContainerReference(ContainerName);
      CloudBlockBlob blockBlob = container.GetBlockBlobReference(BlobName);
      var ms = new MemoryStream();
      await blockBlob.DownloadToStreamAsync(ms);
      var contentType = blockBlob.Properties.ContentType;
      return new Tuple<byte[], string>(ms.ToArray(), contentType);
    }

    public string DownloadBlobToUrlAsync(string BlobName, string ContainerName = _containerName)
    {
      CloudBlobClient blobClient = StorageAccount.CreateCloudBlobClient();
      CloudBlobContainer container = blobClient.GetContainerReference(ContainerName);
      CloudBlockBlob blockBlob = container.GetBlockBlobReference(BlobName);
      var sasToken = blockBlob.GetSharedAccessSignature(new SharedAccessBlobPolicy()
      {
        Permissions = SharedAccessBlobPermissions.Read,
        SharedAccessExpiryTime = DateTime.UtcNow.AddMinutes(10),//assuming the blob can be downloaded in 10 miinutes
      }, new SharedAccessBlobHeaders()
      {
        ContentDisposition = "attachment; filename=file-name"
      });
      var blobUrl = $"{blockBlob.Uri}{sasToken}";
      return blobUrl;
    }

    public CloudStorageAccount StorageAccount;
    private const string _containerName = "mathpicblobs";
    private readonly string _loginStr;
  }
}
