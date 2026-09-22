using System;
using System.Collections;
using SocialSystem.Client.Network;
using UnityEngine;

namespace SocialSystem.Client.Messages
{
    [Serializable] public sealed class MessageDto
    {
        public long id;
        public long senderId;
        public long receiverId;
        public string content;
        public string createdAt;
    }
    [Serializable] public sealed class MessageBody { public long receiverId; public string content; }
    [Serializable] internal sealed class MessageArray { public MessageDto[] items; }
    public sealed class MessageApi
    {
        private readonly ApiClient api;
        public MessageApi(ApiClient api) { this.api = api; }
        public IEnumerator Send(long receiverId, string content, Action<ApiResponse<MessageDto>> completed) =>
            api.Post<MessageDto>("/api/messages/send", new MessageBody { receiverId = receiverId, content = content }, completed);
        public IEnumerator History(long id, Action<ApiResponse<MessageDto[]>> completed)
        {
            ApiResponse<string> response = null;
            yield return api.Get<string>("/api/messages/" + id, r => response = r);
            if (response == null || !response.Success)
            {
                completed(ApiResponse<MessageDto[]>.Failure(response?.Error ?? "消息请求未完成。", response?.StatusCode ?? 0));
                yield break;
            }
            ApiResponse<MessageDto[]> result;
            try
            {
                var json = response.Data?.Trim();
                if (string.IsNullOrEmpty(json) || !json.StartsWith("[") || !json.EndsWith("]"))
                    throw new FormatException();
                var wrapper = JsonUtility.FromJson<MessageArray>("{\"items\":" + json + "}");
                if (wrapper?.items == null) throw new FormatException();
                result = new ApiResponse<MessageDto[]> { Success = true, StatusCode = response.StatusCode, Data = wrapper.items };
            }
            catch (Exception)
            {
                result = ApiResponse<MessageDto[]>.Failure("消息列表响应格式无效。", response.StatusCode);
            }
            completed(result);
        }
    }
}

