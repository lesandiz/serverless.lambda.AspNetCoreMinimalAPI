using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.AspNetCoreServer.Hosting.Internal;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Trace;

namespace AspNetCoreMinimalAPI
{
    public class InstrumentedAPIGatewayRestApiLambdaRuntimeSupportServer : APIGatewayRestApiLambdaRuntimeSupportServer
    {
        private readonly ILambdaSerializer _serializer;

        public InstrumentedAPIGatewayRestApiLambdaRuntimeSupportServer(
            IServiceProvider serviceProvider, ILambdaSerializer serializer) : base(serviceProvider)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        protected override HandlerWrapper CreateHandlerWrapper(IServiceProvider serviceProvider)
        {
            var innerHandler = new APIGatewayRestApiMinimalApi(serviceProvider).FunctionHandlerAsync;

            // Wrap original handler to create OpenTelemetry parent trace
            var outerHandler = (APIGatewayProxyRequest input, ILambdaContext context) => 
            AWSLambdaWrapper.Trace(TracerProvider.Default, innerHandler, input, context);                
            
            return HandlerWrapper.GetHandlerWrapper(outerHandler, _serializer);
        }
    }
}
