using Microsoft.AspNetCore.Mvc;
using Nutriflow.DTOs;
using Nutriflow.Services;

namespace Nutriflow.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class LoginController : ControllerBase
    {
        private readonly ServicioLogin _servicioLogin;

        public LoginController(ServicioLogin servicioLogin)
        {
            _servicioLogin = servicioLogin;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Email y contraseña son obligatorios" });
            }

            var resultado = await _servicioLogin.LoginAsync(request);

            if (resultado == null)
            {
                return Unauthorized(new { message = "Credenciales inválidas" });
            }

            return Ok(resultado);
        }
    }
}