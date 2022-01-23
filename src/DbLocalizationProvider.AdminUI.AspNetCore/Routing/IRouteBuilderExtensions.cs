// Copyright (c) Valdis Iljuconoks. All rights reserved.
// Licensed under Apache-2.0. See the LICENSE file in the project root for more information

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DbLocalizationProvider.AdminUI.AspNetCore.Routing
{
    /// <summary>
    /// Analyzer is happy now
    /// </summary>
    public static class IRouteBuilderExtensions
    {
        /// <summary>
        /// If you are using ASP.NET Mvc Routing -> please use this method to map AdminUI routes
        /// </summary>
        /// <param name="builder">Mvc route  builder</param>
        /// <returns><see cref="IRouteBuilder"/> to support API call chaining</returns>
        public static IRouteBuilder MapDbLocalizationAdminUI(this IRouteBuilder builder)
        {
            var context = builder.ServiceProvider.GetService<UiConfigurationContext>();
            builder.MapRoute("AdminUI API Route", context.RootUrl + "/api/{controller=Service}/{action=Index}");

            return builder;
        }
    }
}
