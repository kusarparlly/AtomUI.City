using System.Net;

namespace AtomUI.City.Data;

/// <summary>
/// Represents data error mapper.
/// </summary>
public static class DataErrorMapper
{
    /// <summary>
    /// Executes the from http status code operation.
    /// </summary>
    public static DataError FromHttpStatusCode(HttpStatusCode statusCode)
    {
        var numericStatusCode = (int)statusCode;
        if (numericStatusCode is < 100 or > 599)
        {
            throw new ArgumentOutOfRangeException(
                nameof(statusCode),
                statusCode,
                "HTTP status code must be between 100 and 599.");
        }

        if (numericStatusCode is >= 200 and <= 299)
        {
            throw new ArgumentException("A successful HTTP status code cannot be mapped as an error.", nameof(statusCode));
        }

        var kind = statusCode switch
        {
            HttpStatusCode.BadRequest => DataErrorKind.BadRequest,
            HttpStatusCode.Unauthorized => DataErrorKind.AuthenticationRequired,
            HttpStatusCode.Forbidden => DataErrorKind.AuthorizationForbidden,
            HttpStatusCode.NotFound => DataErrorKind.NotFound,
            HttpStatusCode.Conflict => DataErrorKind.Conflict,
            HttpStatusCode.RequestTimeout => DataErrorKind.Timeout,
            HttpStatusCode.UnprocessableEntity => DataErrorKind.ValidationFailed,
            HttpStatusCode.TooManyRequests => DataErrorKind.PolicyRejected,
            HttpStatusCode.GatewayTimeout => DataErrorKind.Timeout,
            HttpStatusCode.ServiceUnavailable => DataErrorKind.ServiceUnavailable,
            >= HttpStatusCode.InternalServerError => DataErrorKind.ServerError,
            _ => DataErrorKind.TransportError,
        };

        return new DataError(
            kind,
            $"HTTP request failed with status code {(int)statusCode}.",
            ((int)statusCode).ToString(),
            GetHttpMessageKey(kind));
    }

    /// <summary>
    /// Executes the from grpc status operation.
    /// </summary>
    public static DataError FromGrpcStatus(GrpcStatusCode statusCode, string? detail = null)
    {
        if (!Enum.IsDefined(statusCode))
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode), statusCode, "gRPC status code is not supported.");
        }

        if (statusCode == GrpcStatusCode.OK)
        {
            throw new ArgumentException("A successful gRPC status cannot be mapped as an error.", nameof(statusCode));
        }

        if (detail is not null && string.IsNullOrWhiteSpace(detail))
        {
            throw new ArgumentException("gRPC failure detail cannot be empty when provided.", nameof(detail));
        }

        var kind = statusCode switch
        {
            GrpcStatusCode.Cancelled => DataErrorKind.Cancelled,
            GrpcStatusCode.InvalidArgument => DataErrorKind.ValidationFailed,
            GrpcStatusCode.DeadlineExceeded => DataErrorKind.DeadlineExceeded,
            GrpcStatusCode.Unauthenticated => DataErrorKind.AuthenticationRequired,
            GrpcStatusCode.PermissionDenied => DataErrorKind.AuthorizationForbidden,
            GrpcStatusCode.NotFound => DataErrorKind.NotFound,
            GrpcStatusCode.AlreadyExists => DataErrorKind.Conflict,
            GrpcStatusCode.ResourceExhausted => DataErrorKind.PolicyRejected,
            GrpcStatusCode.FailedPrecondition => DataErrorKind.Conflict,
            GrpcStatusCode.Aborted => DataErrorKind.Conflict,
            GrpcStatusCode.OutOfRange => DataErrorKind.ValidationFailed,
            GrpcStatusCode.Unimplemented => DataErrorKind.ServerError,
            GrpcStatusCode.Unavailable => DataErrorKind.ServiceUnavailable,
            GrpcStatusCode.Internal => DataErrorKind.ServerError,
            GrpcStatusCode.DataLoss => DataErrorKind.ServerError,
            _ => DataErrorKind.Unknown,
        };

        return new DataError(
            kind,
            detail ?? $"gRPC call failed with status '{statusCode}'.",
            statusCode.ToString(),
            GetGrpcMessageKey(kind));
    }

    private static string? GetHttpMessageKey(DataErrorKind kind)
    {
        return kind switch
        {
            DataErrorKind.AuthenticationRequired => "Errors.AuthenticationRequired",
            DataErrorKind.AuthorizationForbidden => "Errors.AuthorizationForbidden",
            DataErrorKind.NotFound => "Errors.NotFound",
            DataErrorKind.Timeout => "Errors.Timeout",
            DataErrorKind.ValidationFailed => "Errors.ValidationFailed",
            DataErrorKind.ServiceUnavailable => "Errors.ServiceUnavailable",
            DataErrorKind.ServerError => "Errors.ServerError",
            _ => null,
        };
    }

    private static string? GetGrpcMessageKey(DataErrorKind kind)
    {
        return kind switch
        {
            DataErrorKind.AuthenticationRequired => "Errors.AuthenticationRequired",
            DataErrorKind.AuthorizationForbidden => "Errors.AuthorizationForbidden",
            DataErrorKind.NotFound => "Errors.NotFound",
            DataErrorKind.DeadlineExceeded => "Errors.Timeout",
            DataErrorKind.ValidationFailed => "Errors.ValidationFailed",
            DataErrorKind.Conflict => "Errors.Conflict",
            DataErrorKind.ServiceUnavailable => "Errors.ServiceUnavailable",
            DataErrorKind.ServerError => "Errors.ServerError",
            _ => null,
        };
    }
}
