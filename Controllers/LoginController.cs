using Microsoft.AspNetCore.Mvc;
using Nutriflow.Dtos;
using Nutriflow.Services;

namespace Nutriflow.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class LoginController : ControllerBase
    {
        private readonly ServicioLogin _servicioLogin;

        //INYECCIONES DE DEPENDENCIA
        public LoginController(ServicioLogin servicioLogin)
        {
            _servicioLogin = servicioLogin;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            //VERIFICA QUE NO SEAN NULOS LOS PARAMETROS 
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Email y contraseña son obligatorios" });
            }

            //LLAMA AL SERVICIO CORRESPONDIENTE
            var resultado = await _servicioLogin.Login(request);

            //SI NO EXISTE USUARIO, ENVIA MENSAJE DE ERROR
            if (resultado == null)
            {
                return Unauthorized(new { message = "Credenciales inválidas" });
            }

            //RETORNA OK SI SALIO TODO BIEN
            return Ok(resultado);
        }
    }
}