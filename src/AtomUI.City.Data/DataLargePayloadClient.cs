using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;

namespace AtomUI.City.Data;

/// <summary>
/// Defines the supported data transfer stage values.
/// </summary>
public enum DataTransferStage
{
    /// <summary>
    /// Represents the starting value.
    /// </summary>
    Starting,
    /// <summary>
    /// Represents the transferring value.
    /// </summary>
    Transferring,
    /// <summary>
    /// Represents the completing value.
    /// </summary>
    Completing,
    /// <summary>
    /// Represents the completed value.
    /// </summary>
    Completed,
}

/// <summary>
/// Defines the supported data range unsupported policy values.
/// </summary>
public enum DataRangeUnsupportedPolicy
{
    /// <summary>
    /// Represents the fail value.
    /// </summary>
    Fail,
    /// <summary>
    /// Represents the restart value.
    /// </summary>
    Restart,
}

/// <summary>
/// Represents data transfer progress.
/// </summary>
public sealed record DataTransferProgress(
    Guid OperationId,
    long BytesTransferred,
    long? TotalBytes,
    double? Percent,
    double BytesPerSecond,
    DataTransferStage Stage)
{
    /// <summary>
    /// Gets or sets operation id.
    /// </summary>
    public Guid OperationId { get; init; } = OperationId != Guid.Empty
        ? OperationId
        : throw new ArgumentException("Transfer operation id cannot be empty.", nameof(OperationId));

    /// <summary>
    /// Gets or sets bytes transferred.
    /// </summary>
    public long BytesTransferred { get; init; } = BytesTransferred >= 0
        ? BytesTransferred
        : throw new ArgumentOutOfRangeException(nameof(BytesTransferred));

    /// <summary>
    /// Gets or sets total bytes.
    /// </summary>
    public long? TotalBytes { get; init; } = TotalBytes is null || TotalBytes >= BytesTransferred
        ? TotalBytes
        : throw new ArgumentOutOfRangeException(nameof(TotalBytes));

    /// <summary>
    /// Gets or sets percent.
    /// </summary>
    public double? Percent { get; init; } = Percent is null
        || (double.IsFinite(Percent.Value) && Percent is >= 0 and <= 100)
            ? Percent
            : throw new ArgumentOutOfRangeException(nameof(Percent));

    /// <summary>
    /// Gets or sets bytes per second.
    /// </summary>
    public double BytesPerSecond { get; init; } = double.IsFinite(BytesPerSecond) && BytesPerSecond >= 0
        ? BytesPerSecond
        : throw new ArgumentOutOfRangeException(nameof(BytesPerSecond));

    /// <summary>
    /// Gets or sets stage.
    /// </summary>
    public DataTransferStage Stage { get; init; } = Enum.IsDefined(Stage)
        ? Stage
        : throw new ArgumentOutOfRangeException(nameof(Stage));
}

/// <summary>
/// Represents data transfer options.
/// </summary>
public sealed class DataTransferOptions
{
    private int _bufferSize = 64 * 1024;
    private TimeSpan _progressInterval = TimeSpan.FromMilliseconds(100);
    private long _resumeOffset;
    private DataRangeUnsupportedPolicy _rangeUnsupportedPolicy = DataRangeUnsupportedPolicy.Fail;
    private string? _temporaryDirectory;

    /// <summary>
    /// Represents the buffer size value.
    /// </summary>
    public int BufferSize
    {
        get => _bufferSize;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 4096, nameof(BufferSize));
            _bufferSize = value;
        }
    }

    /// <summary>
    /// Represents the progress interval value.
    /// </summary>
    public TimeSpan ProgressInterval
    {
        get => _progressInterval;
        init
        {
            if (value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(ProgressInterval), value, "Progress interval cannot be negative.");
            }

            _progressInterval = value;
        }
    }

    /// <summary>
    /// Represents the resume offset value.
    /// </summary>
    public long ResumeOffset
    {
        get => _resumeOffset;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value, nameof(ResumeOffset));
            _resumeOffset = value;
        }
    }

    /// <summary>
    /// Represents the range unsupported policy value.
    /// </summary>
    public DataRangeUnsupportedPolicy RangeUnsupportedPolicy
    {
        get => _rangeUnsupportedPolicy;
        init
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(RangeUnsupportedPolicy), value, "Range policy is not supported.");
            }

            _rangeUnsupportedPolicy = value;
        }
    }

    /// <summary>
    /// Represents the temporary directory value.
    /// </summary>
    public string? TemporaryDirectory
    {
        get => _temporaryDirectory;
        init
        {
            if (value is not null)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(TemporaryDirectory));
            }

            _temporaryDirectory = value;
        }
    }

    /// <summary>
    /// Gets default.
    /// </summary>
    public static DataTransferOptions Default { get; } = new();
}

