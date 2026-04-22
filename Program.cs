using Nutriflow.Services;

var builder = WebApplication.CreateBuilder(args);

//Add services to the container.
builder.Services.AddControllers();

//Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// Registro automático de Services
var services = typeof(Program).Assembly.GetTypes()
    .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Service"));

foreach (var service in services)
{
    builder.Services.AddScoped(service);
}


builder.Services.AddScoped<ServicioLogin>();
builder.Services.AddScoped<ServicioRegister>();

var app = builder.Build();

// Swagger
app.UseSwagger();
app.UseSwaggerUI();

// CORS
app.UseCors("AllowAll");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();