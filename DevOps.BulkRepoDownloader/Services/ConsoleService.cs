using System;

namespace DevOps.BulkRepoDownloader.Services
{
    public class ConsoleService
    {
        /// <summary>
        /// Writes an error message to the console in red.
        /// </summary>
        /// <param name="message">The error message to display.</param>
        public void WriteError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        /// <summary>
        /// Writes a success message to the console in green.
        /// </summary>
        /// <param name="message">The success message to display.</param>
        public void WriteSuccess(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        /// <summary>
        /// Writes an informational message to the console using the default color.
        /// </summary>
        /// <param name="message">The informational message to display.</param>
        public void WriteInfo(string message)
        {
            Console.WriteLine(message);
        }
    }
}
