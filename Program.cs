using Nutriflow.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

//JWT
var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),

            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization();

// Registro automático de services (todo lo que esté en Nutriflow.Services)
var services = Assembly.GetExecutingAssembly().GetTypes()
    .Where(t => t.IsClass
             && !t.IsAbstract
             && t.Namespace == "Nutriflow.Services");

foreach (var service in services)
{
    builder.Services.AddScoped(service);
}

//Servicio de mail + forgot password
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<ServicioForgotPassword>();

var app = builder.Build();


// Swagger
app.UseSwagger();
app.UseSwaggerUI();

// CORS
app.UseCors("AllowAll");

app.UseHttpsRedirection();

// ORDEN IMPORTANTE
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();