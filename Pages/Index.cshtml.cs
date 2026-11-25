using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace MysteryBoxes.Pages;
public class IndexModel : PageModel { public IActionResult OnGet() => Redirect("/Admin"); }
