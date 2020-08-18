using System.Reflection;
using Darker.Builder;
using Darker.RequestLogging;
using Darker.SimpleInjector;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Paramore.Brighter;
using Serilog;
using TodoBackend.Api.Infrastructure;
using TodoBackend.Core.Ports.Commands.Handlers;
using TodoBackend.Core.Ports.Queries.Handlers;
using SimpleInjector;
using SimpleInjector.Integration.AspNetCore;
using SimpleInjector.Integration.AspNetCore.Mvc;
using SimpleInjector.Lifestyles;
using TodoBackend.Api.Data;
using TodoBackend.Core.Domain;
using Microsoft.Extensions.Hosting;

namespace TodoBackend.Api
{
    public class Startup
    {
        private readonly Container _container;
        private readonly IConfigurationRoot _configuration;

        readonly string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

        public Startup(IWebHostEnvironment env)
        {
            _container = new Container();
            _configuration = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appSettings.json", optional: false)
                .AddJsonFile($"appSettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.LiterateConsole()
                .Enrich.WithMachineName()
                .CreateLogger();

            services.AddCors(options =>
        {
            options.AddPolicy(name: MyAllowSpecificOrigins,
                              builder =>
                              {
                                  builder.WithOrigins("http://localhost:8989")
                                                    .AllowAnyHeader()
                                                    .AllowAnyMethod();
                              });
        });

            services.AddMvc()
                .AddNewtonsoftJson(opt =>
                {
                    opt.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                    opt.SerializerSettings.Converters.Add(new StringEnumConverter());
                    opt.SerializerSettings.Formatting = Formatting.Indented;
                    opt.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
                });

                services.AddControllersWithViews();
                services.AddRazorPages();


            services.AddSingleton<IControllerActivator>(new SimpleInjectorControllerActivator(_container));
            services.AddSingleton<IViewComponentActivator>(new SimpleInjectorViewComponentActivator(_container));
            services.UseSimpleInjectorAspNetRequestScoping(_container);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddSerilog(Log.Logger);

            InitializeContainer(app, loggerFactory);

            _container.Verify();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            
            app.UseRouting();
            app.UseCors(MyAllowSpecificOrigins);

            app.UseEndpoints(endpoints => {
                endpoints.MapControllers();
            });

            EnsureDatabaseCreated();
        }

        // hack
        private void EnsureDatabaseCreated()
        {
            if (_configuration["DataStore"] != "SqlServer")
                return;

            var dbopts = new DbContextOptionsBuilder<TodoContext>()
                .UseSqlServer(_configuration.GetConnectionString("SqlServer"))
                .Options;

            using (var ctx = new TodoContext(dbopts))
            {
                ctx.Database.EnsureCreated();
            }
        }

        private void InitializeContainer(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            _container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();

            _container.RegisterMvcControllers(app);
            _container.RegisterSingleton(Log.Logger);

            _container.RegisterSingleton<IConfiguration>(_configuration);

            if (_configuration["DataStore"] == "SqlServer")
            {
                _container.RegisterSingleton(
                    new DbContextOptionsBuilder<TodoContext>()
                        .UseSqlServer(_configuration.GetConnectionString("SqlServer"))
                        .UseLoggerFactory(loggerFactory)
                        .Options);

                _container.Register<IUnitOfWorkManager, EfUnitOfWorkManager>();
            }
            else
            {
                _container.Register<IUnitOfWorkManager, InMemoryUnitOfWorkManager>();
            }

            ConfigureBrighter();
            ConfigureDarker();
        }

        private void ConfigureBrighter()
        {
            var config = new SimpleInjectorHandlerConfig(_container);
            config.RegisterSubscribersFromAssembly(typeof(CreateTodoHandler).GetTypeInfo().Assembly);
            config.RegisterDefaultHandlers();

            var commandProcessor = CommandProcessorBuilder.With()
                .Handlers(config.HandlerConfiguration)
                .DefaultPolicy()
                .NoTaskQueues()
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .Build();

            _container.RegisterSingleton<IAmACommandProcessor>(commandProcessor);
        }

        private void ConfigureDarker()
        {
            var queryProcessor = QueryProcessorBuilder.With()
                .SimpleInjectorHandlers(_container, opts => opts
                    .WithQueriesAndHandlersFromAssembly(typeof(GetTodoHandler).GetTypeInfo().Assembly))
                .InMemoryQueryContextFactory()
                .JsonRequestLogging()
                .Build();

            _container.RegisterSingleton(queryProcessor);
        }
    }
}