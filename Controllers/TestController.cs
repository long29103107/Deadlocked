using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace TestDeadlock.Controllers
{
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
            var task1 = Task.Run(() => SimulateDeadlock(dbContext1, 1, 2));

            using var dbContext2 = new AppDbContext(configuration);
            var task2 = Task.Run(() => SimulateDeadlock(dbContext2, 2, 1)); // Same order for consistent locking

            await Task.WhenAll(task1, task2);

            return Ok();
        }

        private async Task SimulateDeadlock(AppDbContext context, int id1, int id2)
        {
            const int maxRetries = 3;
            const int delayTime = 1500;
            int retries = 0;

            while (retries < maxRetries)
            {
                using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    // Lock the first product
                    var product1 = await context.Products.FindAsync(id1);
                    product1.Quantity -= 10;
                    await context.SaveChangesAsync();

                    // Simulate delay to increase the chances of deadlock
                    await Task.Delay(1000 * retries);

                    // Lock the second product
                    var product2 = await context.Products.FindAsync(id2);
                    product2.Quantity += 10;
                    await context.SaveChangesAsync();

                    await transaction.CommitAsync();
                    break; // Exit the loop if successful
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    retries++;
                    Console.WriteLine($"Concurrency issue (attempt {retries}): {id1} {id2} - {ex.Message}");
                    await transaction.RollbackAsync();
                }
                catch (SqlException ex) when (ex.Number == 1205) // Deadlock error
                {
                    retries++;
                    Console.WriteLine($"Deadlock detected (attempt {retries}): {id1} {id2} - {ex.Message}");
                    await transaction.RollbackAsync();
                }
                catch (Exception ex)
                {
                    // Handle other exceptions
                    Console.WriteLine($"Transaction failed: {id1} {id2} - {ex.Message}");
                    await transaction.RollbackAsync();
                    break; // Exit the loop on non-retryable exceptions
                }
            }
        }
    }
}
