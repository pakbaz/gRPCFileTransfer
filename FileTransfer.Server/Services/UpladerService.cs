using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace gRPCFileTransfer
{
    public class UploaderService : Uploader.UploaderBase
    {
        private static readonly ConcurrentDictionary<string, List<FileChunk>> files = new();
        private static readonly ReaderWriterLock locker = new();
        private readonly ILogger<UploaderService> _logger;
        public UploaderService(ILogger<UploaderService> logger)
        {
            _logger = logger;
        }

        public override Task<Info> Upload(FileChunk request, ServerCallContext context)
        {
            if (!files.ContainsKey(request.FileId))
                files.TryAdd(request.FileId, new List<FileChunk>());

            files[request.FileId].Add(request);

            request.Chunk.ToByteArray();

            if (request.TotalChunks == files[request.FileId].Count)
            {
                try
                {
                    byte[] allBytes = Combine(files[request.FileId]
                            .OrderBy(i => i.ChunkIndex)
                            .Select(i => i.Chunk.ToByteArray())
                            .ToArray());

                    //Here is to Decompress if compressed while transferring
                    //GZipStream decompressed = new GZipStream( new MemoryStream(allBytes), CompressionMode.Decompress);

                    locker.AcquireWriterLock(int.MaxValue);
                    File.WriteAllBytes(request.FileName, allBytes);
                }
                catch
                {
                    //TODO: better error handling, possibly a flag for not to delete dictionary item

                }
                finally
                {
                    locker.ReleaseWriterLock();
                }
            }

            //get Info and cleanup
            Info result = new()
            {
                TotalChunks = request.TotalChunks,
                UploadedChunks = files[request.FileId].Count,
                UploadedChunksIndexes = string.Join(',', files[request.FileId].Select(i => i.ChunkIndex.ToString()))
            };
            //Cleanup once all file is saved
            //TODO: may double check with any error flag to make sure
            List<FileChunk> tobeRemoved = new();
            if(result.UploadedChunks == result.TotalChunks)
            {
                files.TryRemove(request.FileId,out tobeRemoved);
            }

            //TODO: make another copy of the tobeRemoved data somewhere before its gone

            return Task.FromResult(result);
        }

        #region Combine Chunks
        private static byte[] Combine(params byte[][] arrays)
        {
            int counter = 0;
            byte[] rv = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                counter++;
                if (counter == arrays.Length)
                    System.Buffer.BlockCopy(TrimEnd(array), 0, rv, offset, TrimEnd(array).Length);
                else
                    System.Buffer.BlockCopy(array, 0, rv, offset, array.Length);

                offset += array.Length;
            }
            return rv;
        }

        public static byte[] TrimEnd(byte[] array)
        {
            int lastIndex = Array.FindLastIndex(array, b => b != 0);

            Array.Resize(ref array, lastIndex + 1);

            return array;
        }

        #endregion
    }
}
