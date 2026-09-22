using System;
namespace SocialSystem.Client.Network
{
    public sealed class ApiResponse<T>
    {
        public bool Success;
        public long StatusCode;
        public T Data;
        public string Error;
        public static ApiResponse<T> Failure(string error, long status = 0) =>
            new ApiResponse<T> { Success = false, Error = error, StatusCode = status };
    }
    [Serializable]
    internal sealed class ApiProblem { public string title; public string detail; public string code; }
}
