using JobOnlineAPI.Models;
using JobOnlineAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobOnlineAPI.Controllers
{
    public class AccountController(IUserService userService) : Controller
    {
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await userService.AuthenticateAsync(model.Username, model.Password);

                if (user != null)
                {
                    HttpContext.Session.SetString("Username", user.Username);
                    HttpContext.Session.SetString("Role", user.Role);

                    return RedirectToAction("Index", "Home");
                }

                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            }

            return View(model);
        }
    }
}