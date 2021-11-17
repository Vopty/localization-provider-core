// Copyright (c) Valdis Iljuconoks. All rights reserved.
// Licensed under Apache-2.0. See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DbLocalizationProvider.AspNetCore.Cache;
using DbLocalizationProvider.AspNetCore.DataAnnotations;
using DbLocalizationProvider.AspNetCore.Queries;
using DbLocalizationProvider.Cache;
using DbLocalizationProvider.Internal;
using DbLocalizationProvider.Queries;
using DbLocalizationProvider.Refactoring;
using DbLocalizationProvider.Sync;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ILogger = DbLocalizationProvider.Logging.ILogger;

namespace DbLocalizationProvider.AspNetCore
{
    /// <summary>
    /// Extension for adding localization provider services to the collection
    /// </summary>
    public static class IServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the database localization provider.
        /// </summary>
        /// <param name="services">The services.</param>
        /// <param name="setup">The setup.</param>
        /// <returns></returns>
        public static IServiceCollection AddDbLocalizationProvider(
            this IServiceCollection services,
            Action<ConfigurationContext> setup = null)
        {
            var ctx = new ConfigurationContext();
            var factory = ctx.TypeFactory;

            // setup default implementations
            factory.ForQuery<GetAllResources.Query>().DecorateWith<CachedGetAllResourcesHandler>();
            factory.ForQuery<DetermineDefaultCulture.Query>().SetHandler<DetermineDefaultCulture.Handler>();
            factory.ForCommand<ClearCache.Command>().SetHandler<ClearCacheHandler>();

            // set to default in-memory provider
            // only if we have IMemoryCache service registered
            if (services.FirstOrDefault(descr => descr.ServiceType == typeof(IMemoryCache)) != null)
            {
                services.AddSingleton<ICacheManager, InMemoryCacheManager>();
            }

            // run custom configuration setup (if any)
            setup?.Invoke(ctx);

            // adding mvc localization stuff
            var scanState = new ScanState();
            var keyBuilder = new ResourceKeyBuilder(scanState);
            var oldKeyBuilder = new OldResourceKeyBuilder(keyBuilder);
            var expressionHelper = new ExpressionHelper(keyBuilder);
            var queryExecutor = new QueryExecutor(ctx);
            var commandExecutor = new CommandExecutor(ctx);
            var translationBuilder = new DiscoveredTranslationBuilder(queryExecutor);
            var localizationProvider = new LocalizationProvider(keyBuilder, expressionHelper, ctx.FallbackList, queryExecutor);

            services.AddSingleton(p =>
            {
                // TODO: looks like a bit hackish
                ctx.TypeFactory.SetServiceFactory(p.GetService);
                return ctx;
            });

            services.AddSingleton(_ => ctx.TypeFactory);

            // add all registered handlers to DI (in order to use service factory callback from DI lib)
            foreach (var handler in ctx.TypeFactory.GetAllHandlers())
            {
                services.AddTransient(handler);
            }

            // add all registered handlers to DI (in order to use service factory callback from DI lib)
            foreach (var (service, implementation) in ctx.TypeFactory.GetAllTransientServiceMappings())
            {
                services.AddTransient(service, implementation);
            }

            services.AddSingleton(scanState);
            services.AddSingleton(keyBuilder);
            services.AddSingleton(expressionHelper);
            services.AddSingleton(queryExecutor);
            services.AddSingleton<IQueryExecutor>(queryExecutor);
            services.AddSingleton(commandExecutor);
            services.AddSingleton(translationBuilder);
            services.AddSingleton<ICommandExecutor>(commandExecutor);
            services.AddSingleton<ILogger>(p => new LoggerAdapter(p.GetService<ILogger<LoggerAdapter>>()));

            services.AddSingleton(new TypeDiscoveryHelper(new List<IResourceTypeScanner>
            {
                new LocalizedModelTypeScanner(keyBuilder, oldKeyBuilder, scanState, ctx, translationBuilder),
                new LocalizedResourceTypeScanner(keyBuilder, oldKeyBuilder, scanState, ctx, translationBuilder),
                new LocalizedEnumTypeScanner(keyBuilder, translationBuilder),
                new LocalizedForeignResourceTypeScanner(keyBuilder, oldKeyBuilder, scanState, ctx, translationBuilder)
            }, ctx));

            services.AddSingleton(localizationProvider);
            services.AddSingleton<ILocalizationProvider>(localizationProvider);
            services.AddTransient<ISynchronizer, Synchronizer>();
            services.AddTransient<Synchronizer>();

            services.AddSingleton<DbStringLocalizerFactory>();
            services.AddSingleton<IStringLocalizerFactory>(p => p.GetRequiredService<DbStringLocalizerFactory>());
            services.AddSingleton<DbHtmlLocalizerFactory>();
            services.AddSingleton<IHtmlLocalizerFactory>(p => p.GetRequiredService<DbHtmlLocalizerFactory>());
            services.AddTransient<IViewLocalizer, DbViewLocalizer>();
            services.AddTransient(typeof(IHtmlLocalizer<>), typeof(DbHtmlLocalizer<>));

            // we need to check whether invariant fallback is correctly configured
            if (ctx.EnableInvariantCultureFallback && !ctx.FallbackLanguages.Contains(CultureInfo.InvariantCulture))
            {
                ctx.FallbackLanguages.Then(CultureInfo.InvariantCulture);
            }

            // setup model metadata providers
            if (ctx.ModelMetadataProviders.ReplaceProviders)
            {
                services.Configure<MvcOptions>(
                    opt =>
                    {
                        opt.ModelMetadataDetailsProviders.Add(
                            new LocalizedDisplayMetadataProvider(
                                new ModelMetadataLocalizationHelper(localizationProvider, keyBuilder, ctx), ctx));
                    });

                services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<MvcViewOptions>, ConfigureMvcViews>());
            }

            services.AddHttpContextAccessor();

            return services;
        }
    }
}