/// <summary>
/// Represents data transfer receipt.
/// </summary>
public sealed record DataTransferReceipt(
    Guid OperationId,
    long BytesTransferred,
    HttpStatusCode StatusCode)
{
    /// <summary>
    /// Gets or sets operation id.
    /// </summary>
    public Guid OperationId { get; init; } = OperationId != Guid.Empty
        ? OperationId
        : throw new ArgumentException("Transfer operation id cannot be empty.", nameof(OperationId));

    /// <summary>
    /// Gets or sets bytes transferred.
    /// </summary>
    public long BytesTransferred { get; init; } = BytesTransferred >= 0
        ? BytesTransferred
        : throw new ArgumentOutOfRangeException(nameof(BytesTransferred));

    /// <summary>
    /// Gets or sets status code.
    /// </summary>
    public HttpStatusCode StatusCode { get; init; } = (int)StatusCode is >= 100 and <= 599
        ? StatusCode
        : throw new ArgumentOutOfRangeException(nameof(StatusCode));
}

/// <summary>
/// Represents data temporary file.
/// </summary>
public sealed class DataTemporaryFile : IAsyncDisposable
{
    private int _state;

    internal DataTemporaryFile(string path, DataTransferReceipt receipt)
    {
        Path = path;
        Receipt = receipt;
    }

    /// <summary>
    /// Gets path.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets receipt.
    /// </summary>
    public DataTransferReceipt Receipt { get; }

    /// <summary>
    /// Executes the commit operation.
    /// </summary>
    public void Commit()
    {
        while (true)
        {
            var state = Volatile.Read(ref _state);
            if (state == 2)
            {
                throw new ObjectDisposedException(nameof(DataTemporaryFile));
            }

            if (state == 1 || Interlocked.CompareExchange(ref _state, 1, 0) == 0)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Executes the dispose async operation.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _state, 2) == 0)
        {
            TryDelete(Path);
        }

        return ValueTask.CompletedTask;
    }

    internal static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>
/// Represents data large payload client.
/// </summary>
public sealed class DataLargePayloadClient
{
    private readonly HttpClient _httpClient;
    private readonly IDataDiagnostics? _diagnostics;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <c>DataLargePayloadClient</c> type.
    /// </summary>
    public DataLargePayloadClient(
        HttpClient httpClient,
        IDataDiagnostics? diagnostics = null,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _diagnostics = diagnostics;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Executes the upload async operation.
    /// </summary>
    public async ValueTask<DataResult<DataTransferReceipt>> UploadAsync(
        HttpRequestMessage request,
        Stream source,
        long? totalBytes = null,
        Func<DataTransferProgress, CancellationToken, ValueTask>? progress = null,
        DataTransferOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
        {
            throw new ArgumentException("Upload source stream must be readable.", nameof(source));
        }

        if (totalBytes is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalBytes));
        }

        if (request.Content is not null)
        {
            throw new ArgumentException("Upload requests must not already contain content.", nameof(request));
        }

        options ??= DataTransferOptions.Default;
        var operationId = Guid.NewGuid();
        var reporter = new ProgressReporter(operationId, totalBytes, progress, options, _diagnostics, _timeProvider);
        request.Content = new ProgressStreamContent(source, totalBytes, options.BufferSize, reporter);

        try
        {
            await reporter.ReportAsync(0, DataTransferStage.Starting, force: true, cancellationToken).ConfigureAwait(false);
            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return DataResult<DataTransferReceipt>.Failed(DataErrorMapper.FromHttpStatusCode(response.StatusCode));
            }

