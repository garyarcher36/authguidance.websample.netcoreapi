﻿namespace Framework.Api.Base.Errors
{
    using System;
    using Framework.Api.Base.Logging;

    /*
     * A framework base class for error handling
     */
    public class ErrorUtils
    {
        /*
         * Do error handling and logging, then return an error to the client
         */
        public ClientError HandleError(Exception exception, LogEntry logEntry)
        {
            // Already handled API errors
            var apiError = this.TryConvertException<ApiErrorImpl>(exception);
            if (apiError != null)
            {
                // Log the error, which will include technical support details
                logEntry.SetApiError(apiError);

                // Return a client error to the caller
                return apiError.ToClientError();
            }

            // If the API has thrown a 4xx error using an IClientError derived type then it is logged here
            var clientError = this.TryConvertException<ClientError>(exception);
            if (clientError != null)
            {
                // Log the error without an id
                logEntry.SetClientError(clientError);

                // Return the thrown error to the caller
                return clientError;
            }

            // Unhandled exceptions
            apiError = ErrorUtils.FromException(exception);
            logEntry.SetApiError(apiError);
            return apiError.ToClientError();
        }

        /*
         * Handle unexpected data errors if an expected claim was not found in an OAuth message
         */
        public ApiErrorImpl FromMissingClaim(string claimName)
        {
            return new ApiErrorImpl("claims_failure", "Authorization data not found")
            {
                Details = $"An empty value was found for the expected claim {claimName}",
            };
        }

        /*
         * A default implementation for creating an API error from an unrecognised exception
         */
        protected static ApiErrorImpl FromException(Exception ex)
        {
            // Get the exception to use
            var exception = ex;
            if (ex is AggregateException)
            {
                if (ex.InnerException != null)
                {
                    exception = ex.InnerException;
                }
            }

            // Create a generic exception API error and note that in .Net the call stack is included in the details
            return new ApiErrorImpl("server_error", "An unexpected exception occurred in the API")
            {
                Details = exception.ToString(),
            };
        }

        /*
         * Try to convert an exception to a known type
         */
        protected T TryConvertException<T>(Exception exception)
            where T : class
        {
            if (typeof(T).IsAssignableFrom(exception.GetType()))
            {
                return exception as T;
            }

            if (exception is AggregateException)
            {
                if (typeof(T).IsAssignableFrom(exception.InnerException.GetType()))
                {
                    return exception as T;
                }
            }

            return null;
        }
    }
}
