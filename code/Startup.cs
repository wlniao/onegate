using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wlniao;
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<KestrelServerOptions>(options =>
        {
            options.AllowSynchronousIO = true;
        });
    }
    public void Configure(IApplicationBuilder app)
    {
        app.Run(async (context) =>
        {
            var str = "";
            var req = context.Request;
            var path = req.Path.Value;
            var host = string.IsNullOrEmpty(Program.Domain) ? req.Host.Value : Program.Domain;
            var port = req.HttpContext.Connection.LocalPort;
            if (strUtil.IsIP(host))
            {
                str = "{\"errcode\":502,\"errmsg\":\"请通过域名发起服务请求\"}";
                context.Response.Headers.TryAdd("Content-Type", "application/json");
            }
            else
            {
                using (var hostSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    try
                    {
                        var reqStr = "";
                        reqStr += req.Method + " " + path + (req.QueryString.HasValue ? req.QueryString.Value : "") + " HTTP/1.1";
                        foreach (var header in context.Request.Headers)
                        {
                            if (header.Key == "Host" && Program.Domain.Length > 0 && !strUtil.IsIP(Program.Domain))
                            {
                                reqStr += "\r\n" + header.Key + ": " + Program.Domain;
                            }
                            else if (header.Key == "Accept-Encoding" || header.Key == "Origin" || header.Key == "Referer")
                            {
                                //跳过
                            }
                            else
                            {
                                reqStr += "\r\n" + header.Key + ": " + header.Value.ToString();
                            }
                        }
                        reqStr += "\r\n";
                        reqStr += "\r\n";
                        var reqData = System.Text.Encoding.UTF8.GetBytes(reqStr);
                        //var reqData = new List<byte>();
                        //reqData.AddRange(System.Text.Encoding.UTF8.GetBytes(reqStr));
                        if (req.Method == "POST")
                        {
                            var content = new byte[(int)req.ContentLength];
                            var buffer = new byte[reqData.Length + content.Length];
                            context.Request.Body.Read(content, 0, content.Length);
                            Buffer.BlockCopy(reqData, 0, buffer, 0, reqData.Length);
                            Buffer.BlockCopy(content, 0, buffer, reqData.Length, content.Length);
                            reqData = buffer;
                        }

                        #region 发起调用请求
                        if (Program.IsHttps || req.IsHttps)
                        {
                            #region HTTPS请求
                            hostSocket.Connect(host, port);
                            using (SslStream ssl = new SslStream(new NetworkStream(hostSocket, true), false, new RemoteCertificateValidationCallback(Program.ValidateServerCertificate), null))
                            {
                                //ssl.AuthenticateAsClientAsync(host).ContinueWith((_rlt) =>
                                //{
                                //    if (ssl.IsAuthenticated)
                                //    {
                                //        //证书有效
                                //    }
                                //}).Wait();
                                ssl.AuthenticateAsClient(host);
                                ssl.Write(reqData);
                                ssl.Flush();
                                var first = true;
                                var chunked = false;
                                var totalLength = 0;
                                while (true)
                                {
                                    var rev = new byte[65535];
                                    var count = ssl.Read(rev, 0, rev.Length);
                                    if (count == 0)
                                    {
                                        break;
                                    }
                                    var row = 0;
                                    var lines = System.Text.UTF8Encoding.Default.GetString(rev, 0, count).Split(new string[] { "\r\n" }, StringSplitOptions.None);
                                    #region Headers处理
                                    if (first && lines[0].StartsWith("HTTP"))
                                    {
                                        first = false;
                                        var ts = lines[0].Split(' ');
                                        context.Response.StatusCode = cvt.ToInt(ts[1]);
                                        for (row = 1; row < lines.Length; row++)
                                        {
                                            if (lines[row].Length > 0)
                                            {
                                                ts = lines[row].Split(": ");
                                                switch (ts[0].ToLower())
                                                {
                                                    case "content-length":
                                                        totalLength = cvt.ToInt(ts[1]);
                                                        break;
                                                    case "transfer-encoding":
                                                        chunked = ts[1].ToLower() == "chunked";
                                                        continue;
                                                }
                                                if (context.Response.Headers.ContainsKey(ts[0]))
                                                {
                                                    context.Response.Headers[ts[0]] = ts[1];
                                                }
                                                else
                                                {
                                                    context.Response.Headers.TryAdd(ts[0], ts[1]);
                                                }
                                            }
                                            else
                                            {
                                                // Headers部分结束
                                                row++;
                                                break;
                                            }
                                        }
                                    }
                                    #endregion
                                    #region 取文本内容
                                    for (; row < lines.Length; row++)
                                    {
                                        var line = lines[row];
                                        if (chunked)
                                        {
                                            row++;
                                            if (row < lines.Length)
                                            {
                                                var tempLength = cvt.DeHex(line, "0123456789abcdef");
                                                if (tempLength > 0)
                                                {
                                                    totalLength += (int)tempLength;
                                                    line = lines[row];
                                                }
                                                else if (lines.Length == row + 2 && string.IsNullOrEmpty(lines[row + 1]))
                                                {
                                                    goto ResponseOutPut;
                                                }
                                            }
                                            else
                                            {
                                                break;
                                            }
                                        }
                                        str += str.Length == 0 ? line : "\r\n" + line;
                                    }
                                    if (!chunked || str.Length >= totalLength)
                                    {
                                        goto ResponseOutPut;
                                    }
                                    #endregion
                                }
                            }
                            #endregion
                        }
                        else
                        {
                            #region HTTP请求
                            hostSocket.Connect(host, port);
                            if (hostSocket.Send(reqData, reqData.Length, System.Net.Sockets.SocketFlags.None) > 0)
                            {
                                var first = true;
                                var chunked = false;
                                var totalLength = 0;
                                while (true)
                                {
                                    var rev = new byte[65535];
                                    var count = hostSocket.Receive(rev, rev.Length, System.Net.Sockets.SocketFlags.None);
                                    if (count == 0)
                                    {
                                        break;
                                    }
                                    var row = 0;
                                    var lines = System.Text.UTF8Encoding.Default.GetString(rev, 0, count).Split(new string[] { "\r\n" }, StringSplitOptions.None);
                                    #region Headers处理
                                    if (first && lines[0].StartsWith("HTTP"))
                                    {
                                        first = false;
                                        var ts = lines[0].Split(' ');
                                        context.Response.StatusCode = cvt.ToInt(ts[1]);
                                        for (row = 1; row < lines.Length; row++)
                                        {
                                            if (lines[row].Length > 0)
                                            {
                                                ts = lines[row].Split(": ");
                                                switch (ts[0].ToLower())
                                                {
                                                    case "content-length":
                                                        totalLength = cvt.ToInt(ts[1]);
                                                        break;
                                                    case "transfer-encoding":
                                                        chunked = ts[1].ToLower() == "chunked";
                                                        continue;
                                                }
                                                if (context.Response.Headers.ContainsKey(ts[0]))
                                                {
                                                    context.Response.Headers[ts[0]] = ts[1];
                                                }
                                                else
                                                {
                                                    context.Response.Headers.TryAdd(ts[0], ts[1]);
                                                }
                                            }
                                            else
                                            {
                                                // Headers部分结束
                                                row++;
                                                break;
                                            }
                                        }
                                    }
                                    #endregion
                                    #region 取文本内容
                                    for (; row < lines.Length; row++)
                                    {
                                        var line = lines[row];
                                        if (chunked)
                                        {
                                            row++;
                                            if (row < lines.Length)
                                            {
                                                var tempLength = cvt.DeHex(line, "0123456789abcdef");
                                                if (tempLength > 0)
                                                {
                                                    totalLength += (int)tempLength;
                                                    line = lines[row];
                                                }
                                                else if (lines.Length == row + 2 && string.IsNullOrEmpty(lines[row + 1]))
                                                {
                                                    goto ResponseOutPut;
                                                }
                                            }
                                            else
                                            {
                                                break;
                                            }
                                        }
                                        str += str.Length == 0 ? line : "\r\n" + line;
                                    }
                                    if (!chunked || str.Length >= totalLength)
                                    {
                                        goto ResponseOutPut;
                                    }
                                    #endregion
                                }
                            }
                            #endregion
                        }
                        #endregion
                    }
                    catch (Exception ex)
                    {
                        str = "{\"errcode\":502,\"errmsg\":\"" + ex.Message + "\"}";
                        context.Response.Headers.TryAdd("Content-Type", "application/json");
                    }
                    ResponseOutPut:;    //内容接收完成跳转到此处
                    hostSocket.Close();
                }
            }
            await context.Response.WriteAsync(str);
        });
    }
}