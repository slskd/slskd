using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using slskd.Files.API;
using Xunit;

namespace slskd.Tests.Unit.Files.API;

public class FilesControllerTests
{
    [Theory]
    [InlineData(nameof(FilesController.DeleteDownloadSubdirectoryAsync))]
    [InlineData(nameof(FilesController.DeleteDownloadFileAsync))]
    [InlineData(nameof(FilesController.DeleteIncompleteSubdirectoryAsync))]
    [InlineData(nameof(FilesController.DeleteIncompleteFileAsync))]
    public void Delete_RequiresWriteAccess(string methodName)
    {
        var method = typeof(FilesController).GetMethod(methodName);
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(AuthPolicy.Any, authorize.Policy);
        Assert.Equal(AuthRole.ReadWriteOrAdministrator, authorize.Roles);
    }
}
