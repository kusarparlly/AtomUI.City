using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtomUI.City.Templates;

namespace AtomUI.City.Cli;

internal static class PlanApplyTransaction
{
    internal const long MaximumPlanBytes = 4L * 1024 * 1024;
    internal const int MaximumChanges = 256;
    internal const int MaximumContentBytes = 1024 * 1024;

    private const string SchemaVersion = "1.0";
    private const string ExpectedStateAbsent = "absent";
    private const string Utf8EncodingName = "utf-8";
    private const string CreateChangeType = "create";
    private const string RollbackAction = "delete-if-content-hash-matches";

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions PlanJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32,
    };

    public static async ValueTask<PlanCreationResult> CreateAsync(
        GenerationTemplateOptions options,
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        string root;
        try
        {
            root = Path.GetFullPath(workspaceRoot);
            if (!Directory.Exists(root))
            {
                return PlanCreationResult.Failed(Error(
                    "AUCCLI0703",
                    "The plan workspace does not exist.",
                    workspaceRoot));
            }

            ValidateExistingPathChain(root, root);
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            return PlanCreationResult.Failed(Error(
                "AUCCLI0703",
                $"The plan workspace is invalid: {exception.Message}",
                workspaceRoot));
        }

        var previewBase = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "AtomUI.City.Cli",
            "preview"));
        var previewRoot = Path.Combine(previewBase, Guid.NewGuid().ToString("N"));
        TemplateRenderResult? renderResult = null;
        PlanDocument? plan = null;
        CliDiagnostic? failure = null;
        var templateDiagnostics = Array.Empty<CliDiagnostic>();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(previewRoot);
            ValidateOwnedPreviewPath(previewBase, previewRoot);

            var previewOptions = CloneOptions(options, previewRoot);
            renderResult = new GenerationTemplateRenderer().Render(previewOptions, cancellationToken);
            if (!renderResult.Succeeded)
            {
                templateDiagnostics = renderResult.Diagnostics
                    .Select(ToCliDiagnostic)
                    .ToArray();
            }
            else
            {
                var templatePlan = renderResult.Plan!;
                var changes = new List<PlanChange>(templatePlan.Changes.Count);
                for (var index = 0; index < templatePlan.Changes.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var templateChange = templatePlan.Changes[index];
                    if (!TryNormalizeRelativePath(templateChange.Path, out var relativePath, out var pathError))
                    {
                        failure = Error("AUCCLI0704", pathError, templateChange.Path);
                        break;
                    }

                    var previewPath = ResolveSafeTarget(previewRoot, relativePath, requireExistingLeaf: true);
                    var content = await File.ReadAllTextAsync(previewPath, StrictUtf8, cancellationToken).ConfigureAwait(false);
                    var contentBytes = StrictUtf8.GetBytes(content);
                    if (contentBytes.Length > MaximumContentBytes)
                    {
                        failure = Error(
                            "AUCCLI0707",
                            $"Plan content exceeds the {MaximumContentBytes}-byte per-change limit.",
                            relativePath);
                        break;
                    }

                    changes.Add(new PlanChange
                    {
                        ChangeId = $"change-{index + 1:0000}",
                        Type = CreateChangeType,
                        Path = relativePath,
                        ExpectedState = ExpectedStateAbsent,
                        Encoding = Utf8EncodingName,
                        Content = content,
                        ContentSha256 = Hash(contentBytes),
                    });
                }

                if (failure is null && changes.Count > MaximumChanges)
                {
                    failure = Error(
                        "AUCCLI0707",
                        $"Plan contains more than {MaximumChanges} changes.",
                        templatePlan.OperationId);
                }

                if (failure is null)
                {
                    var inputs = CreateInputs(options);
                    var workspaceId = CreateWorkspaceId(root);
                    var command = $"atomui city generate {GetKindName(options.Kind)}";
                    var rollback = changes
                        .Select(static change => new PlanRollback
                        {
                            ChangeId = change.ChangeId,
                            Action = RollbackAction,
                        })
                        .ToArray();
                    var payload = new OperationPayload
                    {
                        WorkspaceId = workspaceId,
                        Command = command,
                        Inputs = inputs,
                        Changes = changes.ToArray(),
                        BuildTargets = templatePlan.BuildTargets.ToArray(),
                        TestTargets = templatePlan.TestTargets.ToArray(),
                        DocsRequired = templatePlan.DocsRequired.ToArray(),
                        Risks = templatePlan.Risks.ToArray(),
                        Rollback = rollback,
                    };
                    var operationId = $"generate-{GetKindName(options.Kind)}-{ComputeOperationHash(payload)[..12]}";
                    plan = new PlanDocument
                    {
                        SchemaVersion = SchemaVersion,
                        OperationId = operationId,
                        WorkspaceId = workspaceId,
                        Command = command,
                        Inputs = inputs,
                        Changes = changes.ToArray(),
                        BuildTargets = payload.BuildTargets,
                        TestTargets = payload.TestTargets,
                        DocsRequired = payload.DocsRequired,
                        Risks = payload.Risks,
                        Rollback = rollback,
                    };
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            failure = Error("AUCCLI0714", "Plan generation was cancelled during preview.", "preview");
        }
        catch (Exception exception) when (IsFileSystemException(exception) || exception is DecoderFallbackException)
        {
            failure = Error(
                "AUCCLI0704",
                $"Plan preview could not be read safely: {exception.Message}",
                ".");
        }
        finally
        {
            try
            {
                if (Directory.Exists(previewRoot))
                {
                    ValidateOwnedPreviewPath(previewBase, previewRoot);
                    Directory.Delete(previewRoot, recursive: true);
                }
            }
            catch (Exception exception) when (IsFileSystemException(exception))
            {
                failure = Error(
                    "AUCCLI0715",
                    $"The isolated plan preview could not be cleaned up: {exception.Message}",
                    previewRoot);
            }
        }

        if (failure is not null)
        {
            return PlanCreationResult.Failed(failure);
        }

        if (templateDiagnostics.Length > 0)
        {
            return PlanCreationResult.Failed(templateDiagnostics);
        }

        if (plan is null)
        {
            return PlanCreationResult.Failed(Error(
                "AUCCLI0705",
                "Plan preview produced no plan.",
                ".city/plans"));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = SerializePlan(plan);
            if (bytes.LongLength > MaximumPlanBytes)
            {
                return PlanCreationResult.Failed(Error(
                    "AUCCLI0707",
                    $"Plan exceeds the {MaximumPlanBytes}-byte file limit.",
                    plan.OperationId));
            }

            var plansDirectory = Path.Combine(root, ".city", "plans");
            EnsureMetadataDirectory(root, plansDirectory);
            var planPath = Path.Combine(plansDirectory, plan.OperationId + ".json");
            var relativePlanPath = ToRelativePath(root, planPath);

            if (File.Exists(planPath))
            {
                var existing = await File.ReadAllBytesAsync(planPath, cancellationToken).ConfigureAwait(false);
                if (!existing.AsSpan().SequenceEqual(bytes))
                {
                    return PlanCreationResult.Failed(Error(
                        "AUCCLI0709",
                        "A plan with the same operation id already exists with different content.",
                        relativePlanPath));
                }
            }
            else
            {
                await WriteNewFileAtomicallyAsync(planPath, bytes, cancellationToken).ConfigureAwait(false);
            }

            return PlanCreationResult.Succeeded(
                plan,
                relativePlanPath,
                Hash(bytes));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return PlanCreationResult.Failed(Error(
                "AUCCLI0714",
                "Plan generation was cancelled before the plan was committed.",
                "plan-write"));
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            return PlanCreationResult.Failed(Error(
                "AUCCLI0705",
                $"The plan file could not be written atomically: {exception.Message}",
                $".city/plans/{plan.OperationId}.json"));
        }
    }

    public static async ValueTask<ApplyTransactionResult> ApplyAsync(
        string planFile,
        string workspaceRoot,
        CancellationToken cancellationToken,
        Action<string, int>? beforeCommit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        if (string.IsNullOrWhiteSpace(planFile))
        {
            return ApplyTransactionResult.Failed(Error(
                "AUCCLI0706",
                "A plan file is required.",
                "plan-file"));
        }

        string root;
        string planPath;
        string relativePlanPath;
        byte[] planBytes;
        PlanDocument plan;
        string planDigest;
        try
        {
            root = Path.GetFullPath(workspaceRoot);
            if (!Directory.Exists(root))
            {
                return ApplyTransactionResult.Failed(Error(
                    "AUCCLI0703",
                    "The apply workspace does not exist.",
                    workspaceRoot));
            }

            planPath = Path.GetFullPath(planFile, root);
            var plansDirectory = Path.GetFullPath(Path.Combine(root, ".city", "plans"));
            if (!IsStrictDescendant(plansDirectory, planPath))
            {
                return ApplyTransactionResult.Failed(Error(
                    "AUCCLI0704",
                    "The plan file must be inside the workspace .city/plans directory.",
                    planFile));
            }

            ValidateExistingPathChain(root, planPath);
            relativePlanPath = ToRelativePath(root, planPath);
            var info = new FileInfo(planPath);
            if (!info.Exists)
            {
                return ApplyTransactionResult.Failed(Error(
                    "AUCCLI0706",
                    "The plan file does not exist.",
                    relativePlanPath));
            }

            if (info.Length > MaximumPlanBytes)
            {
                return ApplyTransactionResult.Failed(Error(
                    "AUCCLI0707",
                    $"The plan file exceeds the {MaximumPlanBytes}-byte limit.",
                    relativePlanPath));
            }

            cancellationToken.ThrowIfCancellationRequested();
            planBytes = await File.ReadAllBytesAsync(planPath, cancellationToken).ConfigureAwait(false);
            ValidateNoDuplicateJsonProperties(planBytes);
            plan = JsonSerializer.Deserialize<PlanDocument>(planBytes, PlanJsonOptions)
                ?? throw new JsonException("The plan document is null.");
            planDigest = Hash(planBytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ApplyTransactionResult.Failed(Error(
                "AUCCLI0714",
                "Plan apply was cancelled while reading the plan.",
                "read"));
        }
        catch (JsonException exception)
        {
            return ApplyTransactionResult.Failed(Error(
                "AUCCLI0707",
                $"The plan schema is invalid: {exception.Message}",
                planFile));
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            return ApplyTransactionResult.Failed(Error(
                "AUCCLI0706",
                $"The plan file could not be read: {exception.Message}",
                planFile));
        }

        var validationDiagnostics = ValidatePlan(plan, root, planPath);
        if (validationDiagnostics.Count > 0)
        {
            return ApplyTransactionResult.Failed(validationDiagnostics.ToArray());
        }

        var transactionsRoot = Path.Combine(root, ".city", "transactions");
        var transactionRoot = Path.Combine(transactionsRoot, plan.OperationId);
        var stagingRoot = Path.Combine(transactionRoot, "staging");
        var journalPath = Path.Combine(transactionRoot, "journal.json");
        var lockPath = Path.Combine(transactionsRoot, "apply.lock");
        FileStream? applyLock = null;

        try
        {
            EnsureMetadataDirectory(root, transactionsRoot);
            try
            {
                applyLock = new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.WriteThrough);
            }
            catch (IOException exception)
            {
                return ApplyTransactionResult.Failed(Error(
                    "AUCCLI0711",
                    $"Another apply transaction owns the workspace lock: {exception.Message}",
                    ToRelativePath(root, lockPath)));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var existingJournal = await TryReadJournalAsync(journalPath, cancellationToken).ConfigureAwait(false);
            if (existingJournal is not null &&
                existingJournal.State == "completed")
            {
                var replayDiagnostic = await ValidateCompletedJournalAsync(
                        existingJournal,
                        plan,
                        planDigest,
                        root,
                        journalPath,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (replayDiagnostic is null)
                {
                    return ApplyTransactionResult.Succeeded(
                        plan.OperationId,
                        relativePlanPath,
                        planDigest,
                        "already-completed",
                        []);
                }

                return ApplyTransactionResult.Failed(replayDiagnostic);
            }

            foreach (var change in plan.Changes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var target = ResolveSafeTarget(root, change.Path, requireExistingLeaf: false);
                if (File.Exists(target) || Directory.Exists(target))
                {
                    return ApplyTransactionResult.Failed(Error(
                        "AUCCLI0710",
                        "A create-only plan target is no longer absent.",
                        change.Path));
                }
            }

            if (Directory.Exists(stagingRoot))
            {
                ValidateTransactionPath(transactionsRoot, stagingRoot);
                Directory.Delete(stagingRoot, recursive: true);
            }

            Directory.CreateDirectory(stagingRoot);
            foreach (var change in plan.Changes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var stagePath = ResolveSafeTarget(stagingRoot, change.Path, requireExistingLeaf: false);
                Directory.CreateDirectory(Path.GetDirectoryName(stagePath)!);
                var contentBytes = StrictUtf8.GetBytes(change.Content);
                await WriteNewFileAsync(stagePath, contentBytes, cancellationToken).ConfigureAwait(false);
                var stagedBytes = await File.ReadAllBytesAsync(stagePath, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(Hash(stagedBytes), change.ContentSha256, StringComparison.Ordinal))
                {
                    TryDeleteDirectory(stagingRoot);
                    return ApplyTransactionResult.Failed(Error(
                        "AUCCLI0709",
                        "Staged content no longer matches the validated plan.",
                        change.Path));
                }
            }

            var journal = new TransactionJournal
            {
                SchemaVersion = SchemaVersion,
                OperationId = plan.OperationId,
                PlanDigest = planDigest,
                State = "staged",
                CommittedPaths = [],
            };
            await WriteJournalAsync(journalPath, journal).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var committed = new List<PlanChange>();
            var createdDirectories = new List<string>();
            Exception? commitFailure = null;
            string? failurePath = null;
            journal.State = "committing";
            await WriteJournalAsync(journalPath, journal).ConfigureAwait(false);

            for (var index = 0; index < plan.Changes.Length; index++)
            {
                var change = plan.Changes[index];
                try
                {
                    var target = ResolveSafeTarget(root, change.Path, requireExistingLeaf: false);
                    failurePath = change.Path;
                    EnsureTargetDirectory(root, Path.GetDirectoryName(target)!, createdDirectories);
                    if (File.Exists(target) || Directory.Exists(target))
                    {
                        throw new IOException("The target appeared after preflight.");
                    }

                    beforeCommit?.Invoke(change.Path, index);
                    var stagePath = ResolveSafeTarget(stagingRoot, change.Path, requireExistingLeaf: true);
                    File.Move(stagePath, target, overwrite: false);
                    committed.Add(change);
                    journal.CommittedPaths = committed.Select(static item => item.Path).ToArray();
                    await WriteJournalAsync(journalPath, journal).ConfigureAwait(false);
                }
                catch (Exception exception) when (IsFileSystemException(exception))
                {
                    commitFailure = exception;
                    break;
                }
            }

            if (commitFailure is null)
            {
                try
                {
                    journal.State = "completed";
                    journal.CommittedPaths = plan.Changes.Select(static item => item.Path).ToArray();
                    await WriteJournalAsync(journalPath, journal).ConfigureAwait(false);
                }
                catch (Exception exception) when (IsFileSystemException(exception))
                {
                    commitFailure = exception;
                    failurePath = ToRelativePath(root, journalPath);
                }
            }

            if (commitFailure is not null)
            {
                var diagnostics = new List<CliDiagnostic>
                {
                    Error(
                        "AUCCLI0712",
                        $"Apply commit failed: {commitFailure.Message}",
                        failurePath),
                };
                journal.State = "rolling-back";
                await TryWriteJournalAsync(journalPath, journal).ConfigureAwait(false);
                Rollback(root, committed, createdDirectories, diagnostics, commitFailure);
                journal.State = diagnostics.Any(static item => item.Code == "AUCCLI0713")
                    ? "rollback-failed"
                    : "rolled-back";
                journal.CommittedPaths = diagnostics.Any(static item => item.Code == "AUCCLI0713")
                    ? committed.Select(static item => item.Path).ToArray()
                    : [];
                await TryWriteJournalAsync(journalPath, journal).ConfigureAwait(false);
                return ApplyTransactionResult.Failed(diagnostics.ToArray());
            }

            TryDeleteDirectory(stagingRoot);

            if (cancellationToken.IsCancellationRequested)
            {
                return ApplyTransactionResult.FailedWithState(
                    plan.OperationId,
                    relativePlanPath,
                    planDigest,
                    "completed",
                    plan.Changes.Select(static item => item.Path).ToArray(),
                    Error(
                        "AUCCLI0714",
                        "Cancellation was requested during commit; commit completed before cancellation was reported.",
                        "commit"));
            }

            return ApplyTransactionResult.Succeeded(
                plan.OperationId,
                relativePlanPath,
                planDigest,
                "completed",
                plan.Changes.Select(static item => item.Path).ToArray());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryDeleteDirectory(stagingRoot);
            return ApplyTransactionResult.FailedWithState(
                plan.OperationId,
                relativePlanPath,
                planDigest,
                "cancelled-before-commit",
                [],
                Error(
                    "AUCCLI0714",
                    "Plan apply was cancelled before commit.",
                    "staging"));
        }
        catch (Exception exception) when (IsFileSystemException(exception) || exception is JsonException)
        {
            TryDeleteDirectory(stagingRoot);
            return ApplyTransactionResult.Failed(Error(
                "AUCCLI0712",
                $"Apply transaction failed before completion: {exception.Message}",
                plan.OperationId));
        }
        finally
        {
            applyLock?.Dispose();
        }
    }

    private static IReadOnlyList<CliDiagnostic> ValidatePlan(
        PlanDocument plan,
        string root,
        string planPath)
    {
        var diagnostics = new List<CliDiagnostic>();
        if (plan.SchemaVersion != SchemaVersion)
        {
            diagnostics.Add(Error("AUCCLI0707", "Only plan schema version 1.0 is supported.", plan.SchemaVersion));
            return diagnostics;
        }

        if (string.IsNullOrWhiteSpace(plan.OperationId) ||
            string.IsNullOrWhiteSpace(plan.WorkspaceId) ||
            string.IsNullOrWhiteSpace(plan.Command) ||
            plan.Inputs is null ||
            plan.Changes is null ||
            plan.BuildTargets is null ||
            plan.TestTargets is null ||
            plan.DocsRequired is null ||
            plan.Risks is null ||
            plan.Rollback is null)
        {
            diagnostics.Add(Error("AUCCLI0707", "The plan is missing one or more required fields.", ToRelativePath(root, planPath)));
            return diagnostics;
        }

        if (string.IsNullOrWhiteSpace(plan.Inputs.Kind) ||
            string.IsNullOrWhiteSpace(plan.Inputs.Name) ||
            string.IsNullOrWhiteSpace(plan.Inputs.ProjectName) ||
            string.IsNullOrWhiteSpace(plan.Inputs.RootNamespace) ||
            plan.Inputs.Cultures is null ||
            plan.Inputs.ModuleDependencies is null ||
            plan.Inputs.Cultures.Any(string.IsNullOrWhiteSpace) ||
            plan.Inputs.ModuleDependencies.Any(string.IsNullOrWhiteSpace) ||
            plan.BuildTargets.Any(string.IsNullOrWhiteSpace) ||
            plan.TestTargets.Any(string.IsNullOrWhiteSpace) ||
            plan.DocsRequired.Any(string.IsNullOrWhiteSpace) ||
            plan.Risks.Any(static value => value is null))
        {
            diagnostics.Add(Error("AUCCLI0707", "The plan contains a null or empty required value.", plan.OperationId));
            return diagnostics;
        }

        var expectedWorkspaceId = CreateWorkspaceId(root);
        if (!string.Equals(plan.WorkspaceId, expectedWorkspaceId, StringComparison.Ordinal))
        {
            diagnostics.Add(Error("AUCCLI0708", "The plan belongs to a different workspace.", ToRelativePath(root, planPath)));
        }

        if (plan.Changes.Length is < 1 or > MaximumChanges)
        {
            diagnostics.Add(Error("AUCCLI0707", $"Plan changes must contain between 1 and {MaximumChanges} entries.", plan.OperationId));
            return diagnostics;
        }

        var hasValidKind = TryGetKind(plan.Inputs.Kind, out var kind);
        if (!hasValidKind ||
            plan.Command != $"atomui city generate {(hasValidKind ? GetKindName(kind) : plan.Inputs.Kind)}")
        {
            diagnostics.Add(Error("AUCCLI0702", "The plan operation or generation kind is unsupported.", plan.Command));
        }

        var expectedFileName = plan.OperationId + ".json";
        if (!Path.GetFileName(planPath).Equals(expectedFileName, PathComparison()))
        {
            diagnostics.Add(Error("AUCCLI0707", "The plan file name must match operationId.", ToRelativePath(root, planPath)));
        }

        var paths = new HashSet<string>(PathComparer());
        var changeIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < plan.Changes.Length; index++)
        {
            var change = plan.Changes[index];
            var expectedChangeId = $"change-{index + 1:0000}";
            if (change is null ||
                change.Content is null ||
                string.IsNullOrWhiteSpace(change.ContentSha256) ||
                change.ChangeId != expectedChangeId ||
                change.Type != CreateChangeType ||
                change.ExpectedState != ExpectedStateAbsent ||
                change.Encoding != Utf8EncodingName)
            {
                diagnostics.Add(Error("AUCCLI0707", "A plan change contains an unsupported or invalid field.", change?.Path));
                continue;
            }

            if (!changeIds.Add(change.ChangeId))
            {
                diagnostics.Add(Error("AUCCLI0707", "The plan contains a duplicate change id.", change.ChangeId));
            }

            if (!TryNormalizeRelativePath(change.Path, out var normalizedPath, out var pathError) ||
                normalizedPath != change.Path)
            {
                diagnostics.Add(Error("AUCCLI0704", pathError, change.Path));
                continue;
            }

            if (!paths.Add(normalizedPath))
            {
                diagnostics.Add(Error("AUCCLI0704", "The plan contains duplicate or case-colliding target paths.", normalizedPath));
            }

            try
            {
                _ = ResolveSafeTarget(root, normalizedPath, requireExistingLeaf: false);
            }
            catch (Exception exception) when (IsFileSystemException(exception))
            {
                diagnostics.Add(Error("AUCCLI0704", exception.Message, normalizedPath));
            }

            byte[] contentBytes;
            try
            {
                contentBytes = StrictUtf8.GetBytes(change.Content);
            }
            catch (EncoderFallbackException)
            {
                diagnostics.Add(Error("AUCCLI0709", "Plan content is not valid UTF-8 text.", normalizedPath));
                continue;
            }

            if (contentBytes.Length > MaximumContentBytes ||
                !string.Equals(Hash(contentBytes), change.ContentSha256, StringComparison.Ordinal))
            {
                diagnostics.Add(Error("AUCCLI0709", "Plan content hash or size is invalid.", normalizedPath));
            }
        }

        ValidateDeclaredPaths(plan.BuildTargets, "buildTargets", root, diagnostics);
        ValidateDeclaredPaths(plan.TestTargets, "testTargets", root, diagnostics);
        ValidateDeclaredPaths(plan.DocsRequired, "docsRequired", root, diagnostics);

        if (plan.Rollback.Length != plan.Changes.Length)
        {
            diagnostics.Add(Error("AUCCLI0707", "Rollback entries must correspond one-to-one with changes.", plan.OperationId));
        }
        else
        {
            for (var index = 0; index < plan.Rollback.Length; index++)
            {
                var rollback = plan.Rollback[index];
                if (rollback is null ||
                    plan.Changes[index] is null ||
                    rollback.ChangeId != plan.Changes[index].ChangeId ||
                    rollback.Action != RollbackAction)
                {
                    diagnostics.Add(Error("AUCCLI0707", "A rollback entry is invalid or out of order.", rollback?.ChangeId));
                }
            }
        }

        var payload = new OperationPayload
        {
            WorkspaceId = plan.WorkspaceId,
            Command = plan.Command,
            Inputs = plan.Inputs,
            Changes = plan.Changes,
            BuildTargets = plan.BuildTargets,
            TestTargets = plan.TestTargets,
            DocsRequired = plan.DocsRequired,
            Risks = plan.Risks,
            Rollback = plan.Rollback,
        };
        var expectedOperationId = hasValidKind
            ? $"generate-{GetKindName(kind)}-{ComputeOperationHash(payload)[..12]}"
            : string.Empty;
        if (!hasValidKind || !string.Equals(plan.OperationId, expectedOperationId, StringComparison.Ordinal))
        {
            diagnostics.Add(Error("AUCCLI0709", "The operation id does not match the canonical plan payload.", plan.OperationId));
        }

        return diagnostics;
    }

    private static void ValidateDeclaredPaths(
        IEnumerable<string> paths,
        string fieldName,
        string root,
        List<CliDiagnostic> diagnostics)
    {
        foreach (var path in paths)
        {
            if (!TryNormalizeRelativePath(path, out var normalizedPath, out var pathError) ||
                normalizedPath != path)
            {
                diagnostics.Add(Error(
                    "AUCCLI0704",
                    $"{fieldName} contains an invalid path: {pathError}",
                    path));
                continue;
            }

            try
            {
                _ = ResolveSafeTarget(root, normalizedPath, requireExistingLeaf: false);
            }
            catch (Exception exception) when (IsFileSystemException(exception))
            {
                diagnostics.Add(Error(
                    "AUCCLI0704",
                    $"{fieldName} contains an unsafe path: {exception.Message}",
                    normalizedPath));
            }
        }
    }

    private static async ValueTask<CliDiagnostic?> ValidateCompletedJournalAsync(
        TransactionJournal journal,
        PlanDocument plan,
        string planDigest,
        string root,
        string journalPath,
        CancellationToken cancellationToken)
    {
        var journalTarget = ToRelativePath(root, journalPath);
        if (journal.SchemaVersion != SchemaVersion ||
            journal.OperationId != plan.OperationId ||
            journal.PlanDigest != planDigest ||
            journal.CommittedPaths is null ||
            !journal.CommittedPaths.SequenceEqual(
                plan.Changes.Select(static change => change.Path),
                StringComparer.Ordinal))
        {
            return Error(
                "AUCCLI0709",
                "The completed transaction journal does not match the validated plan.",
                journalTarget);
        }

        foreach (var change in plan.Changes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var target = ResolveSafeTarget(root, change.Path, requireExistingLeaf: true);
                var bytes = await File.ReadAllBytesAsync(target, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(Hash(bytes), change.ContentSha256, StringComparison.Ordinal))
                {
                    return Error(
                        "AUCCLI0709",
                        "A completed transaction target no longer matches the plan content hash.",
                        change.Path);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (IsFileSystemException(exception))
            {
                return Error(
                    "AUCCLI0709",
                    $"A completed transaction target cannot be verified: {exception.Message}",
                    change.Path);
            }
        }

        return null;
    }

    private static void Rollback(
        string root,
        IReadOnlyList<PlanChange> committed,
        IReadOnlyList<string> createdDirectories,
        List<CliDiagnostic> diagnostics,
        Exception originalFailure)
    {
        foreach (var change in committed.Reverse())
        {
            try
            {
                var target = ResolveSafeTarget(root, change.Path, requireExistingLeaf: true);
                var actualHash = Hash(File.ReadAllBytes(target));
                if (!string.Equals(actualHash, change.ContentSha256, StringComparison.Ordinal))
                {
                    throw new IOException("Committed content changed before rollback.");
                }

                File.Delete(target);
            }
            catch (Exception exception) when (IsFileSystemException(exception))
            {
                diagnostics.Add(Error(
                    "AUCCLI0713",
                    $"Rollback failed after '{originalFailure.Message}': {exception.Message}",
                    change.Path));
            }
        }

        foreach (var directory in createdDirectories.Reverse())
        {
            try
            {
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }
            catch (Exception exception) when (IsFileSystemException(exception))
            {
                diagnostics.Add(Error(
                    "AUCCLI0713",
                    $"Rollback could not remove a transaction-created directory: {exception.Message}",
                    ToRelativePath(root, directory)));
            }
        }
    }

    private static GenerationTemplateOptions CloneOptions(
        GenerationTemplateOptions options,
        string outputPath) => new()
        {
            Kind = options.Kind,
            Name = options.Name,
            ProjectName = options.ProjectName,
            RootNamespace = options.RootNamespace,
            OutputPath = outputPath,
            RoutePath = options.RoutePath,
            Cultures = options.Cultures,
            ModuleDependencies = options.ModuleDependencies,
            IncludeTests = options.IncludeTests,
            ReloadableConfiguration = options.ReloadableConfiguration,
        };

    private static PlanInputs CreateInputs(GenerationTemplateOptions options) => new()
    {
        Kind = GetKindName(options.Kind),
        Name = options.Name.Replace('\\', '/'),
        ProjectName = options.ProjectName,
        RootNamespace = options.RootNamespace,
        IncludeTests = options.IncludeTests,
        Route = options.RoutePath,
        Cultures = options.Cultures.ToArray(),
        ModuleDependencies = options.ModuleDependencies.ToArray(),
        ReloadableConfiguration = options.ReloadableConfiguration,
    };

    private static byte[] SerializePlan(PlanDocument plan)
    {
        var json = JsonSerializer.Serialize(plan, PlanJsonOptions).Replace("\r\n", "\n", StringComparison.Ordinal);
        return StrictUtf8.GetBytes(json + "\n");
    }

    private static string ComputeOperationHash(OperationPayload payload)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, PlanJsonOptions);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private static string CreateWorkspaceId(string root)
    {
        var normalized = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace('\\', '/');
        if (OperatingSystem.IsWindows())
        {
            normalized = normalized.ToUpperInvariant();
        }

        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(StrictUtf8.GetBytes(normalized)));
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static async ValueTask WriteNewFileAtomicallyAsync(
        string path,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        var temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await WriteNewFileAsync(temporaryPath, bytes, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, path, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static async ValueTask WriteNewFileAsync(
        string path,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }

    private static async ValueTask<TransactionJournal?> TryReadJournalAsync(
        string journalPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(journalPath))
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(journalPath, cancellationToken).ConfigureAwait(false);
        ValidateNoDuplicateJsonProperties(bytes);
        return JsonSerializer.Deserialize<TransactionJournal>(bytes, PlanJsonOptions)
            ?? throw new JsonException("Transaction journal is null.");
    }

    private static async ValueTask WriteJournalAsync(
        string journalPath,
        TransactionJournal journal)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(journalPath)!);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(journal, PlanJsonOptions);
        var temporaryPath = journalPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await WriteNewFileAsync(temporaryPath, bytes, CancellationToken.None).ConfigureAwait(false);
            File.Move(temporaryPath, journalPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static async ValueTask TryWriteJournalAsync(
        string journalPath,
        TransactionJournal journal)
    {
        try
        {
            await WriteJournalAsync(journalPath, journal).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            // The caller already reports the primary commit/rollback failure.
        }
    }

    private static void EnsureMetadataDirectory(string root, string directory)
    {
        var fullDirectory = Path.GetFullPath(directory);
        if (!IsDescendantOrSame(root, fullDirectory))
        {
            throw new IOException("Metadata directory escapes the workspace.");
        }

        var current = root;
        foreach (var segment in Path.GetRelativePath(root, fullDirectory)
                     .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (Directory.Exists(current))
            {
                RejectReparsePoint(current);
            }
            else
            {
                Directory.CreateDirectory(current);
            }
        }
    }

    private static void EnsureTargetDirectory(
        string root,
        string directory,
        List<string> createdDirectories)
    {
        if (!IsDescendantOrSame(root, directory))
        {
            throw new IOException("Target directory escapes the workspace.");
        }

        var missing = new Stack<string>();
        var current = directory;
        while (!Directory.Exists(current))
        {
            missing.Push(current);
            current = Path.GetDirectoryName(current)
                ?? throw new IOException("Target directory has no workspace ancestor.");
        }

        ValidateExistingPathChain(root, current);
        while (missing.Count > 0)
        {
            var path = missing.Pop();
            Directory.CreateDirectory(path);
            createdDirectories.Add(path);
        }
    }

    private static string ResolveSafeTarget(
        string root,
        string relativePath,
        bool requireExistingLeaf)
    {
        if (!TryNormalizeRelativePath(relativePath, out var normalized, out var error) ||
            normalized != relativePath)
        {
            throw new IOException(error);
        }

        var target = Path.GetFullPath(Path.Combine([root, .. normalized.Split('/')]));
        if (!IsStrictDescendant(root, target))
        {
            throw new IOException("Target path escapes the workspace.");
        }

        ValidateExistingPathChain(root, target);
        if (requireExistingLeaf && !File.Exists(target))
        {
            throw new IOException("Expected transaction file does not exist.");
        }

        return target;
    }

    private static void ValidateExistingPathChain(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(path);
        if (!IsDescendantOrSame(fullRoot, fullPath))
        {
            throw new IOException("Path escapes its owned root.");
        }

        RejectReparsePoint(fullRoot);
        var relative = Path.GetRelativePath(fullRoot, fullPath);
        var current = fullRoot;
        foreach (var segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current))
            {
                break;
            }

            RejectReparsePoint(current);
        }
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Path contains a symbolic link or reparse point.");
        }
    }

    private static void ValidateOwnedPreviewPath(string previewBase, string previewRoot)
    {
        if (!IsStrictDescendant(previewBase, previewRoot))
        {
            throw new IOException("Preview root is outside the CLI preview base.");
        }

        ValidateExistingPathChain(previewBase, previewRoot);
    }

    private static void ValidateTransactionPath(string transactionsRoot, string path)
    {
        if (!IsStrictDescendant(transactionsRoot, path))
        {
            throw new IOException("Transaction path escapes the transaction root.");
        }

        ValidateExistingPathChain(transactionsRoot, path);
    }

    private static bool TryNormalizeRelativePath(
        string? path,
        out string normalized,
        out string error)
    {
        normalized = string.Empty;
        error = "Plan path is invalid.";
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\0'))
        {
            return false;
        }

        var separated = path.Replace('\\', '/');
        if (separated.StartsWith("/", StringComparison.Ordinal) ||
            separated.StartsWith("//", StringComparison.Ordinal) ||
            Path.IsPathRooted(separated))
        {
            return false;
        }

        var segments = separated.Split('/', StringSplitOptions.None);
        if (segments.Length == 0 || segments.Any(static segment =>
                string.IsNullOrEmpty(segment) ||
                segment is "." or ".." ||
                segment.Any(static character => char.IsControl(character) || character is '<' or '>' or ':' or '"' or '|' or '?' or '*') ||
                segment.EndsWith(' ') ||
                segment.EndsWith('.') ||
                IsReservedWindowsName(segment)))
        {
            return false;
        }

        normalized = string.Join('/', segments);
        error = string.Empty;
        return true;
    }

    private static bool IsReservedWindowsName(string segment)
    {
        var stem = segment.Split('.')[0];
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("NUL", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return stem.Length == 4 &&
               (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
               stem[3] is >= '1' and <= '9';
    }

    private static void ValidateNoDuplicateJsonProperties(ReadOnlySpan<byte> bytes)
    {
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 32,
        });
        var objectProperties = new Stack<HashSet<string>>();
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    objectProperties.Push(new HashSet<string>(StringComparer.Ordinal));
                    break;
                case JsonTokenType.EndObject:
                    objectProperties.Pop();
                    break;
                case JsonTokenType.PropertyName:
                    var name = reader.GetString()!;
                    if (objectProperties.Count == 0 || !objectProperties.Peek().Add(name))
                    {
                        throw new JsonException($"Duplicate JSON property '{name}' is not allowed.");
                    }

                    break;
            }
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            // A completed journal is authoritative; stale staging is inert metadata.
        }
    }

    private static string ToRelativePath(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    private static bool IsDescendantOrSame(string root, string path)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
        return relative == "." ||
               (!relative.Equals("..", StringComparison.Ordinal) &&
                !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
                !Path.IsPathRooted(relative));
    }

    private static bool IsStrictDescendant(string root, string path) =>
        IsDescendantOrSame(root, path) &&
        !Path.GetFullPath(root).Equals(Path.GetFullPath(path), PathComparison());

    private static StringComparison PathComparison() =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static StringComparer PathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static bool IsFileSystemException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or PathTooLongException;

    private static string GetKindName(GenerationTemplateKind kind) => kind switch
    {
        GenerationTemplateKind.Module => "module",
        GenerationTemplateKind.Page => "page",
        GenerationTemplateKind.Test => "test",
        GenerationTemplateKind.Configuration => "config",
        GenerationTemplateKind.Localization => "localization",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported generation kind."),
    };

    private static bool TryGetKind(string? value, out GenerationTemplateKind kind)
    {
        switch (value)
        {
            case "module":
                kind = GenerationTemplateKind.Module;
                return true;
            case "page":
                kind = GenerationTemplateKind.Page;
                return true;
            case "test":
                kind = GenerationTemplateKind.Test;
                return true;
            case "config":
                kind = GenerationTemplateKind.Configuration;
                return true;
            case "localization":
                kind = GenerationTemplateKind.Localization;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static CliDiagnostic ToCliDiagnostic(TemplateDiagnostic diagnostic)
    {
        var target = diagnostic.Context.TryGetValue("path", out var path)
            ? path?.ToString()
            : diagnostic.Context.TryGetValue("rawValue", out var rawValue)
                ? rawValue?.ToString()
                : null;
        return Error(diagnostic.Code, diagnostic.Message, target);
    }

    private static CliDiagnostic Error(string code, string message, string? target) =>
        CliDiagnostic.Error(code, message, target);

    internal sealed class PlanDocument
    {
        public required string SchemaVersion { get; init; }
        public required string OperationId { get; init; }
        public required string WorkspaceId { get; init; }
        public required string Command { get; init; }
        public required PlanInputs Inputs { get; init; }
        public required PlanChange[] Changes { get; init; }
        public required string[] BuildTargets { get; init; }
        public required string[] TestTargets { get; init; }
        public required string[] DocsRequired { get; init; }
        public required string[] Risks { get; init; }
        public required PlanRollback[] Rollback { get; init; }
    }

    internal sealed class PlanInputs
    {
        public required string Kind { get; init; }
        public required string Name { get; init; }
        public required string ProjectName { get; init; }
        public required string RootNamespace { get; init; }
        public required bool IncludeTests { get; init; }
        public string? Route { get; init; }
        public required string[] Cultures { get; init; }
        public required string[] ModuleDependencies { get; init; }
        public required bool ReloadableConfiguration { get; init; }
    }

    internal sealed class PlanChange
    {
        public required string ChangeId { get; init; }
        public required string Type { get; init; }
        public required string Path { get; init; }
        public required string ExpectedState { get; init; }
        public required string Encoding { get; init; }
        public required string Content { get; init; }
        public required string ContentSha256 { get; init; }
    }

    internal sealed class PlanRollback
    {
        public required string ChangeId { get; init; }
        public required string Action { get; init; }
    }

    private sealed class OperationPayload
    {
        public required string WorkspaceId { get; init; }
        public required string Command { get; init; }
        public required PlanInputs Inputs { get; init; }
        public required PlanChange[] Changes { get; init; }
        public required string[] BuildTargets { get; init; }
        public required string[] TestTargets { get; init; }
        public required string[] DocsRequired { get; init; }
        public required string[] Risks { get; init; }
        public required PlanRollback[] Rollback { get; init; }
    }

    private sealed class TransactionJournal
    {
        public required string SchemaVersion { get; init; }
        public required string OperationId { get; init; }
        public required string PlanDigest { get; init; }
        public required string State { get; set; }
        public required string[] CommittedPaths { get; set; }
    }
}

internal sealed record PlanCreationResult(
    bool Success,
    PlanApplyTransaction.PlanDocument? Plan,
    string? PlanFile,
    string? PlanDigest,
    IReadOnlyList<CliDiagnostic> Diagnostics)
{
    public static PlanCreationResult Succeeded(
        PlanApplyTransaction.PlanDocument plan,
        string planFile,
        string planDigest) => new(true, plan, planFile, planDigest, []);

    public static PlanCreationResult Failed(params CliDiagnostic[] diagnostics) =>
        new(false, null, null, null, Array.AsReadOnly(diagnostics));
}

internal sealed record ApplyTransactionResult(
    bool Success,
    string? OperationId,
    string? PlanFile,
    string? PlanDigest,
    string? TransactionState,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<CliDiagnostic> Diagnostics)
{
    public static ApplyTransactionResult Succeeded(
        string operationId,
        string planFile,
        string planDigest,
        string transactionState,
        IReadOnlyList<string> changedFiles) =>
        new(true, operationId, planFile, planDigest, transactionState, Array.AsReadOnly(changedFiles.ToArray()), []);

    public static ApplyTransactionResult Failed(params CliDiagnostic[] diagnostics) =>
        new(false, null, null, null, null, [], Array.AsReadOnly(diagnostics));

    public static ApplyTransactionResult FailedWithState(
        string operationId,
        string planFile,
        string planDigest,
        string transactionState,
        IReadOnlyList<string> changedFiles,
        params CliDiagnostic[] diagnostics) =>
        new(
            false,
            operationId,
            planFile,
            planDigest,
            transactionState,
            Array.AsReadOnly(changedFiles.ToArray()),
            Array.AsReadOnly(diagnostics));
}