            await reporter.ReportAsync(reporter.BytesTransferred, DataTransferStage.Completed, force: true, cancellationToken)
                .ConfigureAwait(false);
            WriteCompleted(operationId, reporter.BytesTransferred);
            return DataResult<DataTransferReceipt>.Success(
                new DataTransferReceipt(operationId, reporter.BytesTransferred, response.StatusCode));
        }
        catch (OperationCanceledException)
        {
            return DataResult<DataTransferReceipt>.Cancelled();
        }
        catch (HttpRequestException exception)
        {
            return Failure<DataTransferReceipt>(DataErrorKind.NetworkUnavailable, "Upload failed.", exception);
        }
        catch (IOException exception)
        {
            return Failure<DataTransferReceipt>(DataErrorKind.LocalStorageError, "Upload source could not be read.", exception);
        }
    }

    /// <summary>
    /// Executes the download async operation.
    /// </summary>
    public async ValueTask<DataResult<DataTransferReceipt>> DownloadAsync(
        HttpRequestMessage request,
        Stream destination,
        Func<DataTransferProgress, CancellationToken, ValueTask>? progress = null,
        DataTransferOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
        {
            throw new ArgumentException("Download destination stream must be writable.", nameof(destination));
        }

        options ??= DataTransferOptions.Default;
        if (options.ResumeOffset > 0)
        {
            request.Headers.Range = new RangeHeaderValue(options.ResumeOffset, null);
        }

        var operationId = Guid.NewGuid();
        try
        {
            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return DataResult<DataTransferReceipt>.Failed(DataErrorMapper.FromHttpStatusCode(response.StatusCode));
            }

            if (options.ResumeOffset > 0 && response.StatusCode != HttpStatusCode.PartialContent)
            {
                if (options.RangeUnsupportedPolicy == DataRangeUnsupportedPolicy.Fail)
                {
                    return DataResult<DataTransferReceipt>.Failed(new DataError(
                        DataErrorKind.PolicyRejected,
                        "Server did not honor the requested byte range."));
                }

                if (!destination.CanSeek)
                {
                    return DataResult<DataTransferReceipt>.Failed(new DataError(
                        DataErrorKind.PolicyRejected,
                        "Restarting a range download requires a seekable destination stream."));
                }

                destination.SetLength(0);
                destination.Position = 0;
            }
            else if (options.ResumeOffset > 0)
            {
                var contentRange = response.Content.Headers.ContentRange;
                if (contentRange?.From != options.ResumeOffset)
                {
                    return DataResult<DataTransferReceipt>.Failed(new DataError(
                        DataErrorKind.PolicyRejected,
                        "Server returned a partial response for a different byte range."));
                }

                if (destination.CanSeek)
                {
                    if (destination.Length < options.ResumeOffset)
                    {
                        return DataResult<DataTransferReceipt>.Failed(new DataError(
                            DataErrorKind.PolicyRejected,
                            "The download destination is shorter than the requested resume offset."));
                    }

                    destination.SetLength(options.ResumeOffset);
                    destination.Position = options.ResumeOffset;
                }
            }

            var initialBytes = response.StatusCode == HttpStatusCode.PartialContent ? options.ResumeOffset : 0;
            var total = response.Content.Headers.ContentRange?.Length
                ?? (response.Content.Headers.ContentLength is { } contentLength ? contentLength + initialBytes : null);
            var reporter = new ProgressReporter(operationId, total, progress, options, _diagnostics, _timeProvider, initialBytes);
            await reporter.ReportAsync(initialBytes, DataTransferStage.Starting, force: true, cancellationToken).ConfigureAwait(false);
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var buffer = new byte[options.BufferSize];
            while (true)
            {
                var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                var nextBytes = reporter.BytesTransferred + read;
                if (total is { } declaredTotal && nextBytes > declaredTotal)
                {
                    return ProtocolLengthMismatch<DataTransferReceipt>(declaredTotal, nextBytes);
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                await reporter.ReportAsync(nextBytes, DataTransferStage.Transferring, force: false, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (total is { } expectedTotal && reporter.BytesTransferred != expectedTotal)
            {
                return ProtocolLengthMismatch<DataTransferReceipt>(expectedTotal, reporter.BytesTransferred);
            }

            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            await reporter.ReportAsync(reporter.BytesTransferred, DataTransferStage.Completed, force: true, cancellationToken)
                .ConfigureAwait(false);
            WriteCompleted(operationId, reporter.BytesTransferred);
            return DataResult<DataTransferReceipt>.Success(
                new DataTransferReceipt(operationId, reporter.BytesTransferred, response.StatusCode));
        }
        catch (OperationCanceledException)
        {
            return DataResult<DataTransferReceipt>.Cancelled();
        }
        catch (HttpRequestException exception)
        {
            return Failure<DataTransferReceipt>(DataErrorKind.NetworkUnavailable, "Download failed.", exception);
        }
        catch (IOException exception)
        {
            return Failure<DataTransferReceipt>(DataErrorKind.LocalStorageError, "Download destination could not be written.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Failure<DataTransferReceipt>(DataErrorKind.LocalStorageError, "Download destination is not accessible.", exception);
        }
        catch (NotSupportedException exception)
        {
            return Failure<DataTransferReceipt>(DataErrorKind.LocalStorageError, "Download destination does not support the required operation.", exception);
        }
    }

    /// <summary>
    /// Executes the download to temporary file async operation.
    /// </summary>
    public async ValueTask<DataResult<DataTemporaryFile>> DownloadToTemporaryFileAsync(
        HttpRequestMessage request,
        Func<DataTransferProgress, CancellationToken, ValueTask>? progress = null,
        DataTransferOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        options ??= DataTransferOptions.Default;
        if (options.ResumeOffset != 0)
        {
            throw new ArgumentException(
                "A new temporary file cannot resume an existing download. Use DownloadAsync with the existing file stream.",
                nameof(options));
        }

        var path = string.Empty;

        try
        {
            var directory = options.TemporaryDirectory ?? System.IO.Path.GetTempPath();
            Directory.CreateDirectory(directory);
            path = System.IO.Path.Combine(directory, $"atomui-city-data-{Guid.NewGuid():N}.tmp");
            DataResult<DataTransferReceipt> result;
            await using (var destination = new FileStream(
                             path,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             options.BufferSize,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                result = await DownloadAsync(request, destination, progress, options, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (!result.Succeeded)
            {
                DataTemporaryFile.TryDelete(path);
                return result.Cast<DataTemporaryFile>();
            }

            return DataResult<DataTemporaryFile>.Success(new DataTemporaryFile(path, result.Value!));
        }
        catch (OperationCanceledException)
        {
            if (path.Length > 0)
            {
                DataTemporaryFile.TryDelete(path);
            }

            return DataResult<DataTemporaryFile>.Cancelled();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            if (path.Length > 0)
            {
                DataTemporaryFile.TryDelete(path);
            }

            return Failure<DataTemporaryFile>(DataErrorKind.LocalStorageError, "Temporary download file could not be created.", exception);
        }
    }

    private DataResult<T> Failure<T>(DataErrorKind kind, string fallback, Exception exception) =>
        DataResult<T>.Failed(new DataError(
            kind,
            DataErrorMessage.FromException(exception, fallback),
            Exception: exception));

    private static DataResult<T> ProtocolLengthMismatch<T>(long expected, long actual) =>
        DataResult<T>.Failed(new DataError(
            DataErrorKind.StreamProtocolError,
            $"Download payload length mismatch. Expected {expected} bytes but received {actual}."));

    private void WriteCompleted(Guid operationId, long bytes)
    {
        DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
            DataDiagnosticIds.TransferCompleted,
            $"Data transfer '{operationId}' completed after {bytes} bytes.",
            DataDiagnosticSeverity.Info,
            operationId));
    }

    private sealed class ProgressReporter
    {
        private readonly Guid _operationId;
        private readonly long? _totalBytes;
        private readonly Func<DataTransferProgress, CancellationToken, ValueTask>? _progress;
        private readonly DataTransferOptions _options;
        private readonly IDataDiagnostics? _diagnostics;
        private readonly TimeProvider _timeProvider;
        private readonly long _startingBytes;
        private readonly long _startedTimestamp;
        private long _lastReportTimestamp;

        public ProgressReporter(
            Guid operationId,
            long? totalBytes,
            Func<DataTransferProgress, CancellationToken, ValueTask>? progress,
            DataTransferOptions options,
            IDataDiagnostics? diagnostics,
            TimeProvider timeProvider,
            long startingBytes = 0)
        {
            _operationId = operationId;
            _totalBytes = totalBytes;
            _progress = progress;
            _options = options;
            _diagnostics = diagnostics;
            _timeProvider = timeProvider;
            _startingBytes = startingBytes;
            _startedTimestamp = timeProvider.GetTimestamp();
            _lastReportTimestamp = _startedTimestamp;
            BytesTransferred = startingBytes;
        }

        public long BytesTransferred { get; private set; }

        public async ValueTask ReportAsync(
            long bytesTransferred,
            DataTransferStage stage,
            bool force,
            CancellationToken cancellationToken)
        {
            BytesTransferred = bytesTransferred;
            if (_progress is null)
            {
                return;
            }

            var now = _timeProvider.GetTimestamp();
            if (!force && _timeProvider.GetElapsedTime(_lastReportTimestamp, now) < _options.ProgressInterval)
            {
                return;
            }

            _lastReportTimestamp = now;
            var elapsed = _timeProvider.GetElapsedTime(_startedTimestamp, now).TotalSeconds;
            var speed = elapsed <= 0 ? 0 : (bytesTransferred - _startingBytes) / elapsed;
            double? percent = _totalBytes is > 0
                ? Math.Min(100, bytesTransferred * 100d / _totalBytes.Value)
                : null;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _progress(
                        new DataTransferProgress(
                            _operationId,
                            bytesTransferred,
                            _totalBytes,
                            percent,
                            speed,
                            stage),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
                    DataDiagnosticIds.TransferProgressFailed,
                    $"Data transfer progress handler failed: {exception.Message}",
                    DataDiagnosticSeverity.Warning,
                    _operationId,
                    ErrorKind: DataErrorKind.LocalStorageError));
            }
        }
    }

    private sealed class ProgressStreamContent(
        Stream source,
        long? length,
        int bufferSize,
        ProgressReporter reporter) : HttpContent
    {
        protected override bool TryComputeLength(out long computedLength)
        {
            computedLength = length ?? 0;
            return length.HasValue;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override async Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[bufferSize];
            while (true)
            {
                var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                await reporter.ReportAsync(
                        reporter.BytesTransferred + read,
                        DataTransferStage.Transferring,
                        force: false,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }
}
