using FiguraServer.Server.Auth;
using FiguraServer.Server.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace FiguraServer
{
    public class Startup
    {
        private static int currentConnections = 0;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            #region SQL middleware
            if (!System.IO.File.Exists("sqlconnection.txt"))
            {
                throw new Exception("No 'sqlconnection.txt' file found, unable to initialize MySQL connections. Please create an sqlconnection.txt file containing the mysql connection string.");
            }

            string connection = System.IO.File.ReadAllText("sqlconnection.txt");
            #endregion

            services.Configure<FiguraAuthServer.Config>(Configuration.GetSection("FiguraAuthServer"));
            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "FiguraServer", Version = "v1" });
            });

            services.AddHostedService<FiguraAuthServer>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "FiguraServer v1"));
            }

            //app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseWebSockets();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.Use(async (context, next) =>
            {

                Logger.LogMessage("TEST! " + context.Request.Path + "|" + context.WebSockets.IsWebSocketRequest);

                if (context.Request.Path == "/connect/" && context.WebSockets.IsWebSocketRequest)
                {

                    currentConnections++;
                    try
                    {
                        Logger.LogMessage("Accepting websocket. Total connections : " + currentConnections);
                        using (WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync())
                        {
                            using (var wsc = new WebSocketConnection(webSocket))
                            {
                                await wsc.Run();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.LogMessage(e);
                    }
                    currentConnections--;

                    Logger.LogMessage("Websocket disposed.");
                }
            });
        }
    }
}
