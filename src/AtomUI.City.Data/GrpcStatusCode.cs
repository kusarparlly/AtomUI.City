namespace AtomUI.City.Data;

/// <summary>
/// Defines the supported grpc status code values.
/// </summary>
public enum GrpcStatusCode
{
    /// <summary>
    /// Represents the ok value.
    /// </summary>
    OK = 0,
    /// <summary>
    /// Represents the cancelled value.
    /// </summary>
    Cancelled = 1,
    /// <summary>
    /// Represents the unknown value.
    /// </summary>
    Unknown = 2,
    /// <summary>
    /// Represents the invalid argument value.
    /// </summary>
    InvalidArgument = 3,
    /// <summary>
    /// Represents the deadline exceeded value.
    /// </summary>
    DeadlineExceeded = 4,
    /// <summary>
    /// Represents the not found value.
    /// </summary>
    NotFound = 5,
    /// <summary>
    /// Represents the already exists value.
    /// </summary>
    AlreadyExists = 6,
    /// <summary>
    /// Represents the permission denied value.
    /// </summary>
    PermissionDenied = 7,
    /// <summary>
    /// Represents the resource exhausted value.
    /// </summary>
    ResourceExhausted = 8,
    /// <summary>
    /// Represents the failed precondition value.
    /// </summary>
    FailedPrecondition = 9,
    /// <summary>
    /// Represents the aborted value.
    /// </summary>
    Aborted = 10,
    /// <summary>
    /// Represents the out of range value.
    /// </summary>
    OutOfRange = 11,
    /// <summary>
    /// Represents the unimplemented value.
    /// </summary>
    Unimplemented = 12,
    /// <summary>
    /// Represents the internal value.
    /// </summary>
    Internal = 13,
    /// <summary>
    /// Represents the unavailable value.
    /// </summary>
    Unavailable = 14,
    /// <summary>
    /// Represents the data loss value.
    /// </summary>
    DataLoss = 15,
    /// <summary>
    /// Represents the unauthenticated value.
    /// </summary>
    Unauthenticated = 16,
}
