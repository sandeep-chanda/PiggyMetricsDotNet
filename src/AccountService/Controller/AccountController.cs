using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PiggyMetrics.AccountService.Config;
using PiggyMetrics.AccountService.Domain;
using AccountServiceApi = PiggyMetrics.AccountService.Service.AccountService;

namespace PiggyMetrics.AccountService.Controller;

[ApiController]
public class AccountController : ControllerBase
{
    private readonly AccountServiceApi _accountService;

    public AccountController(AccountServiceApi accountService)
    {
        _accountService = accountService;
    }

    [HttpGet("{name}")]
    [ServerOrDemo]
    public IActionResult GetAccountByName(string name)
    {
        return Ok(_accountService.FindByName(name));
    }

    [HttpGet("current")]
    [Authorize(Policy = "user")]
    public IActionResult GetCurrentAccount()
    {
        return Ok(_accountService.FindByName(User.Identity?.Name ?? string.Empty));
    }

    [HttpPut("current")]
    [Authorize(Policy = "user")]
    public IActionResult SaveCurrentAccount([FromBody] Account account)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        _accountService.SaveChanges(User.Identity?.Name ?? string.Empty, account);
        return Ok();
    }

    [HttpPost("/")]
    [AllowAnonymous]
    public IActionResult CreateNewAccount([FromBody] User user)
    {
        if (user is null || !ModelState.IsValid)
        {
            return BadRequest();
        }

        return Ok(_accountService.Create(user));
    }
}
