using AspNetCoreMinimalAPI;
using OpenTelemetry.Contrib.Instrumentation.AWSLambda.Implementation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging(builder.Configuration);

builder.Services.AddControllers();

// Add Swagger/OpenAPI support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add AWS Lambda support. When application is run in Lambda Kestrel is swapped out as the web server with Amazon.Lambda.AspNetCoreServer. This
// package will act as the webserver translating request and responses between the Lambda event source and ASP.NET Core.
builder.Services.AddAWSLambdaHosting<InstrumentedAPIGatewayRestApiLambdaRuntimeSupportServer>();

builder.Services.AddOpenTelemetry(builder.Environment, builder.Configuration);

var app = builder.Build();

app.UseSwagger();
if (builder.Environment.IsDevelopment())
{
    app.UseSwaggerUI();
}

app.UseMiddleware<LoggingMiddleware>();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/welcome", () => "Welcome to running ASP.NET Core Minimal API on AWS Lambda");

app.Run();
