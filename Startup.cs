// Generated with EchoBot .NET Template version v4.22.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Bot.Builder.Azure;


namespace EchoBot
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpClient().AddControllers().AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.MaxDepth = HttpHelper.BotMessageSerializerSettings.MaxDepth;
            });

            // Create the Bot Framework Authentication to be used with the Bot Adapter.
            services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();

            // Create the Bot Adapter with error handling enabled.
            services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();

            // Create the bot as a transient. In this case the ASP Controller is expecting an IBot.
            services.AddTransient<IBot, Bots.EchoBot>();

            var storage = new MemoryStorage();
            var conversationState = new ConversationState(storage);
            services.AddSingleton(conversationState);
            var userState = new UserState(storage);
            services.AddSingleton(userState);

            // Use partitioned CosmosDB for storage, instead of in-memory storage.
            services.AddSingleton<IStorage>(
                new CosmosDbPartitionedStorage(
                    new CosmosDbPartitionedStorageOptions
                    {
                        CosmosDbEndpoint = Configuration.GetValue<string>("CosmosDbEndpoint"),
                        AuthKey = Configuration.GetValue<string>("CosmosDbAuthKey"),
                        DatabaseId = Configuration.GetValue<string>("CosmosDbDatabaseId"),
                        ContainerId = Configuration.GetValue<string>("CosmosDbContainerId"),
                        CompatibilityMode = false,
                    }));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseDefaultFiles()
                .UseStaticFiles()
                .UseWebSockets()
                .UseRouting()
                .UseAuthorization()
                .UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });

            // app.UseHttpsRedirection();
        }
    }
}
