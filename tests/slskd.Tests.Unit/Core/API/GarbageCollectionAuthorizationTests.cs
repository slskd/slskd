using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using slskd.Core.API;
using Xunit;

namespace slskd.Tests.Unit.Core.API;

public class GarbageCollectionAuthorizationTests
{
    [Fact]
    public void CollectGarbage_RequiresAdministrator()
    {
        var method = typeof(ApplicationController).GetMethod(nameof(ApplicationController.CollectGarbage));
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(AuthPolicy.Any, authorize.Policy);
        Assert.Equal(AuthRole.AdministratorOnly, authorize.Roles);
    }
}
