using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
namespace Das.PdfProtocol;

public static class PdfProtocolSettings
{
    public static bool MaintenanceEnabled(IConfiguration config,IHostEnvironment environment,string remote)
    {
        var text=config["PdfProtocol:MaintenanceEnabled"];
        if(text is null)return false;
        if(!bool.TryParse(text,out var enabled))throw new InvalidOperationException("PdfProtocol:MaintenanceEnabled must be true or false.");
        if(!enabled)return false;
        var key=config["PdfProtocol:Key"];var address=config["PdfProtocol:"+remote];
        if(string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key)<32 || key.Length>1024 ||
            !Uri.TryCreate(address,UriKind.Absolute,out var uri) || uri.UserInfo.Length>0 || uri.Query.Length>0 || uri.Fragment.Length>0 ||
            (uri.Scheme!="https" && !(environment.IsDevelopment() && uri.Scheme=="http")))
            throw new InvalidOperationException("Configure a separate PdfProtocol key and explicit HTTPS peer before enabling maintenance.");
        return true;
    }
}
