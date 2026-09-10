using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Presentation;

public sealed class DefaultViewModelFactory : IViewModelFactory
{
    public async ValueTask<ViewModelLease> AcquireAsync(
        ViewModelAcquisitionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ReusableInstance is not null)
        {
            EnsureExpectedType(request, request.ReusableInstance);
            return ViewModelLease.Borrowed(request.ReusableInstance);
        }

        var scopeFactory = request.Services.GetService<IServiceScopeFactory>();
        if (scopeFactory is not null)
        {
            var scope = scopeFactory.CreateScope();
            try
            {
                var resolved = scope.ServiceProvider.GetService(request.ViewModelType);
                if (resolved is not null)
                {
                    EnsureExpectedType(request, resolved);
                    return ViewModelLease.ServiceScopeOwned(resolved, scope);
                }
            }
            catch
            {
                scope.Dispose();
                throw;
            }

            scope.Dispose();
        }

        if (request.Factory is not null)
        {
            var created = await request.Factory(request.Services, cancellationToken).ConfigureAwait(false);
            if (created is null)
            {
                throw new PresentationException(
                    PresentationError.ViewModelCreationFailed,
                    $"ViewModel factory for '{request.ViewModelType.FullName}' returned null.");
            }

            try
            {
                EnsureExpectedType(request, created);
            }
            catch
            {
                await DisposeCreatedInstanceAsync(created).ConfigureAwait(false);
                throw;
            }

            return ViewModelLease.EntryOwned(created);
        }

        throw new PresentationException(
            PresentationError.ViewModelNotFound,
            $"ViewModel '{request.ViewModelType.FullName}' is not registered and has no generated or application factory.");
    }

    private static void EnsureExpectedType(ViewModelAcquisitionRequest request, object instance)
    {
        if (!request.ViewModelType.IsInstanceOfType(instance))
        {
            throw new PresentationException(
                PresentationError.ViewModelCreationFailed,
                $"ViewModel acquisition returned '{instance.GetType().FullName}', expected '{request.ViewModelType.FullName}'.");
        }
    }

    private static async ValueTask DisposeCreatedInstanceAsync(object instance)
    {
        if (instance is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (instance is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
