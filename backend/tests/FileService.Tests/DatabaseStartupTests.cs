using FilesService.Data;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace FileService.Tests;
public sealed class DatabaseStartupTests
{
    [Theory]
    [InlineData("provider")][InlineData("connection")][InlineData("mismatched")][InlineData("invalidFlag")]
    public void Explicit_database_configuration_is_required(string invalid)
    {
        var data=new Dictionary<string,string?>{["Database:Provider"]="SqlServer",["ConnectionStrings:Default"]="Server=localhost;Database=files;Integrated Security=true",["Database:Initialize"]="false"};
        if(invalid=="provider")data["Database:Provider"]=null;if(invalid=="connection")data["ConnectionStrings:Default"]=null;
        if(invalid=="mismatched")data["ConnectionStrings:Default"]="Data Source=files.db";if(invalid=="invalidFlag")data["Database:Initialize"]="yes";
        Assert.Throws<InvalidOperationException>(()=>FileDatabaseOptions.Read(new ConfigurationBuilder().AddInMemoryCollection(data).Build(),false));
    }
    [Theory]
    [InlineData("Sqlite",false)][InlineData("SqlServer",true)]
    public void Production_never_uses_sqlite_or_startup_schema_changes(string provider,bool initialize)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Database:Provider"]=provider,
            ["ConnectionStrings:Default"]=provider=="Sqlite"?"Data Source=files.db":"Server=localhost;Database=files;Integrated Security=true",["Database:Initialize"]=initialize.ToString()}).Build();
        Assert.Throws<InvalidOperationException>(()=>FileDatabaseOptions.Read(config,false));
    }
    [Fact]
    public void Development_can_explicitly_select_sqlite_initialization()
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Database:Provider"]="Sqlite",["ConnectionStrings:Default"]="Data Source=files.db",["Database:Initialize"]="true"}).Build();
        Assert.Equal(new FileDatabaseOptions("Sqlite","Data Source=files.db",true),FileDatabaseOptions.Read(config,true));
    }
}
