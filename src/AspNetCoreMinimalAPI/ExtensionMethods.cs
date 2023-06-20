using Amazon.Lambda.AspNetCoreServer.Hosting.Internal;
using Amazon.Lambda.AspNetCoreServer.Internal;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Contrib.Extensions.AWSXRay.Resources;
using OpenTelemetry.Contrib.Extensions.AWSXRay.Trace;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Expressions;
using Serilog.Settings.Configuration;
using Serilog.Templates;
using System.Reflection;

namespace AspNetCoreMinimalAPI
{
    public static class ExtensionMethods
    {
        public static IServiceCollection AddLogging(this IServiceCollection services, IConfiguration configuration)
        {
            // Serilog
            var options = new ConfigurationReaderOptions(typeof(ConsoleLoggerConfigurationExtensions).Assembly, typeof(SerilogExpression).Assembly)
            {
                SectionName = "Logging"
            };

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration, options)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Service", configuration.GetServiceName())
                .Enrich.WithProperty("Environment", configuration.GetEnvironmentName())
                // This format is required for Cloudwatch to correlate logs with traces in X-Ray based on AwsRequestId
                // Although not pure JSON, search expressions in Cloudwatch work on the JSON part of the log entry
                .WriteTo.Console(new ExpressionTemplate("{UtcDateTime(@t):o}\t{AwsRequestId}\t{@l}\t{ {@t, @m, @l, @x, ..@p} }\n"))
                .CreateLogger();

            services.AddScoped<LoggingMiddleware>();

            // Add Serilog to the ServiceCollection to be injected into dependencies using ILogger
            // This guarantees a consistent logging format from all the dependencies using ILogger abstraction
            return services.AddLogging(options =>
            {
                options.ClearProviders();
                options.AddSerilog(dispose: true);
            });
        }

        public static string GetServiceName(this IConfiguration configuration)
        {
            return configuration.GetValue<string>("ZPL_SERVICE_NAME") ?? "unknown";
        }

        public static string GetEnvironmentName(this IConfiguration configuration)
        {
            return configuration.GetValue<string>("ZPL_ENVIRONMENT") ?? "unknown";
        }

        public static bool TryGetAwsRequestId(this HttpContext context, out string awsRequestId)
        {
            var lambdaContext = context.Items[Amazon.Lambda.AspNetCoreServer.AbstractAspNetCoreFunction.LAMBDA_CONTEXT] as ILambdaContext;
            awsRequestId = lambdaContext?.AwsRequestId;
            return awsRequestId != null;
        }

        public static IServiceCollection AddOpenTelemetry(this IServiceCollection collection, IWebHostEnvironment environment, IConfiguration configuration)
        {
            var serviceName = configuration.GetServiceName();
            collection.AddSingleton(TracerProvider.Default.GetTracer(serviceName));
            return collection;
        }

        private static TracerProviderBuilder AddAWSLambdaInstrumentation(this TracerProviderBuilder builder)
        {
            var functionName = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME");

            if (string.IsNullOrEmpty(functionName))
            {
                return builder;
            }

            Log.Logger.Information("AddAWSLambdaInstrumentation for function {functionName}", functionName);

            return builder.AddAWSLambdaConfigurations();
        }

        public static IServiceCollection AddAWSLambdaHosting<T>(this IServiceCollection services) where T : LambdaRuntimeSupportServer
        {
            // Not running in Lambda so exit and let Kestrel be the web server
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME")))
                return services;

            services.AddSingleton<ILambdaSerializer, DefaultLambdaJsonSerializer>();

            Utilities.EnsureLambdaServerRegistered(services, typeof(T));

            return services;
        }

    }
}
