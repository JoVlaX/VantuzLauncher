using Xunit;

namespace Vantuz.Core.Tests;

public class CommandResultTests
{
    [Fact]
    public void Success_Constructor_SetsProperties()
    {
        var result = new CommandResult(true);
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Failure_Constructor_SetsProperties()
    {
        var result = new CommandResult(false, "error");
        Assert.False(result.Success);
        Assert.Equal("error", result.ErrorMessage);
    }
}
