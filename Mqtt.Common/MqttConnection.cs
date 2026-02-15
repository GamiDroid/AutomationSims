using MQTTnet;
using MQTTnet.Adapter;
using MQTTnet.Diagnostics.Logger;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Mqtt.Common;

public interface IMqttConnection
{
    public Task StartAsync(CancellationToken ct = default);
    public Task StopAsync(CancellationToken ct = default);
}

public class MqttConnection : IMqttConnection, IAsyncDisposable
{
    private readonly MqttClientOptions _options;
    private readonly IMqttClient _client;

    private readonly ConcurrentDictionary<string, Delegate> _subscriptions = [];

    public MqttConnection(MqttClientOptions options)
    {
        _options = options;

        _client = new MqttClientFactory().CreateMqttClient();

        _client.ApplicationMessageReceivedAsync += OnApplicationMessageReceivedAsync;
        _client.ConnectedAsync += OnConnectedAsync;
        _client.DisconnectedAsync += OnDisconnectedAsync;
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (_client.IsConnected)
            await _client.DisconnectAsync().ConfigureAwait(false);
        _client.Dispose();
    }

    private async Task OnConnectedAsync(MqttClientConnectedEventArgs args)
    {
        if (_subscriptions.IsEmpty)
            return;

        foreach (var topic in _subscriptions.Keys)
        {
            var options = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(topic)
                .Build();
            await _client.SubscribeAsync(options).ConfigureAwait(false);
        }
    }

    private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        throw new NotImplementedException();
    }

    private async Task OnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        throw new NotImplementedException();
    }

    public Task StartAsync(CancellationToken ct)
    {
        return _client.ConnectAsync(_options, ct);
    }

    public Task StopAsync(CancellationToken ct)
    {
        return _client.DisconnectAsync(cancellationToken: ct);
    }

    public async ValueTask AddSubscriptionAsync(string topic, Action<MqttApplicationMessage> handler, CancellationToken ct)
    {
        _subscriptions.TryAdd(topic, handler);

        if (!_client.IsConnected)
            return;

        var options = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(topic)
            .Build();

        await _client.SubscribeAsync(options, ct);
    }

    public async Task OnMqttMessage(string topic, Action<MqttApplicationMessage> handler)
    {
        await AddSubscriptionAsync(topic, handler, CancellationToken.None);
    }

    public async Task<T> GetTopicDataAsync<T>(string topic, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            throw new ArgumentException("Topic cannot be null, empty, or whitespace.", nameof(topic));
        }

        TaskCompletionSource<T> tcs = new();

        try
        {
            await this.OnMqttMessage(topic, (mqttMessage) =>
            {
                try
                {
                    if (mqttMessage.Payload.IsEmpty)
                    {
                        // Empty payload means the retained message was deleted
                        tcs.TrySetException(new MqttTopicNotFoundException(topic, $"No retained message found on topic: {topic} (empty payload received)"));
                        return;
                    }

                    T? data = JsonSerializer.Deserialize<T>(mqttMessage.ConvertPayloadToString());
                    tcs.TrySetResult(data!);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            const int timeoutSeconds = 5;

            try
            {
                await tcs.Task.WaitAsync(TimeSpan.FromSeconds(timeoutSeconds), ct);
                return tcs.Task.Result;
            }
            catch (TimeoutException)
            {
                throw new MqttTopicNotFoundException(topic, $"No retained message found on topic: {topic} (timeout after {timeoutSeconds} seconds)");
            }
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            await _client.UnsubscribeAsync(topic, CancellationToken.None);
        }
    }


}
