
using EventTicketing.Api.Data;

namespace EventTicketing
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            // The executor holds the connection string; each invocation opens its own SQL connection.
            builder.Services.AddSingleton<ISqlStoredProcedureExecutor,SqlStoredProcedureExecutor>();

            // Keep operation-specific database mapping within the current request scope.
            builder.Services.AddScoped<IEventDataAccess, EventDataAccess>();

            // Controllers map expected outcomes to HTTP results; ProblemDetails covers unhandled failures.
            builder.Services.AddControllers();
            builder.Services.AddProblemDetails();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            // Return a generic ProblemDetails response for unhandled exceptions.
            app.UseExceptionHandler();

            // Publish the OpenAPI document only in the Development environment.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            // Controller actions run after the exception, HTTPS, and authorization middleware.
            app.MapControllers();

            app.Run();
        }
    }
}
