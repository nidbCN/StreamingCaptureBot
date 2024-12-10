using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StreamCaptureBot.Utils.Extensions;
using VideoStreamCaptureBot.Impl.Tencent.Options;
using VideoStreamCaptureBot.Impl.Tencent.Protocols;
using VideoStreamCaptureBot.Impl.Tencent.Protocols.EventContents;
using VideoStreamCaptureBot.Impl.Tencent.Utils.Sign;

namespace VideoStreamCaptureBot.Impl.Tencent;

public class TencentWebhookWorker(
    ILogger<TencentWebhookWorker> logger,
    IOptions<TencentImplOption> options,
    ISignProvider signProvider) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    => Task.Run(async () =>
        {
            using var listener = new HttpListener();

            var listen = options.Value.ListenIpAddress;
            var port = options.Value.ListenPort;
            var route = options.Value.Route;

            logger.LogInformation("Start webhook listen on `http://{ip}:{port}{route}`.", listen, port, route);

            if (IPAddress.TryParse(listen, out var ip))
            {
                if (ip.Equals(IPAddress.Any))
                    listen = "+";
            }
            else
            {
                listen = IPAddress.Loopback.ToString();
            }

            var fullUrl = $"http://{listen}:{port}{route}";

            logger.LogInformation("Add `{url}` to listener prefixes list.", fullUrl);

            listener.Prefixes.Add(fullUrl);

            listener.Start();

            while (!stoppingToken.IsCancellationRequested)
            {
                var ctx = await listener.GetContextAsync();
                var req = ctx.Request;
                using var resp = ctx.Response;

                if (!req.HasEntityBody)
                {
                    logger.LogInformation("Received no body, return {bad}.", HttpStatusCode.BadRequest.ToString());
                    resp.StatusCode = (int)HttpStatusCode.BadRequest;
                    continue;
                }

                Payload? payload;

                try
                {
                    using var reader = new StreamReader(req.InputStream);
                    var body = await reader.ReadToEndAsync();
                    logger.LogInformation("Received request with payload: {json}", body);
                    payload = JsonSerializer.Deserialize<Payload>(body);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Deserialize payload error.");
                    logger.LogInformation("Received un-known body payload, return {bad}.", HttpStatusCode.BadRequest.ToString());
                    resp.StatusCode = (int)HttpStatusCode.BadRequest;
                    continue;
                }

                if (payload is null)
                {
                    logger.LogInformation("Received empty body payload, return {bad}.", HttpStatusCode.BadRequest.ToString());
                    resp.StatusCode = (int)HttpStatusCode.BadRequest;
                    continue;
                }

                switch (payload.OperationCode)
                {
                    case OperationCode.HttpCallbackVerify:
                        var content = payload.EventContent as HttpCallbackVerify;
                        var signed = signProvider.Sign(content.EventTimespan + content.PlainToken);

                        resp.StatusCode = (int)HttpStatusCode.OK;
                        await JsonSerializer.SerializeAsync(resp.OutputStream, new
                        {
                            plain_token = content.PlainToken,
                            signature = signed.ToHex(),
                        }, cancellationToken: stoppingToken);

                        break;
                    default:
                        resp.StatusCode = (int)HttpStatusCode.Forbidden;
                        break;
                }
            }
        }, stoppingToken);

}
