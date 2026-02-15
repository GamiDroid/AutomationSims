
namespace Mqtt.Common;

internal class MqttTopicNotFoundException(string topic, string message) : Exception(message)
{
    public string Topic { get; } = topic;
}