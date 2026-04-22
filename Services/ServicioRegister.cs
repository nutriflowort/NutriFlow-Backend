using Microsoft.AspNetCore.Identity.Data;
using Npgsql;
using Nutriflow.DTOs;

namespace Nutriflow.Services
{
    public class ServicioRegister
    {
        private readonly IConfiguration _configuration;

        // INYECCIONES DE DEPENDENCIA
        public ServicioRegister(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<RegisterResponse?> Register(DTOs.RegisterRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Nombre) ||
                    string.IsNullOrWhiteSpace(request.Apellido) ||
                    string.IsNullOrWhiteSpace(request.Email) ||
                    string.IsNullOrWhiteSpace(request.Password))
                {
                    return null;
                }

                var connectionString = _configuration.GetConnectionString("SupabaseConnection");

                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                // VERIFICA SI YA EXISTE UN USUARIO CON ESE EMAIL
                var checkQuery = @"SELECT COUNT(*) FROM usuarios WHERE LOWER(email) = LOWER(@email);";

                await using var checkCmd = new NpgsqlCommand(checkQuery, conn);
                checkCmd.Parameters.AddWithValue("email", request.Email);

                var count = (long)(await checkCmd.ExecuteScalarAsync() ?? 0);

                if (count > 0)
                {
                    return null; // EL CONTROLLER LO MANEJA COMO CONFLICTO
                }

                // INSERTA EL NUEVO USUARIO Y DEVUELVE SUS DATOS
                var insertQuery = @"INSERT INTO usuarios (nombre, apellido, email, ""contraseña"")
                                    VALUES (@nombre, @apellido, @email, @password)
                                    RETURNING id, nombre, email;";

                await using var insertCmd = new NpgsqlCommand(insertQuery, conn);
                insertCmd.Parameters.AddWithValue("nombre", request.Nombre);
                insertCmd.Parameters.AddWithValue("apellido", request.Apellido);
                insertCmd.Parameters.AddWithValue("email", request.Email);
                insertCmd.Parameters.AddWithValue("password", request.Password);

                await using var reader = await insertCmd.ExecuteReaderAsync();

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

                return new RegisterResponse
                {
                    Message = "Registro correcto",
                    User = user
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR REGISTER:");
                Console.WriteLine(ex.ToString());
                throw;
            }
        }
    }
}