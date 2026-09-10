using AtomUI.City.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Presentation.Tests;

public sealed class ViewModelFactoryTests
{
    [Fact]
    public async Task RegisteredViewModelIsOwnedByCreatedServiceScope()
    {
        var services = new ServiceCollection();
        services.AddScoped<DisposableViewModel>();
        await using var provider = services.BuildServiceProvider();
        var factory = new DefaultViewModelFactory();

        var lease = await factory.AcquireAsync(
            new ViewModelAcquisitionRequest(typeof(DisposableViewModel), provider));
        var viewModel = Assert.IsType<DisposableViewModel>(lease.Instance);

        Assert.Equal(ViewModelOwnership.ServiceScopeOwned, lease.Ownership);
        await lease.DisposeAsync();
        Assert.True(viewModel.IsDisposed);
    }

    [Fact]
    public async Task ReusedViewModelIsBorrowedAndNotDisposed()
    {
        await using var provider = new ServiceCollection().BuildServiceProvider();
        var reusable = new DisposableViewModel();
        var factory = new DefaultViewModelFactory();

        var lease = await factory.AcquireAsync(
            new ViewModelAcquisitionRequest(
                typeof(DisposableViewModel),
                provider,
                ReusableInstance: reusable));
        await lease.DisposeAsync();

        Assert.Equal(ViewModelOwnership.Borrowed, lease.Ownership);
        Assert.False(reusable.IsDisposed);
    }

    [Fact]
    public async Task ExplicitFactoryInstanceIsEntryOwned()
    {
        await using var provider = new ServiceCollection().BuildServiceProvider();
        var factory = new DefaultViewModelFactory();

        var lease = await factory.AcquireAsync(
            new ViewModelAcquisitionRequest(
                typeof(DisposableViewModel),
                provider,
                (_, _) => ValueTask.FromResult<object>(new DisposableViewModel())));
        var viewModel = Assert.IsType<DisposableViewModel>(lease.Instance);
        await lease.DisposeAsync();

        Assert.Equal(ViewModelOwnership.EntryOwned, lease.Ownership);
        Assert.True(viewModel.IsDisposed);
    }

    private sealed class DisposableViewModel : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }
}
