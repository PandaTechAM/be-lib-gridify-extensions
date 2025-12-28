using System.Reflection;
using GridifyExtensions.Demo;
using GridifyExtensions.Demo.Context;
using GridifyExtensions.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

builder.Services.AddDbContextPool<PostgresContext>(o =>
   o.UseNpgsql(
       "Server=localhost;Port=5432;Database=gridify_test;User Id=test;Password=test;Pooling=true;Include Error Detail=true;")
    .UseSnakeCaseNamingConvention());

builder.AddGridify(Assembly.GetExecutingAssembly());

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
   var db = scope.ServiceProvider.GetRequiredService<PostgresContext>();
   await db.Database.EnsureCreatedAsync();
}

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapEstateEndpoints();

app.Run();