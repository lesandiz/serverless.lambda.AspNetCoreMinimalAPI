using AspNetCoreMinimalAPI;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging(builder.Environment, builder.Configuration);

builder.Services.AddOpenTelemetry(builder.Environment, builder.Configuration);

builder.Services.AddControllers();

// Add Swagger/OpenAPI support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add AWS Lambda support. When application is run in Lambda Kestrel is swapped out as the web server with Amazon.Lambda.AspNetCoreServer. This
// package will act as the webserver translating request and responses between the Lambda event source and ASP.NET Core.
builder.Services.AddAWSLambdaHosting<InstrumentedAPIGatewayRestApiLambdaRuntimeSupportServer>();

var app = builder.Build();

if (builder.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<LoggingMiddleware>();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/welcome", () => $"Welcome to v1.0 from {Environment.MachineName}. Trace: {Activity.Current?.Id}");

app.Run();
