using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Authorization;
using PortalTarefas.Web.Models;
using PortalTarefas.Web.ViewModels;

namespace PortalTarefas.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    // =====================================
    // CADASTRO
    // =====================================

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(
        RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            NomeExibicao = model.NomeExibicao.Trim()
        };

        // 1. Criar usuário no ASP.NET Core Identity.
        var result = await _userManager.CreateAsync(
            user,
            model.Senha);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    string.Empty,
                    error.Description);
            }

            return View(model);
        }

        // 2. Atribuir automaticamente o papel Usuario.
        var roleResult = await _userManager.AddToRoleAsync(
            user,
            AuthorizationConstants.RoleUser);

        if (!roleResult.Succeeded)
        {
            _logger.LogError(
                "Falha ao atribuir papel Usuario ao usuário {Email}: {Errors}",
                user.Email,
                string.Join(
                    "; ",
                    roleResult.Errors.Select(e => e.Description)));

            // 3. Executar rollback caso a atribuição do papel falhe.
            var rollbackResult = await _userManager.DeleteAsync(user);

            if (!rollbackResult.Succeeded)
            {
                _logger.LogCritical(
                    "Falha ao remover usuário {Email} após erro na atribuição de papel: {Errors}",
                    user.Email,
                    string.Join(
                        "; ",
                        rollbackResult.Errors.Select(e => e.Description)));
            }

            ModelState.AddModelError(
                string.Empty,
                "Não foi possível concluir o cadastro. Tente novamente mais tarde.");

            return View(model);
        }

        // 4. Registrar usuário na sessão autenticada.
        // O papel já foi atribuído antes da criação do cookie.
        await _signInManager.SignInAsync(
            user,
            isPersistent: false);

        _logger.LogInformation(
            "Usuário {Email} cadastrado com sucesso e associado ao papel Usuario.",
            user.Email);

        return RedirectToAction("Index", "Home");
    }

    // =====================================
    // LOGIN
    // =====================================

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email.Trim(),
            model.Senha,
            model.LembrarMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation(
                "Usuário {Email} autenticado com sucesso.",
                model.Email);

            return RedirectToAction("Index", "Home");
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning(
                "Tentativa de acesso com conta bloqueada: {Email}",
                model.Email);

            ModelState.AddModelError(
                string.Empty,
                "Conta temporariamente bloqueada. Tente novamente mais tarde.");
        }
        else
        {
            _logger.LogWarning(
                "Falha na autenticação do usuário {Email}.",
                model.Email);

            ModelState.AddModelError(
                string.Empty,
                "E-mail ou senha inválidos.");
        }

        return View(model);
    }

    // =====================================
    // LOGOUT
    // =====================================

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();

        _logger.LogInformation(
            "Sessão do usuário encerrada.");

        return RedirectToAction(nameof(Login));
    }

    // =====================================
    // ACESSO NEGADO
    // =====================================

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}