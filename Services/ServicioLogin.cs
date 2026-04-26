using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Nutriflow.Dtos;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Nutriflow.Services
{
    public class ServicioLogin
    {
        private readonly IConfiguration _config;

        //INYECCIONES DE DEPENDENCIA 
        public ServicioLogin(IConfiguration configuration)
        {
            _config = configuration;
        }

        public async Task<LoginResponse?> Login(LoginRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return null;
                }

                var connectionString = _config.GetConnectionString("SupabaseConnection");

                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                var query = @"SELECT id, nombre, email, rol, ""contraseña""
                              FROM usuarios
                              WHERE LOWER(email) = LOWER(@email)
                              LIMIT 1;";

                await using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("email", request.Email);

                await using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return null;
                }

                var passwordGuardada = reader["contraseña"]?.ToString() ?? "";

                //DESHASHEA LA CONTRASEÑA
                if (!BCrypt.Net.BCrypt.Verify(request.Password, passwordGuardada))
                {
                    return null;
                }

                var user = new UserDto
                {
                    Id = reader.GetGuid(reader.GetOrdinal("id")),
                    Nombre = reader["nombre"]?.ToString() ?? string.Empty,
                    Email = reader["email"]?.ToString() ?? string.Empty,
                    Rol = reader["rol"]?.ToString() ?? string.Empty
                };

                //GENERA TOKEN DE JWT
                var token = GenerarJwt(user);

                //RESPUESTA 
                return new LoginResponse
                {
                    Message = "Login correcto",
                    User = user,
                    Token = token
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR LOGIN:");
                Console.WriteLine(ex.ToString());
                throw;
            }
        }


        //GENERACION DE JWT
        private string GenerarJwt(UserDto user)
        {
            //TOMA LA CLAVE DE APPSETTINGS,JSON
            var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]!);

            //DATOS DEL USUARIO QUE VAND ENTRO DEL JWT 
            var claims = new[]
            {
                new Claim("id", user.Id.ToString()),
                new Claim("email", user.Email),
                new Claim("nombre", user.Nombre),
                new Claim("rol", user.Rol),
            };

            //CREA TOKEN 
            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256 //HASH DE TOKEN Y NADIE LA PUEDE MODIFICAR
                )
            );

            //DEVUELVE EL TOKEN COMO STRING
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

    }
}