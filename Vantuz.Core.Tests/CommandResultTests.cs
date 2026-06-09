using Xunit;

namespace Vantuz.Core.Tests;
/// F_doc: {CommandResultTests returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies CommandResultTests behavior

public class CommandResultTests
{
    [Fact]
    /// F_doc: {Success_Constructor_SetsProperties returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Success_Constructor_SetsProperties behavior
    public void Success_Constructor_SetsProperties()
    {
        var result = new CommandResult(true);
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    /// F_doc: {Failure_Constructor_SetsProperties returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Failure_Constructor_SetsProperties behavior
    public void Failure_Constructor_SetsProperties()
    {
        var result = new CommandResult(false, "error");
        Assert.False(result.Success);
        Assert.Equal("error", result.ErrorMessage);
    }
}
