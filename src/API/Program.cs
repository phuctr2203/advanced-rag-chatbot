using Microsoft.Extensions.FileProviders;
using PolicyBot.Api.DependencyInjection;
using PolicyBot.Api.Services.Ingestion.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services
    .AddConfiguredOptions(builder.Configuration)
    .AddProviderServices()
    .AddIngestionServices()
    .AddQueryServices()
    .AddVectorServices();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var pathResolver = app.Services.GetRequiredService<ConfiguredPathResolver>();
var uploadedDocumentsPath = pathResolver.UploadedDocumentsPath;
Directory.CreateDirectory(uploadedDocumentsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadedDocumentsPath),
    RequestPath = "/documents"
});

var imageStorePath = pathResolver.ImageStorePath;
Directory.CreateDirectory(imageStorePath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imageStorePath),
    RequestPath = "/images"
});

var templatesStorePath = pathResolver.TemplatesStorePath;
Directory.CreateDirectory(templatesStorePath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(templatesStorePath),
    RequestPath = "/templates"
});

app.MapControllers();

app.Run();
