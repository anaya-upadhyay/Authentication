﻿using AutoMapper;
using IdentityServer.Data;
using IdentityServer4.EntityFramework.DbContexts;
using IdentityServer4.EntityFramework.Mappers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace IdentityServer
{
    public static class StartupExtensions
    {
        public static IConfiguration CreateConfiguration(this IServiceCollection services)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddJsonFile($"appsettings.{Utils.Env}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            services.AddSingleton(config);

            return config;
        }

        public static void ConfigureAspNetIdentity(this IServiceCollection services, string connectionString)
        {
            services
                .AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

            services
                .AddIdentity<IdentityUser, IdentityRole>(options =>
                {
                    options.Password.RequireDigit = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireNonAlphanumeric = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequiredLength = 8;
                    options.Password.RequiredUniqueChars = 4;

                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                    options.Lockout.MaxFailedAccessAttempts = 5;
                    options.Lockout.AllowedForNewUsers = true;

                    options.User.AllowedUserNameCharacters =
                        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._!@#$^&| ";
                    options.User.RequireUniqueEmail = true;
                })
                .AddEntityFrameworkStores<AppDbContext>()
                .AddDefaultTokenProviders();

            services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.Name = "IS406_IdentityServer.Cookie";
                options.LoginPath = "/Auth/Login";
                options.LogoutPath = "/Auth/Logout";
            });
        }

        public static void ConfigureIdentityServer(this IServiceCollection services, string connectionString)
        {
            var migrationsAssembly = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;

            services
                .AddIdentityServer(
                    options =>
                    {
                        options.Events.RaiseErrorEvents = true;
                        options.Events.RaiseInformationEvents = true;
                        options.Events.RaiseFailureEvents = true;
                        options.Events.RaiseSuccessEvents = true;
                    })
                .AddDeveloperSigningCredential() // used for signing tokens (not to be used in prod)
                .AddConfigurationStore(
                    options =>
                    {
                        options.DefaultSchema = "Identity";
                        options.ConfigureDbContext = builder =>
                            builder.UseSqlServer(
                                connectionString,
                                sql => sql.MigrationsAssembly(migrationsAssembly));
                    })
                .AddOperationalStore(
                    options =>
                    {
                        options.ConfigureDbContext = builder =>
                            builder.UseSqlServer(
                                connectionString,
                                sql => sql.MigrationsAssembly(migrationsAssembly));

                        options.DefaultSchema = "Identity";
                        options.EnableTokenCleanup = true;
                        options.TokenCleanupInterval = 30;
                    })
                .AddAspNetIdentity<IdentityUser>();
        }

        public static void ConfigureServices(this IServiceCollection services)
        {
            services.AddAutoMapper(typeof(Startup));

            // The definition UserManager<IdentityUser> and SignInManager<IdentityUser>
            // require a Scope resolve from Dependency Injection.

            // If you were to go with custom classes akin to Services that have
            // UserManager (et cetera) as dependencies, the service can only be added
            // properly with AddScoped. If your service was designed as a singleton, 
            // this could effect performance significantly. You may need to find a work
            // around in such a case (i.e. using custom AspNet classes.).

            // Example.) Service being added with AddSingleton will cause an InvalidOperationException.
            //services.AddSingleton<IUserService>(s =>
            //{
            //    return new UserService(
            //        s.GetRequiredService<UserManager<IdentityUser>>(),
            //        s.GetRequiredService<SignInManager<IdentityUser>>());
            //});

            // So we will be using UserManager and SignInManager directly into the
            // Controllers themselves.
        }

        public static void InitializeDatabase(this IApplicationBuilder app)
        {
            Log.Logger.Information("Initializing the database...");

            using var serviceScope = app
                .ApplicationServices
                .GetService<IServiceScopeFactory>()
                .CreateScope();

            serviceScope
                .ServiceProvider
                .GetRequiredService<AppDbContext>()
                .Database
                .Migrate();

            serviceScope
                .ServiceProvider
                .GetRequiredService<PersistedGrantDbContext>()
                .Database
                .Migrate();

            var context = serviceScope
                .ServiceProvider
                .GetRequiredService<ConfigurationDbContext>();

            context.Database.Migrate();

            if (!context.Clients.Any())
            {
                foreach (var client in Resources.GetClients())
                {
                    context.Clients.Add(client.ToEntity());
                }
            }

            if (!context.IdentityResources.Any())
            {
                foreach (var resource in Resources.GetIdentityResources())
                {
                    context.IdentityResources.Add(resource.ToEntity());
                }
            }

            if (!context.ApiResources.Any())
            {
                foreach (var resource in Resources.GetApis())
                {
                    context.ApiResources.Add(resource.ToEntity());
                }
            }

            context.SaveChanges();
        }

        public static async Task InitializeDatabaseAsync(this IApplicationBuilder app)
        {
            Log.Logger.Information("Initializing the database...");

            using var serviceScope = app
                .ApplicationServices
                .GetService<IServiceScopeFactory>()
                .CreateScope();

            await serviceScope
                .ServiceProvider
                .GetRequiredService<AppDbContext>()
                .Database
                .MigrateAsync();

            await serviceScope
                .ServiceProvider
                .GetRequiredService<PersistedGrantDbContext>()
                .Database
                .MigrateAsync();

            var context = serviceScope
                .ServiceProvider
                .GetRequiredService<ConfigurationDbContext>();

            await context.Database.MigrateAsync();

            if (!context.Clients.Any())
            {
                foreach (var client in Resources.GetClients())
                {
                    context.Clients.Add(client.ToEntity());
                }
            }

            if (!context.IdentityResources.Any())
            {
                foreach (var resource in Resources.GetIdentityResources())
                {
                    context.IdentityResources.Add(resource.ToEntity());
                }
            }

            if (!context.ApiResources.Any())
            {
                foreach (var resource in Resources.GetApis())
                {
                    context.ApiResources.Add(resource.ToEntity());
                }
            }

            await context.SaveChangesAsync();
        }
    }
}
