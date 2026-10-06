using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace Das.PdfProtocol;

public interface IPdfMaintenance { Task RunAsync(CancellationToken ct); }
public sealed class PdfMaintenanceWorker(IServiceScopeFactory scopes,ILogger<PdfMaintenanceWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(30));
        while(await timer.WaitForNextTickAsync(stoppingToken)) {
            using var budget=CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);budget.CancelAfter(TimeSpan.FromSeconds(25));
            try { using var scope=scopes.CreateScope();await scope.ServiceProvider.GetRequiredService<IPdfMaintenance>().RunAsync(budget.Token); }
            catch(OperationCanceledException) when(!stoppingToken.IsCancellationRequested){logger.LogWarning("PDF maintenance batch exceeded its budget; durable work will retry.");}
            catch(Exception) when(!stoppingToken.IsCancellationRequested){logger.LogWarning("PDF maintenance batch could not complete; durable work will retry.");}
        }
    }
}
