﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Context;
using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IdentityServer.Middleware
{
    public static class SerilogHttpContextExtensions
    {
        public static IApplicationBuilder UseSerilogHttpContextLogger(
            this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SerilogHttpContextLogger>();
        }
    }

    public class SerilogHttpContextLogger
    {
        private readonly RequestDelegate _next;
        private ArrayPool<byte> SharedBytePool { get; } = ArrayPool<byte>.Shared;
        private readonly bool LogRequestBody;
        private readonly int LogRequestBodyMaxSize;
        private readonly bool LogResponseBody;
        private readonly int LogResponseBodyMaxSize;

        #region Constants 

        private const string UserNameProperty = "UserName";
        private const string AnonUserName = "*";
        private const string IPProperty = "IP";
        private const string UserAgentProperty = "UserAgent";
        private const string UserAgentField = "User-Agent";
        private const string RequestHeadersProperty = "RequestHeaders";
        private const string RequestBodyProperty = "RequestBody";
        private const string ResponseBodyProperty = "ResponseBody";

        private const string RequestTemplate = "Middleware Request: {RequestMethod} {RequestPath}";
        private const string RequestTooLargeTemplate = "Middleware Request: {RequestMethod} {RequestPath} (Body Skipped)";

        private const string ResponseTemplate = "Middleware Response: {RequestMethod} {RequestPath} {StatusCode}";
        private const string ResponseTooLargeTemplate = "Middleware Response: {RequestMethod} {RequestPath} {StatusCode} (Body Skipped)";

        #endregion

        public SerilogHttpContextLogger(RequestDelegate next, IConfiguration configuration)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            LogRequestBody = configuration.GetSection("Application:LoggingMiddleware").GetValue<bool>("LogRequestBody");
            LogResponseBody = configuration.GetSection("Application:LoggingMiddleware").GetValue<bool>("LogResponseBody");
            LogRequestBodyMaxSize = configuration.GetSection("Application:LoggingMiddleware").GetValue<int>("LogRequestBodyMaxSize");
            LogResponseBodyMaxSize = configuration.GetSection("Application:LoggingMiddleware").GetValue<int>("LogResponseBodyMaxSize");
        }

        public async Task Invoke(HttpContext httpContext)
        {
            if (httpContext == null) throw new ArgumentNullException(nameof(httpContext));

            var username = httpContext.User.Identity.IsAuthenticated ? httpContext.User.Identity.Name : AnonUserName;
            LogContext.PushProperty(UserNameProperty, username);
            LogContext.PushProperty(IPProperty, httpContext.Connection.RemoteIpAddress.ToString());
            LogContext.PushProperty(UserAgentProperty, httpContext.Request.Headers[UserAgentField]);

            if (LogRequestBody) { await LogRequestBodyAsync(httpContext); }

            await _next(httpContext);

            if (LogResponseBody) { await LogResponseBodyAsync(httpContext); }
        }

        private async Task LogRequestBodyAsync(HttpContext httpContext)
        {
            if (httpContext.Request.ContentLength.HasValue
                && httpContext.Request.ContentLength.Value > 0
                && httpContext.Request.ContentLength.Value < LogRequestBodyMaxSize)
            {
                httpContext.Request.EnableBuffering();

                var length = Convert.ToInt32(httpContext.Request.ContentLength.Value);
                var buffer = SharedBytePool.Rent(length);

                await httpContext.Request.Body.ReadAsync(buffer, 0, length);

                httpContext.Request.Body.Seek(0, SeekOrigin.Begin);

                Log
                    .ForContext(
                        RequestHeadersProperty,
                        httpContext.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                        destructureObjects: true)
                    .ForContext(
                        RequestBodyProperty,
                        Encoding.UTF8.GetString(buffer, 0, length))
                    .Information(
                        RequestTemplate,
                        httpContext.Request.Method,
                        httpContext.Request.Path);

                SharedBytePool.Return(buffer);
            }
            else
            {
                Log
                    .ForContext(
                        RequestHeadersProperty,
                        httpContext.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                        destructureObjects: true)
                    .Information(
                        RequestTooLargeTemplate,
                        httpContext.Request.Method,
                        httpContext.Request.Path);
            }
        }

        private async Task LogResponseBodyAsync(HttpContext httpContext)
        {
            if (httpContext.Response.ContentLength.HasValue
                && httpContext.Response.ContentLength.Value > 0
                && httpContext.Response.ContentLength.Value < LogResponseBodyMaxSize)
            {
                httpContext.Response.Body.Seek(0, SeekOrigin.Begin);

                using var streamReader = new StreamReader(httpContext.Response.Body, leaveOpen: true);
                var responseBody = await streamReader.ReadToEndAsync();

                httpContext.Response.Body.Seek(0, SeekOrigin.Begin);

                Log
                    .ForContext(
                        ResponseBodyProperty,
                        responseBody)
                    .Information(
                        ResponseTemplate,
                        httpContext.Request.Method,
                        httpContext.Request.Path,
                        httpContext.Response.StatusCode);
            }
            else
            {
                Log
                    .Information(
                        ResponseTooLargeTemplate,
                        httpContext.Request.Method,
                        httpContext.Request.Path,
                        httpContext.Response.StatusCode);
            }
        }
    }
}
