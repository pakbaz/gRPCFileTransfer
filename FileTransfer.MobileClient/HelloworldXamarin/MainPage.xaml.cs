#region Copyright notice and license
// Copyright 2020 The gRPC Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using Grpc.Core;
using Xamarin.Essentials;
using gRPCFileTransfer;
using System.IO;

namespace HelloworldXamarin
{
    public partial class MainPage : ContentPage
    {
        private static List<FileChunk> Chunks { get; set; }

        const string backend = "https://localhost:5001";
        const int chunkSize = 1024 * 1024;
        const int maxConcurrency = 2;

        public MainPage()
        {
            InitializeComponent();
        }

        private async void Button_Clicked(object sender, EventArgs e)
        {
            // The port number(5001) must match the port of the gRPC server.
            //using var channel = GrpcChannel.ForAddress(backend);
            Channel channel = new Channel(backend, ChannelCredentials.Insecure);
            var client = new Uploader.UploaderClient(channel);
            Console.Write("Enter the File Path to Upload: ");
            string path = Console.ReadLine();

            //FileStream file = File.Open(path, FileMode.Open, FileAccess.Read);
            //string fileName = file.Name.Substring(file.Name.LastIndexOf('\\'));

            var result = await FilePicker.PickAsync();
            var file = await result.OpenReadAsync();


            Chunks = new List<FileChunk>();

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
                    FileName = result.FileName,
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
        }

    }
}
