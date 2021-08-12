using Grpc.Core;
using Grpc.Net.Client;
using gRPCFileTransfer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileTransfer.Client
{
    class Program
    {
        private static List<FileChunk> Chunks { get; set; }

        const string backend = "https://localhost:5001";
        const int chunkSize = 1024 * 1024;
        const int maxConcurrency = 2;
        static async Task Main(string[] args)
        {
            // The port number(5001) must match the port of the gRPC server.
            using var channel = GrpcChannel.ForAddress(backend);
            var client = new Uploader.UploaderClient(channel);
            Console.Write("Enter the File Path to Upload: ");
            string path = Console.ReadLine(); 

            using FileStream file = File.Open(path, FileMode.Open, FileAccess.Read);
            string fileName = file.Name[(file.Name.LastIndexOf('\\') + 1)..];


            Chunks = new();

            //To compress file either with GZip or Brotli
            //GZipStream compressed = new GZipStream(file, CompressionMode.Compress);
            int totalChunks = (int)(file.Length / chunkSize);
            if (file.Length % chunkSize != 0) totalChunks++; //add fraction as another chunk

            string guid = Guid.NewGuid().ToString();


            byte[] buffer;
            
            
            for (int i = 0; i < totalChunks; i++)
            {
                buffer = new byte[chunkSize];
                file.Read(buffer, 0, chunkSize);

                Chunks.Add(new FileChunk()
                {
                    ChunkIndex = i,
                    FileName = fileName,
                    TotalChunks = totalChunks,
                    FileId = guid,
                    Chunk = Google.Protobuf.ByteString.CopyFrom(buffer),
                    TotalFileSize = file.Length
                });
            }

            //Solution 1: Single thread
            Info info = null;
            while (true)
            {
               var nextProcess = (info == null) ? Chunks.First() : Chunks.First(i => !info.UploadedChunksIndexes.Split(',').Contains(i.ChunkIndex.ToString()));
                
               info = await client.UploadAsync(nextProcess);

               Console.WriteLine($"Uploading Chunks {info?.UploadedChunks}/{info?.TotalChunks} ");

               if (info.UploadedChunks == info.TotalChunks) break;
                
            }

            //Solution 2 Task Library

            // var mutex = new SemaphoreSlim(maxConcurrency);
            // var tasks = Enumerable.Range(0, totalChunks).Select(async index =>
            // {
            //     await mutex.WaitAsync();
            //     try { await client.UploadAsync(Chunks[index]); }
            //     finally { 
            //         mutex.Release(); 
            //         Console.WriteLine($"Uploading Chunks {index}/{totalChunks} ");
            //         }
            // });
            // Task.WhenAll(tasks).Wait();

            //Solution 3 Threadpool Partitioning
            // await Tools.ParallelForEachAsync(Chunks, maxConcurrency, async(c) => 
            //     { 
            //         var info = await client.UploadAsync(c);
            //         Console.WriteLine($"Uploading Chunks {info?.UploadedChunks}/{info?.TotalChunks} ");
            //     });


            //Todo: do more error handling
            await Task.Delay(1); //this line can be removed
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }


    }
    
}
