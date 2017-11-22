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
        private long solution;


        public override Task OnActivateAsync()
        {
            Console.WriteLine($"Activating {this.GetPrimaryKeyLong()}");
            return Task.CompletedTask;
        }

        public async Task<long> Calculate()
        {
            var value = this.GetPrimaryKeyLong();
            if (value <= 1) return 1;
            if (solution > 0) return solution;

            var valueToMultiple = await this
                .GrainFactory
                .GetGrain<IFacGrain>(value - 1)
                .Calculate();
            
            this.solution = valueToMultiple * value;

            return solution;
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
                var answer = await grain.Calculate();
                Console.WriteLine($"answer {input}! = {answer}");
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
