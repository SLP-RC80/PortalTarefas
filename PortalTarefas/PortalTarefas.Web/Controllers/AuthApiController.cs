
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using PortalTarefas.Web.Models;
using System.ComponentModel.DataAnnotations;

namespace PortalTarefas.Web.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthApiController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AuthApiController> _logger;

    public AuthApiController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AuthApiController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    // ==========================================
    // 1. CONSULTAR USUARIO AUTENTICADO
    // ==========================================

    [AllowAnonymous]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Ok(AuthUserResponse.Anonymous);
        }

        var user = await _userManager.GetUserAsync(User);

        if (user == null)
        {
            await _signInManager.SignOutAsync();
            return Ok(AuthUserResponse.Anonymous);
        }

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new AuthUserResponse(
            true,
            user.Id,
            user.Email,
            user.NomeExibicao,
            roles.ToArray()));
    }

    // ==========================================
    // 2. LOGIN
    // ==========================================

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] AuthLoginRequest request)
    {
        var email = request.Email.Trim();

        var result = await _signInManager.PasswordSignInAsync(
            email,
            request.Password,
            request.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                await _signInManager.SignOutAsync();

                return Unauthorized(new
                {
                    message = "Não foi possível identificar o usuário."
                });
            }

            var roles = await _userManager.GetRolesAsync(user);

            _logger.LogInformation(
                "Autenticação React realizada para {Email}.",
                email);

            return Ok(new AuthUserResponse(
                true,
                user.Id,
                user.Email,
                user.NomeExibicao,
                roles.ToArray()));
        }

        if (result.IsLockedOut)
        {
            return StatusCode(
                StatusCodes.Status423Locked,
                new
                {
                    message = "Conta temporariamente bloqueada."
                });
        }

        if (result.RequiresTwoFactor)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message = "Autenticação em duas etapas necessária."
                });
        }

        _logger.LogWarning(
            "Falha na autenticação React para {Email}.",
            email);

        return Unauthorized(new
        {
            message = "E-mail ou senha inválidos."
        });
    }

    // ==========================================
    // 3. LOGOUT
    // ==========================================

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();

        _logger.LogInformation(
            "Sessão React encerrada.");

        return NoContent();
    }
}

// ==========================================
// MODELOS DE RESPOSTA
// ==========================================

public sealed class AuthLoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;

    public bool RememberMe { get; init; }
}

public sealed record AuthUserResponse(
    bool Authenticated,
    string? Id,
    string? Email,
    string? NomeExibicao,
    string[] Roles)
{
    public static AuthUserResponse Anonymous { get; } =
        new(
            false,
            null,
            null,
            null,
            Array.Empty<string>());
}
