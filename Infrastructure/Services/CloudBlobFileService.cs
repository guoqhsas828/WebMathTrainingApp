using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using StoreManager.Services;

namespace WebMathTraining.Services
{
  public class CloudBlobFileService : IBlobFileService
  {
    private readonly SendGridOptions _Configuration;

    public CloudBlobFileService(IOptions<SendGridOptions> sendGridOptions)
    {
      _Configuration = sendGridOptions.Value;

      try
      {
        _loginStr = _Configuration.STORAGE_CONNSTR;

        StorageAccount = CloudStorageAccount.Parse(_loginStr);
      }
      catch (Exception ex)
      {
        //TODO add logging info
      }
    }

    public CloudBlobFileService(string acctName, string acctKey) 
      : this($"DefaultEndpointsProtocol=https;AccountName={acctName};AccountKey={acctKey}")
    {
    }

    public CloudBlobFileService(string userConnStr)
    {
      try
      {
        StorageAccount = CloudStorageAccount.Parse(userConnStr);
      }
      catch (Exception ex)
      {
      }
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

    public async void DeleteBlob(string BlobName, string ContainerName)
    {
      CloudBlobClient blobClient = StorageAccount.CreateCloudBlobClient();
      CloudBlobContainer container = blobClient.GetContainerReference(ContainerName);
      CloudBlockBlob blockBlob = container.GetBlockBlobReference(BlobName);
      await blockBlob.DeleteAsync();
    }

    public async Task<IList<string>> ListBlobFileNamesAsync(string ContainerName = _containerName)
    {
      if (string.IsNullOrEmpty(ContainerName)) ContainerName = _containerName;
      CloudBlobClient blobClient = StorageAccount.CreateCloudBlobClient();
      CloudBlobContainer container = blobClient.GetContainerReference(ContainerName);
      BlobContinuationToken continuationToken = null;
      var blockBlobs = await container.ListBlobsSegmentedAsync(continuationToken);
      return blockBlobs.Results.Select(b => b.Uri.Segments.Last()).ToList();
    }

    public async Task<Tuple<byte[], string>> DownloadBlobToByteArrayAsync(string BlobName)
    {
      return await DownloadBlobToByteArrayAsync(BlobName, _containerName);
    }

    public async Task<Tuple<byte[],string>> DownloadBlobToByteArrayAsync(string BlobName, string ContainerName)
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
