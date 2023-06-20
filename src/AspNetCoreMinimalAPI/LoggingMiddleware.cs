using Serilog.Context;
using Serilog.Core;
using Serilog.Core.Enrichers;

namespace AspNetCoreMinimalAPI
{
    public class LoggingMiddleware : IMiddleware
    {
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var enrichers = new List<ILogEventEnricher>();
            
            if (context.TryGetAwsRequestId(out var requestId))
            {
                enrichers.Add(new PropertyEnricher("AwsRequestId", requestId));
            }

            if (enrichers.Any())
            {
                using (LogContext.Push(enrichers.ToArray()))
                {
                    await next(context);
                }
            }
            else
            {
                await next(context);
            }
        }
    }
}
