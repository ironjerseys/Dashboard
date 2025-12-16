using Dashboard.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Dashboard.Controllers;

[Authorize]
public class AccountController : Controller
{
    private readonly UserManager<IdentityUser> _userMgr;
    private readonly SignInManager<IdentityUser> _signInMgr;

    public AccountController(UserManager<IdentityUser> um, SignInManager<IdentityUser> sm)
    {
        _userMgr = um;
        _signInMgr = sm;
    }

    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = new IdentityUser(vm.Email) { Email = vm.Email };
        var res = await _userMgr.CreateAsync(user, vm.Password);
        if (!res.Succeeded)
        {
            foreach (var e in res.Errors)
                ModelState.AddModelError("", e.Description);
            return View(vm);
        }

        await _signInMgr.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Dashboard");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
        => View(new LoginViewModel { ReturnUrl = returnUrl });

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var res = await _signInMgr.PasswordSignInAsync(
            vm.Email, vm.Password, vm.RememberMe, lockoutOnFailure: false
        );
        if (res.Succeeded)
            return Redirect(vm.ReturnUrl ?? Url.Action("Index", "Dashboard")!);

        ModelState.AddModelError("", "Identifiants invalides");
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _signInMgr.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied() => View();
}