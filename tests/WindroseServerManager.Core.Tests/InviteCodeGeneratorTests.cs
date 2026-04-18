using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;
using Xunit;

namespace WindroseServerManager.Core.Tests;

public class InviteCodeGeneratorTests
{
    [Fact]
    public void Generate_Produces100ValidCodes()
    {
        for (var i = 0; i < 100; i++)
        {
            var code = InviteCodeGenerator.Generate();
            Assert.False(string.IsNullOrWhiteSpace(code));
            Assert.True(code.Length >= 6, $"Code zu kurz: '{code}'");
            var error = ServerDescription.ValidateInviteCode(code);
            Assert.Null(error);
        }
    }
}
