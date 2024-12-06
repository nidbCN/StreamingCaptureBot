﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using CameraCaptureBot.Impl.Tencent.Options;
using CameraCaptureBot.Impl.Tencent.Protocols;
using CameraCaptureBot.Impl.Tencent.Protocols.EventContents;
using CameraCaptureBot.Impl.Tencent.Utils.Sign;
using Microsoft.Extensions.Options;

namespace CameraCaptureBot.Impl.Tencent;

public class TencentWebhookWorker(
    ILogger<TencentWebhookWorker> logger,
    IOptions<TencentImplOption> options,
    ISignProvider signProvider) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    => Task.Run(async () =>
        {
            using var listener = new HttpListener();

            var listen = options.Value.ListenIpAddress.MapToIPv4().ToString();
            var port = options.Value.ListenPort;
            var route = options.Value.Route;

            logger.LogInformation("Start webhook listen on `http://{ip}:{port}{route}.`", listen, port, route);

            if (options.Value.ListenIpAddress.Equals(IPAddress.Any))
            {
                listen = "+";
            }

            listener.Prefixes.Add($"http://{listen}:{port}{route}");

            listener.Start();

            while (!stoppingToken.IsCancellationRequested)
            {
                var ctx = await listener.GetContextAsync();
                var req = ctx.Request;
                using var resp = ctx.Response;
                var payload = await JsonSerializer.DeserializeAsync<Payload>(req.InputStream, cancellationToken: stoppingToken);

                if (payload is null)
                {
                    resp.StatusCode = (int)HttpStatusCode.BadRequest;
                    continue;
                }

                switch (payload.OperationCode)
                {
                    case OperationCode.HttpCallbackVerify:
                        var content = payload.EventContent as HttpCallbackVerify;
                        var signed = signProvider.Sign(content.EventTimespan + content.PlainToken);

                        resp.StatusCode = (int)HttpStatusCode.OK;
                        break;
                }

                resp.StatusCode = (int)HttpStatusCode.Forbidden;
            }
        }, stoppingToken);

}
