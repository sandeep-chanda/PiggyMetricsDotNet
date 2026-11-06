using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PiggyMetrics.StatisticsService.Config;
using PiggyMetrics.StatisticsService.Domain;
using PiggyMetrics.StatisticsService.Domain.Timeseries;
using StatisticsServiceApi = PiggyMetrics.StatisticsService.Service.StatisticsService;

namespace PiggyMetrics.StatisticsService.Controller;

[ApiController]
public class StatisticsController : ControllerBase
{
    private readonly StatisticsServiceApi _statisticsService;

    public StatisticsController(StatisticsServiceApi statisticsService)
    {
        _statisticsService = statisticsService;
    }

    [HttpGet("current")]
    [Authorize(Policy = "user")]
    public List<DataPoint> GetCurrentAccountStatistics()
    {
        return _statisticsService.FindByAccountName(User.Identity?.Name);
    }

    [HttpGet("{accountName}")]
    [ServerOrDemo]
    public List<DataPoint> GetStatisticsByAccountName(string accountName)
    {
        return _statisticsService.FindByAccountName(accountName);
    }

    [HttpPut("{accountName}")]
    [Authorize(Policy = "server")]
    public IActionResult SaveAccountStatistics(string accountName, [FromBody] Account account)
    {
        _statisticsService.Save(accountName, account);
        return Ok();
    }
}
