using System;
using System.Collections;
using System.Text;
using SocialSystem.Client.Auth;
using UnityEngine;
using UnityEngine.Networking;

namespace SocialSystem.Client.Network
{
    public sealed class ApiClient : MonoBehaviour
    {
        public string baseUrl = "http://localhost:5080";
        public TokenManager tokenManager;
        [Range(1, 120)] public int timeoutSeconds = 15;

        public IEnumerator Get<T>(string path, Action<ApiResponse<T>> completed, bool authorize = true) =>
            Send<T>("GET", path, null, completed, authorize);
        public IEnumerator Post<T>(string path, object body, Action<ApiResponse<T>> completed, bool authorize = true) =>
            Send<T>("POST", path, body == null ? null : JsonUtility.ToJson(body), completed, authorize);
        public IEnumerator Delete<T>(string path, Action<ApiResponse<T>> completed, bool authorize = true) =>
            Send<T>("DELETE", path, null, completed, authorize);

        private IEnumerator Send<T>(string method, string path, string json, Action<ApiResponse<T>> completed, bool authorize)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("/api/", StringComparison.Ordinal) ||
                !Uri.TryCreate(baseUrl, UriKind.Absolute, out var root) ||
                (root.Scheme != "http" && root.Scheme != "https"))
            {
                completed?.Invoke(ApiResponse<T>.Failure("Invalid API URL."));
                yield break;
            }
            var sentToken = authorize && tokenManager != null ? tokenManager.AccessToken : null;
            using (var request = new UnityWebRequest(baseUrl.TrimEnd('/') + path, method))
            {
                request.timeout = timeoutSeconds;
                request.redirectLimit = 0;
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Accept", "application/json");
                if (json != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
                }
                if (!string.IsNullOrEmpty(sentToken))
                    request.SetRequestHeader("Authorization", "Bearer " + sentToken);
                yield return request.SendWebRequest();
                if (request.responseCode == 401 && !string.IsNullOrEmpty(sentToken) &&
                    tokenManager != null && tokenManager.AccessToken == sentToken) tokenManager.Clear();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    var error = request.responseCode switch
                    {
                        400 => "参数无效，请检查 ID 或内容长度。",
                        401 => "账号或密码错误，或登录已失效。",
                        403 => "无操作权限，请检查好友关系或资源归属。",
                        404 => "用户、申请或动态不存在。",
                        409 => "操作冲突：账号、好友关系或待处理申请已存在，或申请已处理。",
                        0 => "无法连接服务器，请检查后端地址和网络；提交超时后请先刷新确认结果。",
                        _ => "API request failed. HTTP " + request.responseCode
                    };
                    try
                    {
                        var problem = JsonUtility.FromJson<ApiProblem>(request.downloadHandler.text);
                        error = problem?.code switch
                        {
                            "not_friends" => "仅好友可发送消息，请先添加好友。",
                            "request_pending" => "双方已有待处理的好友申请。",
                            "already_friends" => "你们已经是好友。",
                            "not_request_receiver" => "只有申请接收者可以接受此申请。",
                            "request_handled" => "此申请已处理，请使用新的申请 ID。",
                            "not_post_author" => "只能删除自己发布的动态。",
                            _ => error
                        };
                    }
                    catch (Exception) { /* Keep the HTTP fallback for non-JSON errors. */ }
                    completed?.Invoke(ApiResponse<T>.Failure(error, request.responseCode));
                    yield break;
                }
                var result = new ApiResponse<T> { Success = true, StatusCode = request.responseCode };
                if (!string.IsNullOrWhiteSpace(request.downloadHandler.text))
                {
                    try
                    {
                        result.Data = typeof(T) == typeof(string)
                            ? (T)(object)request.downloadHandler.text
                            : JsonUtility.FromJson<T>(request.downloadHandler.text);
                    }
                    catch (Exception)
                    {
                        result = ApiResponse<T>.Failure("Invalid JSON response.", request.responseCode);
                    }
                }
                completed?.Invoke(result);
            }
        }
    }
}

