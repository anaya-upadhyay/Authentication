﻿using IdentityModel;
using IdentityServer4.Models;
using System.Collections.Generic;

namespace IdentityServer
{
    public static class Resources
    {
        public static IEnumerable<IdentityResource> GetIdentityResources() =>
            new List<IdentityResource>
            {
                new IdentityResources.OpenId(), // required scopes for OpenID 2.0
                new IdentityResources.Profile(),
                new IdentityResources.Email()
            };

        public static IEnumerable<ApiResource> GetApis() =>
            new List<ApiResource>
            {
                new ApiResource("TestApi"),
                new ApiResource("TestApiClient")
            };

        public static IEnumerable<Client> GetClients() =>
            new List<Client>
            {
                new Client
                {
                    ClientId = "MvcClient",
                    ClientSecrets = new List<Secret>
                    {
                        new Secret("MvcClientSecret".ToSha256())
                    },
                    AllowedGrantTypes = GrantTypes.Code, // Different GrantType

                    RedirectUris = new List<string>{ "https://localhost:5011/signin-oidc" },
                    PostLogoutRedirectUris = new List<string>{ "https://localhost:5011/Home/Index" },

                    AllowedScopes = new List<string>
                    {
                        IdentityServer4.IdentityServerConstants.StandardScopes.OpenId,
                        IdentityServer4.IdentityServerConstants.StandardScopes.Profile,
                    },

                    RequireConsent = false, // Only temporary till we are ready to handle user consent.
                },
                new Client
                {
                    ClientId = "PostmanClient",
                    ClientSecrets = new List<Secret>
                    {
                        new Secret("PostmanSecret".ToSha256())
                    },
                    AllowedGrantTypes = GrantTypes.Code, // Different GrantType

                    RedirectUris = new List<string>{ "https://localhost:5011/signin-oidc" },
                    PostLogoutRedirectUris = new List<string>{ "https://localhost:5011/Home/Index" },

                    AllowedScopes = new List<string>
                    {
                        IdentityServer4.IdentityServerConstants.StandardScopes.OpenId,
                        IdentityServer4.IdentityServerConstants.StandardScopes.Profile,
                    },
                    AllowAccessTokensViaBrowser = true,
                    RequireConsent = false, // Only temporary till we are ready to handle user consent.
                },
            };

    }
}
