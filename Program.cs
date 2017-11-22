using System;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime.Configuration;
using Orleans.Hosting;
using Orleans.Runtime;
using Microsoft.Extensions.Logging;

namespace SDOrleans
{
    public interface IFacGrain : IGrainWithIntegerKey
    {
        Task<long> Calculate();
    }

    public class FacGrain : Grain, IFacGrain
    {
        public long answer { get; private set; }

        public async override Task OnActivateAsync()
        {
            Console.WriteLine($"  activating {this.GetPrimaryKeyLong()}");
        }

        public async Task<long> Calculate()
        {
            var input = this.GetPrimaryKeyLong();
            if (input <= 1) return 1;
            if (answer > 0) return answer;

            var nextGrain = this.GrainFactory.GetGrain<IFacGrain>(input - 1);
            var nextValue = await nextGrain.Calculate();

            this.answer = nextValue * input;
            return answer;
        }
    }

    class Program
    {

        private static async Task Test(IClusterClient client)
        {
            while(true)
            {
                var input = int.Parse(Console.ReadLine());

                var grain = client.GetGrain<IFacGrain>(input);
                var result = await grain.Calculate();

                Console.WriteLine($"  {input}! = {result}");

            }


        }

        #region boilerplate

        static void Main()
        {
            var host = StartSilo().Result;
            var client = StartClientWithRetries().Result;

            Test(client).Wait();

            Console.WriteLine("Press Enter to terminate...");
            Console.ReadLine();

            host.StopAsync().Wait();
        }

        private static async Task<ISiloHost> StartSilo()
        {
            Console.WriteLine("Starting silo");
            // define the cluster configuration
            var config = ClusterConfiguration.LocalhostPrimarySilo();
            config.AddMemoryStorageProvider();
            config.RegisterDashboard();

            var host = new SiloHostBuilder()
                .UseConfiguration(config)
                .UseDashboard(x => {
                    x.HostSelf = true;
                })
                .AddApplicationPartsFromReferences(typeof(Program).Assembly)
                .ConfigureLogging(logging => {
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Warning);
                })
                .Build();

            await host.StartAsync();
            Console.WriteLine("Silo successfully started");
            return host;
        }

        private static async Task<IClusterClient> StartClientWithRetries(int initializeAttemptsBeforeFailing = 5)
        {
            int attempt = 0;
            IClusterClient client;
            while (true)
            {
                try
                {
                    var config = ClientConfiguration.LocalhostSilo();
                    client = new ClientBuilder()
                        .UseConfiguration(config)
                        .AddApplicationPartsFromReferences(typeof(Program).Assembly)
                        .Build();

                    await client.Connect();
                    Console.WriteLine("Client successfully connect to silo host");
                    break;
                }
                catch (SiloUnavailableException)
                {
                    attempt++;
                    Console.WriteLine($"Attempt {attempt} of {initializeAttemptsBeforeFailing} failed to initialize the Orleans client.");
                    if (attempt > initializeAttemptsBeforeFailing)
                    {
                        throw;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(4));
                }
            }

            return client;
        }

    }
#endregion
}
