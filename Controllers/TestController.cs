using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TestDeadlock.Controllers;
[Route("api/[controller]")]
[ApiController]
public class TestController : ControllerBase
{
    private readonly IConfiguration configuration;

    public TestController(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {

        using var dbContext1 = new AppDbContext(configuration);

        // Tạo hai tác vụ độc lập để tái hiện deadlock
        var task1 = Task.Run(() => SimulateDeadlock(dbContext1, 1, 2));

        using var dbContext2 = new AppDbContext(configuration);
        var task2 = Task.Run(() => SimulateDeadlock(dbContext2, 2, 1));

        await Task.WhenAll(task1, task2);

        return Ok();
    }

    private async Task SimulateDeadlock(AppDbContext context, int id1, int id2)
    {
        using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            // Khóa hàng đầu tiên
            var product1 = await context.Products.FindAsync(id1);
            product1.Quantity -= 10;
            await context.SaveChangesAsync();

            // Mô phỏng thời gian chờ để tạo deadlock
            await Task.Delay(100);

            // Khóa hàng thứ hai
            var product2 = await context.Products.FindAsync(id2);
            product2.Quantity += 10;
            await context.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Transaction failed: {id1} {id2} {ex.Message}");
            await transaction.RollbackAsync();
        }
    }

    private async Task SimulateDeadlock2(AppDbContext context, int id1, int id2)
    {
        using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            // Khóa hàng đầu tiên
            var product1 = await context.Products.FindAsync(id1);
            product1.Quantity -= 10;
            await context.SaveChangesAsync();

            // Mô phỏng thời gian chờ để tạo deadlock
            await Task.Delay(100);

            // Khóa hàng thứ hai
            var product2 = await context.Products.FindAsync(id2);
            product2.Quantity += 10;
            await context.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Transaction failed: {id1} {id2} {ex.Message}");
            await transaction.RollbackAsync();
        }
    }
}
