using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebMathTraining.Services
{
  public interface IBlobFileService
  {
    Task<Tuple<byte[], string>> DownloadBlobToByteArrayAsync(string BlobName);
    Task<Tuple<byte[], string>> DownloadBlobToByteArrayAsync(string BlobName, string ContainerName);
  }
}
