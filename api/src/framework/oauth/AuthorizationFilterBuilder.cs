﻿namespace Framework.OAuth
{
    using System;
    using System.Net.Http;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Caching.Distributed;
    using Microsoft.Extensions.DependencyInjection;
    using Framework.Configuration;
    using Framework.Utilities;

    /// <summary>
    /// Helper methods for setting up authentication
    /// </summary>
    public sealed class AuthorizationFilterBuilder<TClaims> where TClaims : CoreApiClaims, new()
    {
        // Our OAuth configuration
        private readonly OAuthConfiguration configuration;

        // The type of custom claims provider
        private Type customClaimsProviderType;

        // The ASP.Net Core services we will configure
        private IServiceCollection services;

        // An object to support HTTP debugging
        private Func<HttpClientHandler> httpProxyFactory;

        /// <summary>
        /// Create our builder and receive our options
        /// </summary>
        /// <param name="options">The options</param>
        public AuthorizationFilterBuilder(AuthorizationFilterOptions options)
        {
            this.configuration = options.OAuthConfiguration;
        }

        /// <summary>
        /// Provide the type of custom claims provider
        /// </summary>
        /// <typeparam name="TProvider">The type of provider</typeparam>
        /// <returns>The builder</returns>
        public AuthorizationFilterBuilder<TClaims> WithCustomClaimsProvider<TProvider>()
            where TProvider : CustomClaimsProvider<TClaims>
        {
            this.customClaimsProviderType = typeof(TProvider);
            return this;
        }

        /// <summary>
        /// Store an ASP.Net core services reference which we will update later
        /// </summary>
        /// <param name="services">The services</param>
        /// <returns>The builder</returns>
        public AuthorizationFilterBuilder<TClaims> WithServices(IServiceCollection services)
        {
            this.services = services;
            return this;
        }

        /// <summary>
        /// Store an object to manage HTTP debugging
        /// </summary>
        /// <param name="httpProxyFactory">A factory object to create custom HTTP handlers</param>
        /// <returns>The builder</returns>
        public AuthorizationFilterBuilder<TClaims> WithHttpDebugging(bool enabled, string url)
        {
            this.httpProxyFactory = () => new ProxyHttpHandler(enabled, url);
            return this;
        }

        /// <summary>
        /// Do the work of validating the configuration and finalizing configuration
        /// </summary>
        public void Build()
        {
            // Check prerequisites and get the Microsoft cache
            IDistributedCache cache = null;
            using (var provider = services.BuildServiceProvider())
            {
                cache = VerifyPrerequisite<IDistributedCache>(provider);
                VerifyPrerequisite<IHttpContextAccessor>(provider);
            }

            // Load issuer metadata during startup
            var issuerMetadata = new IssuerMetadata(this.configuration, this.httpProxyFactory);
            issuerMetadata.Load().Wait();

            // Create the thread safe claims cache
            var claimsCache = new ClaimsCache<TClaims>(cache, this.configuration);

            // Create a default custom claims provider if needed
            if (this.customClaimsProviderType == null)
            {
                this.customClaimsProviderType = typeof(CustomClaimsProvider<TClaims>);
            }

            // Create a default proxy object if needed
            if (this.httpProxyFactory == null)
            {
                this.httpProxyFactory = () => new ProxyHttpHandler(false, null);
            }

            // Update dependency injection
            this.RegisterAuthenticationDependencies(issuerMetadata, claimsCache);
        }

        /// <summary>
        /// Verify and return a prerequisite service
        /// </summary>
        /// <typeparam name="T">The type of the service</typeparam>
        /// <param name="provider">The provider</param>
        /// <returns>The service</returns>
        private T VerifyPrerequisite<T>(ServiceProvider provider) where T: class
        {
            var result = provider.GetService<T>();
            if(result == null)
            {
                throw new InvalidOperationException($"The prerequisite service {typeof(T).Name} has not been configured");
            }

            return result;
        }

        /// <summary>
        /// Register framework dependencies
        /// </summary>
        /// <param name="issuerMetadata">The singleton issuer metadata</param>
        /// <param name="cache">The singleton claims cache</param>
        private void RegisterAuthenticationDependencies(IssuerMetadata issuerMetadata, ClaimsCache<TClaims> cache)
        {
            // Register singletons
            this.services.AddSingleton(this.configuration);
            this.services.AddSingleton(issuerMetadata);
            this.services.AddSingleton(cache);
            this.services.AddSingleton(this.httpProxyFactory);

            // Register OAuth per request dependencies
            this.services.AddScoped<ClaimsMiddleware<TClaims>>();
            this.services.AddScoped<Authenticator>();
            this.services.AddScoped(typeof(CustomClaimsProvider<TClaims>), this.customClaimsProviderType);

            // The claims middleware populates the TClaims object and sets it against the HTTP context's claims principal
            // When controller operations execute they access the HTTP context and extract the claims
            this.services.AddScoped(
                ctx =>
                {
                    var claims = new TClaims();
                    claims.ReadFromPrincipal(ctx.GetService<IHttpContextAccessor>().HttpContext.User);
                    return claims;
                });
        }
    }
}
