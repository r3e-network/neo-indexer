// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Hosting.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO.Compression;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Neo.Plugins.RpcServer
{
    public partial class RpcServer
    {
        public void StartRpcServer()
        {
            host = new WebHostBuilder().UseKestrel(options => options.Listen(settings.BindAddress, settings.Port, listenOptions =>
            {
                // Default value is 5Mb
                options.Limits.MaxRequestBodySize = settings.MaxRequestBodySize;
                options.Limits.MaxRequestLineSize = Math.Min(settings.MaxRequestBodySize, options.Limits.MaxRequestLineSize);
                // Default value is 40
                options.Limits.MaxConcurrentConnections = settings.MaxConcurrentConnections;

                // Default value is 1 minutes
                options.Limits.KeepAliveTimeout = settings.KeepAliveTimeout == -1 ?
                    TimeSpan.MaxValue :
                    TimeSpan.FromSeconds(settings.KeepAliveTimeout);

                // Default value is 15 seconds
                options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(settings.RequestHeadersTimeout);

                if (string.IsNullOrEmpty(settings.SslCert)) return;
                listenOptions.UseHttps(settings.SslCert, settings.SslCertPassword, httpsConnectionAdapterOptions =>
                {
                    if (settings.TrustedAuthorities is null || settings.TrustedAuthorities.Length == 0)
                        return;
                    httpsConnectionAdapterOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                    httpsConnectionAdapterOptions.ClientCertificateValidation = (cert, chain, err) =>
                    {
                        if (err != SslPolicyErrors.None)
                            return false;
                        X509Certificate2 authority = chain.ChainElements[^1].Certificate;
                        return settings.TrustedAuthorities.Contains(authority.Thumbprint);
                    };
                });
            }))
            .Configure(app =>
            {
                if (settings.EnableCors)
                    app.UseCors("All");

                app.UseResponseCompression();
                app.Run(ProcessAsync);
            })
            .ConfigureServices(services =>
            {
                if (settings.EnableCors)
                {
                    if (settings.AllowOrigins.Length == 0)
                        services.AddCors(options =>
                        {
                            options.AddPolicy("All", policy =>
                            {
                                policy.AllowAnyOrigin()
                                .WithHeaders("Content-Type")
                                .WithMethods("GET", "POST");
                                // The CORS specification states that setting origins to "*" (all origins)
                                // is invalid if the Access-Control-Allow-Credentials header is present.
                            });
                        });
                    else
                        services.AddCors(options =>
                        {
                            options.AddPolicy("All", policy =>
                            {
                                policy.WithOrigins(settings.AllowOrigins)
                                .WithHeaders("Content-Type")
                                .AllowCredentials()
                                .WithMethods("GET", "POST");
                            });
                        });
                }

                services.AddResponseCompression(options =>
                {
                    // options.EnableForHttps = false;
                    options.Providers.Add<GzipCompressionProvider>();
                    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Append("application/json");
                });

                services.Configure<GzipCompressionProviderOptions>(options =>
                {
                    options.Level = CompressionLevel.Fastest;
                });
            })
            .Build();

            host.Start();
        }
    }
}

