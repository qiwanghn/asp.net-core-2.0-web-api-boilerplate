﻿using System.IO;
using System.Reflection;
using AutoMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CoreApi.DataContext.Core;
using CoreApi.Repositories.Angular;
using CoreApi.Repositories.Core;
using CoreApi.Web.MyConfigurations;
using Infrastructure.Features.Data;
using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using SharedSettings.Settings;
using Swashbuckle.AspNetCore.Swagger;

namespace CoreApi.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
        
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<CoreContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));

            services.AddMvc(options =>
            {
                options.OutputFormatters.Remove(new XmlDataContractSerializerOutputFormatter());
            });
            
            services.AddAutoMapper();

            services.AddScoped<IUnitOfWork, CoreContext>();
            services.AddScoped(typeof(ICoreService<>), typeof(CoreService<>));
            services.AddScoped<IUploadedFileRepository, UploadedFileRepository>();
            services.AddScoped<IClientRepository, ClientRepository>();
            
            var physicalProvider = new PhysicalFileProvider(Directory.GetCurrentDirectory());
            var embeddedProvider = new EmbeddedFileProvider(Assembly.GetEntryAssembly());
            var compositeProvider = new CompositeFileProvider(physicalProvider, embeddedProvider);
            services.AddSingleton<IFileProvider>(compositeProvider);
            
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info { Title = CoreApiSettings.ApiResource.DisplayName, Version = "v1" });
            });
            
            services.AddMvcCore()
                .AddAuthorization()
                .AddJsonFormatters();

            services.AddAuthentication("Bearer")
                .AddIdentityServerAuthentication(options =>
                {
                    options.Authority = AuthorizationServerSettings.AuthorizationServerBase;
                    options.RequireHttpsMetadata = false;

                    options.ApiName = CoreApiSettings.ApiResource.Name;
                });

            services.AddCors(options =>
            {
                options.AddPolicy(CoreApiSettings.CorsPolicyName, policy =>
                {
                    policy.WithOrigins(CoreApiSettings.CorsOrigin)
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });
        }
        
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, CoreContext coreContext)
        {
            app.UseCors(CoreApiSettings.CorsPolicyName);

            app.UseStaticFiles();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler();
            }

            app.UseStatusCodePages();
            
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Core APIs V1");
            });
            
            // Config Serilog
            app.ConfigureSerilog(Configuration);
            
            // Identity Server
            app.UseAuthentication();

            app.UseMvc();

        }
    }
}
