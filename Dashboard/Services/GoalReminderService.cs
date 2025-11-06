using Dashboard.Data;
using Dashboard.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace Dashboard.Services;

// Reminder service disabled after simplification of goals. Keeping empty type to avoid DI errors if referenced.
public class GoalReminderService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
