using GridifyExtensions.Extensions;
using System.Reflection;
using GridifyExtensions.Models;
using Microsoft.AspNetCore.Mvc;
using BaseConverter;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

builder.AddGridify(PandaBaseConverter.Base36Chars, Assembly.GetExecutingAssembly());

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.Run();

[ApiController]
[Route("api/")]
public class SomeController : ControllerBase
{
    [HttpGet("test")]
    public IActionResult Get([FromQuery] GridifyQueryModel request)
    {
        return Ok(request);
    }
}