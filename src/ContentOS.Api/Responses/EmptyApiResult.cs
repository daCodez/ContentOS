using System;

namespace ContentOS.Api.Responses
{
    /// <summary>
    /// Standardized API response wrapper for successful operations without data.
    /// </summary>
    public class EmptyApiResult
    {
        public EmptyApiResult()
        {
            IsSuccessful = true;
            Timestamp = DateTime.UtcNow;
        }

        public bool IsSuccessful { get; set; }
        public string? Message { get; set; }
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Creates a successful response with optional message.
        /// </summary>
        public static EmptyApiResult Successful(string? message = null)
        {
            return new EmptyApiResult
            {
                IsSuccessful = true,
                Message = message,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Creates an error response with message.
        /// </summary>
        public static EmptyApiResult Failed(string message)
        {
            return new EmptyApiResult
            {
                IsSuccessful = false,
                Message = message,
                Timestamp = DateTime.UtcNow
            };
        }
    }
}