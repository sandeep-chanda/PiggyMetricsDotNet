using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PiggyMetrics.NotificationService.Domain;
using RecipientServiceApi = PiggyMetrics.NotificationService.Service.RecipientService;

namespace PiggyMetrics.NotificationService.Controller;

[ApiController]
[Route("recipients")]
public class RecipientController : ControllerBase
{
    private readonly RecipientServiceApi _recipientService;

    public RecipientController(RecipientServiceApi recipientService)
    {
        _recipientService = recipientService;
    }

    [HttpGet("current")]
    [Authorize(Policy = "user")]
    public IActionResult GetCurrentNotificationsSettings()
    {
        return Ok(_recipientService.FindByAccountName(User.Identity?.Name ?? string.Empty));
    }

    [HttpPut("current")]
    [Authorize(Policy = "user")]
    public IActionResult SaveCurrentNotificationsSettings([FromBody] Recipient recipient)
    {
        if (recipient is null || !ModelState.IsValid)
        {
            return BadRequest();
        }

        return Ok(_recipientService.Save(User.Identity?.Name ?? string.Empty, recipient));
    }
}
