using System;
using System.Threading;
using System.Threading.Tasks;

namespace MailKit.Net.Common
{
    public class EngineLock
    {
        readonly SemaphoreSlim _slim = new SemaphoreSlim(1);

        public async Task StartAsync(Func<Task> action)
        {
            try
            {
                await _slim.WaitAsync();
                await action();
            }
            finally
            {
                _slim.Release();
            }
        }

        public async Task<T> StartAsync<T>(Func<Task<T>> action)
        {
            try
            {
                await _slim.WaitAsync();
                return await action();
            }
            finally
            {
                _slim.Release();
            }
        }
    }
}