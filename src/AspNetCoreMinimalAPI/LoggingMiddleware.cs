using Serilog.Context;
using Serilog.Core;
using Serilog.Core.Enrichers;

namespace AspNetCoreMinimalAPI
{
    public class LoggingMiddleware : IMiddleware
    {
        public Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var enrichers = new List<ILogEventEnricher>();
            
            if (context.TryGetAwsRequestId(out var requestId))
            {
                enrichers.Add(new PropertyEnricher("AwsRequestId", requestId));
            }

            if (context.TryGetAltoGroupId(out var altoGroupId))
            {
                enrichers.Add(new PropertyEnricher("AltoGroupId", altoGroupId));
            }

            if (enrichers.Any())
            {
                using (LogContext.Push(enrichers.ToArray()))
                {
                    return next(context);
                }
            }
            else
            {
                return next(context);
            }
        }
    }
}
