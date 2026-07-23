using ERPSystem.Host.Middleware;
using FluentAssertions;

namespace ERPSystem.Tests.Middleware;

public class RequestIdHeaderTests
{
    [Fact]
    public void RequestIdHeader_IsExportedConstant()
    {
        RequestTrackingMiddleware.RequestIdHeader.Should().Be("X-Request-ID");
        RequestTrackingMiddleware.RequestIdItemKey.Should().Be("RequestId");
    }
}