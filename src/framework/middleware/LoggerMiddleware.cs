namespace SampleApi.Host.Errors
{
    using System.Threading.Tasks;
    using Framework.Logging;
    using Microsoft.AspNetCore.Http;

    /*
     * Middleware to do framework request logging
     */
    public class LoggerMiddleware
    {
        private readonly RequestDelegate next;

        /*
         * Store a reference to the next middleware
         */
        public LoggerMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        /*
         * Log the request and response
         */
        public async Task Invoke(HttpContext context)
        {
            // TODO: Create the log entry and add it to the container
            System.Console.WriteLine("*** Adding this request's log entry to the container");
            var logEntry = new LogEntry();

            // Run the next handler
            await this.next(context);

            // TODO: Update the log entry here
        }
    }
}
