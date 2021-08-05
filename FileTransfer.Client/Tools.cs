using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileTransfer.Client
{
    public static class Tools
    {
        public static Task ParallelForEachAsync<T>(this IEnumerable<T> source, int maxConcurrency, Func<T, Task> funcBody)
        {
            async Task AwaitPartition(IEnumerator<T> partition)
            {
                using (partition)
                {
                    while (partition.MoveNext())
                    {
                        await Task.Yield(); // prevents a sync/hot thread hangup
                        await funcBody(partition.Current);
                    }
                }
            }

            return Task.WhenAll(
                Partitioner
                    .Create(source)
                    .GetPartitions(maxConcurrency)
                    .AsParallel()
                    .Select(p => AwaitPartition(p)));
        }
    }
}
