using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MysteryBoxes.Pages;
public class LoginModel(IConfiguration config) : PageModel
{
    public void OnGet() { }
    public async Task<IActionResult> OnPostAsync(string password)
    {
        var expected = config["Admin:Password"] ?? "";
        if (string.IsNullOrWhiteSpace(expected) || expected == "CHANGE-THIS-PASSWORD" || password != expected)
        {
            ViewData["Error"] = "Nieprawidłowe hasło. Ustaw własne hasło w appsettings.json.";
            return Page();
        }
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "admin"), new Claim(ClaimTypes.Role, "Administrator") }, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        var returnUrl = Request.Query["ReturnUrl"].ToString();
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        return Redirect("/Admin");
    }
}
