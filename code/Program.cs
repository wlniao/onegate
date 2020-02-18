using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class Program
{
    private static string realhost = null;
    public static string RealHost
    {
        get
        {
            if (realhost == null)
            {
                realhost = Wlniao.Config.GetSetting("RealHost");
                if (realhost == null)
                {
                    realhost = "";
                }
            }
            return realhost;
        }
    }
    public static void Main(string[] args)
    {
        //启动Web服务（用于Web代理的错误信息输出）
        var host = new WebHostBuilder()
            .UseKestrel()
            .UseStartup<Startup>()
            .UseKestrel(o =>
            {
                o.Listen(System.Net.IPAddress.IPv6Any, 80);
                if (Wlniao.file.Exists(Wlniao.IO.PathTool.Map("server.pfx")))
                {
                    o.Listen(System.Net.IPAddress.IPv6Any, 443, x =>
                    {
                        var opt = new Microsoft.AspNetCore.Server.Kestrel.Https.HttpsConnectionAdapterOptions();
                        opt.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
                        opt.ServerCertificate = new X509Certificate2("server.pfx");
                        x.UseHttps(opt); o.Limits.MaxConcurrentConnections = 100;
                    });
                }
            })
            .Build();
        host.Run();
    }
    public static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
    {
        return true;
    }
}