using System;

namespace ContentOS.Api.Responses
{
    /// <summary>
    /// Standardized API response wrapper for successful operations with data.
    /// </summary>
    /// <typeparam name="T">The type of data being returned.</typeparam>
    public class ApiResult<T>
    {
        public ApiResult()
        {
            IsSuccessful = true;
            Timestamp = DateTime.UtcNow;
        }

        public bool IsSuccessful { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Creates a successful response with data and optional message.
        /// </summary>
        public static ApiResult<T> Successful(T? data, string? message = null)
        {
            return new ApiResult<T>
            {
                IsSuccessful = true,
                Data = data,
                Message = message,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Creates a successful response with only a message (no data).
        /// </summary>
        public static ApiResult<T> Successful(string? message = null)
        {
            return new ApiResult<T>
            {
                IsSuccessful = true,
                Message = message,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Creates an error response with message and optional data.
        /// </summary>
        public static ApiResult<T> Failed(string message, T? data = default)
        {
            return new ApiResult<T>
            {
                IsSuccessful = false,
                Message = message,
                Data = data,
                Timestamp = DateTime.UtcNow
            };
        }
    }
}