using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using slskd.Core.API;
using Xunit;

namespace slskd.Tests.Unit.Core.API;

public class LogsHubTests
{
    [Fact]
    public void LogsHub_RequiresAdministrator()
    {
        var authorize = typeof(LogsHub).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(AuthPolicy.Any, authorize.Policy);
        Assert.Equal(AuthRole.AdministratorOnly, authorize.Roles);
    }
}
