using Npgsql;
using Nutriflow.DTOs;

namespace Nutriflow.Services
{
    public class ServicioLogin
    {
        private readonly IConfiguration _configuration;

        public ServicioLogin(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<LoginResponse?> LoginAsync(LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return null;
            }

            var connectionString = _configuration.GetConnectionString("SupabaseConnection");

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT id, nombre, email
                FROM usuarios
                WHERE LOWER(email) = LOWER(@email)
                  AND ""contraseña"" = @password
                LIMIT 1;
            ";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("email", request.Email);
            cmd.Parameters.AddWithValue("password", request.Password);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return null;
            }

            var user = new UserDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                Nombre = reader["nombre"]?.ToString() ?? string.Empty,
                Email = reader["email"]?.ToString() ?? string.Empty
            };

            return new LoginResponse
            {
                Message = "Login correcto",
                User = user
            };
        }
    }
}