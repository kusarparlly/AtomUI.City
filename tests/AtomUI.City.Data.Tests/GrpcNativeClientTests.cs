using Grpc.Core;

namespace AtomUI.City.Data.Tests;

public sealed class GrpcNativeClientTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void RpcExceptionMapperTreatsBlankStatusDetailAsAbsent(string detail)
    {
        var exception = new RpcException(new Status(StatusCode.DeadlineExceeded, detail));

        var error = GrpcRpcExceptionMapper.Map(exception);

        Assert.Equal(DataErrorKind.DeadlineExceeded, error.Kind);
        Assert.Equal("gRPC call failed with status 'DeadlineExceeded'.", error.Message);
        Assert.Same(exception, error.Exception);
    }
}
