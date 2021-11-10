using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using StackExchange.Redis;
using WebApplication.Middleware;

namespace WebApplication
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton((serviceProvider) => new MongoClient(serviceProvider.GetRequiredService<IConfiguration>().GetValue<string>("Mongo:ConnectionString")));
            services.AddSingleton<IConnectionMultiplexer>((serviceProvider) =>
            {
                var options = ConfigurationOptions.Parse(serviceProvider.GetRequiredService<IConfiguration>().GetValue<string>("Redis:ConnectionString"));
                options.AllowAdmin = true;
                options.Password = "test";
                options.CommandMap = CommandMap.Default;
                return ConnectionMultiplexer.Connect(options);
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseMiddleware<SaveRequestData>();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Hello World!");
                });
            });
        }
    }
}
