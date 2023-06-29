using Amazon.Lambda.AspNetCoreServer.Hosting.Internal;
using Amazon.Lambda.AspNetCoreServer.Internal;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using OpenTelemetry;
using OpenTelemetry.Contrib.Extensions.AWSXRay.Resources;
using OpenTelemetry.Contrib.Extensions.AWSXRay.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Expressions;
using Serilog.Settings.Configuration;
using Serilog.Templates;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace AspNetCoreMinimalAPI
{
    public static class ExtensionMethods
    {
        public static IServiceCollection AddLogging(this IServiceCollection services, IWebHostEnvironment environment, IConfiguration configuration)
        {
            // Serilog
            var options = new ConfigurationReaderOptions(typeof(ConsoleLoggerConfigurationExtensions).Assembly, typeof(SerilogExpression).Assembly)
            {
                SectionName = "Logging"
            };

            var config = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration, options)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Service", configuration.GetServiceName())
                .Enrich.WithProperty("Environment", configuration.GetEnvironmentName());

            if (environment.IsDevelopment())
            {
                config.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
            }
            else
            {
                // This format is required for Cloudwatch to correlate logs with traces in X-Ray based on AwsRequestId
                // Although not pure JSON, search expressions in Cloudwatch work on the JSON part of the log entry
                config.WriteTo.Console(new ExpressionTemplate("{UtcDateTime(@t):o}\t{AwsRequestId}\t{@l}\t{ {@t, @m, @l, @x, ..@p} }\n"));
            }

            Log.Logger = config.CreateLogger();

            services.AddScoped<LoggingMiddleware>();

            // Add Serilog to the ServiceCollection to be injected into dependencies using ILogger
            // This guarantees a consistent logging format from all the dependencies using ILogger abstraction
            return services.AddLogging(options =>
            {
                options.ClearProviders();
                options.AddSerilog(dispose: true);
            });
        }

        public static IServiceCollection AddOpenTelemetry(this IServiceCollection collection, IWebHostEnvironment environment, IConfiguration configuration)
        {
            var serviceName = configuration.GetServiceName();
            var serviceVersion = configuration.GetServiceVersion();
            var serviceInstanceId = Guid.NewGuid().ToString();

            var openTelemetryBuilder = collection.AddOpenTelemetry()
                .ConfigureResource(builder => builder
                    .AddService(serviceName, serviceVersion: serviceVersion, autoGenerateServiceInstanceId: false, serviceInstanceId: serviceInstanceId)
                    .AddTelemetrySdk()
                    .AddEnvironmentVariableDetector()
                    .AddDetector(new AWSECSResourceDetector()))
                .WithTracing(builder => builder
                    .AddXRayTraceId() // for generating AWS X-Ray compliant trace IDs
                    .AddSource(serviceName)
                    .AddAWSInstrumentation() // for tracing calls to AWS services via AWS SDK for .NET
                    .AddAWSLambdaInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddAspNetCoreInstrumentation(opt => opt.RecordException = true)
                    .AddSqlClientInstrumentation(opt =>
                    {
                        opt.SetDbStatementForText = true;
                        opt.SetDbStatementForStoredProcedure = true;
                        opt.RecordException = true;
                    })
                    .AddOtlpExporter()) // default address localhost:4317
                .WithMetrics(builder => builder
                    .AddMeter(serviceName)
                    //.AddRuntimeInstrumentation()
                    //.AddHttpClientInstrumentation()
                    //.AddAspNetCoreInstrumentation()
                    .AddOtlpExporter());

            if (environment.IsDevelopment())
            {
                openTelemetryBuilder.WithTracing(builder => builder.AddConsoleExporter());
                openTelemetryBuilder.WithMetrics(builder => builder.AddConsoleExporter());
            }

            Sdk.SetDefaultTextMapPropagator(new AWSXRayPropagator());

            collection.AddSingleton(new Meter(serviceName));
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

            Log.Logger.Information("AddAWSLambdaConfigurations to OpenTelemetry configuration for function {functionName}", functionName);

            return builder.AddAWSLambdaConfigurations();
        }

        public static IServiceCollection AddAWSLambdaHosting<T>(this IServiceCollection services) where T : LambdaRuntimeSupportServer
        {
            // Not running in Lambda so exit and let Kestrel be the web server
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME")))
            {
                return services;
            }

            services.AddSingleton<ILambdaSerializer, DefaultLambdaJsonSerializer>();

            Utilities.EnsureLambdaServerRegistered(services, typeof(T));

            return services;
        }

        internal static string GetServiceName(this IConfiguration configuration)
        {
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var assemblyName = assembly.GetName().Name;

            return configuration.GetValue<string>("ZPL_SERVICE_NAME") ??
                configuration.GetValue<string>("AWS_LAMBDA_FUNCTION_NAME") ??
                assemblyName ??
                "unknown_service";

        }
        internal static string GetServiceVersion(this IConfiguration configuration)
        {
            var lambdaFunctionVersion = configuration.GetValue<string>("AWS_LAMBDA_FUNCTION_VERSION");
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var assemblyVersion = assembly.GetName().Version?.ToString();

            return lambdaFunctionVersion ??
                assemblyVersion ??
                "0.0.0-unknown";
        }

        internal static string GetEnvironmentName(this IConfiguration configuration)
        {
            return configuration.GetValue<string>("ZPL_ENVIRONMENT") ?? "unknown_environment";
        }

        public static bool TryGetAwsRequestId(this HttpContext context, out string awsRequestId)
        {
            var lambdaContext = context.Items[Amazon.Lambda.AspNetCoreServer.AbstractAspNetCoreFunction.LAMBDA_CONTEXT] as ILambdaContext;
            awsRequestId = lambdaContext?.AwsRequestId;
            return awsRequestId != null;
        }

        public static bool TryGetAltoGroupId(this HttpContext context, out string altoGroupId)
        {
            var headers = context.Request.Headers;
            altoGroupId = headers["Alto-GroupId"].FirstOrDefault();
            return altoGroupId != null;
        }
    }
}
