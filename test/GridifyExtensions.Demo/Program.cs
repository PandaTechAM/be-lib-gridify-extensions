using GridifyExtensions;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.AddGridify(Assembly.GetExecutingAssembly());

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();


app.Run();