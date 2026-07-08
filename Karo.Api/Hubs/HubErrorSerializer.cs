using System.Text.Json;
using Karo.Api.Models;
using Microsoft.AspNetCore.SignalR;

namespace Karo.Api.Hubs;

public static class HubErrorSerializer
{
    private static readonly JsonSerializerOptions ErrorJsonOptions = new(JsonSerializerDefaults.Web);

    public static HubException ToHubException(GameRuleException exception)
    {
        return ToHubException(exception.ErrorCode, exception.UserMessage);
    }

    public static HubException ToHubException(LobbyException exception)
    {
        return ToHubException(exception.ErrorCode, exception.UserMessage);
    }

    public static HubException ToHubException(string errorCode, string userMessage)
    {
        return new HubException(Serialize(errorCode, userMessage));
    }

    public static string Serialize(string errorCode, string userMessage)
    {
        return JsonSerializer.Serialize(new GameValidationErrorDto(errorCode, userMessage), ErrorJsonOptions);
    }

    private sealed record GameValidationErrorDto(
        string ErrorCode,
        string UserMessage);
}
