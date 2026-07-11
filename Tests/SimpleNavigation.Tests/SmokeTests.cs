using SimpleNavigation.Common;

namespace SimpleNavigation.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void DialogParameters_RoundTripsValue()
    {
        var parameters = new DialogParameters("answer", 42);

        Assert.Equal(42, parameters.Get<int>("answer"));
    }
}
