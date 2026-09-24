using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace LovePandas.Core
{
    /// HTTP к серверу LovePandas. Токен анонимного игрока хранится в PlayerPrefs.
    public class ApiClient
    {
        public const string OfflineError = "Нет связи с сервером";

        readonly string baseUrl;
        readonly string tokenKey;
        string token;

        public ApiClient(string baseUrl, string profile, string tokenOverride)
        {
            this.baseUrl = baseUrl.TrimEnd('/');
            tokenKey = "token_" + profile;
            token = tokenOverride ?? PlayerPrefs.GetString(tokenKey, null);
        }

        public bool HasToken => !string.IsNullOrEmpty(token);

        /// Сервер не узнал токен (например, базу пересоздали) — забываем и регистрируемся заново.
        public void ForgetToken()
        {
            token = null;
            PlayerPrefs.DeleteKey(tokenKey);
        }

        public async Task<string> Register()
        {
            var (code, text) = await Send("POST", "/auth/register", null);
            if (code != 200) return OfflineError;
            var r = JsonUtility.FromJson<RegisterResponse>(text);
            token = r.token;
            PlayerPrefs.SetString(tokenKey, token);
            PlayerPrefs.Save();
            return null;
        }

        /// null — состояние не менялось с версии since (304).
        public async Task<ApiResponse> GetState(int since)
        {
            var (code, text) = await Send("GET", since >= 0 ? $"/state?since={since}" : "/state", null);
            if (code == 304) return null;
            return Parse(code, text);
        }

        public async Task<ApiResponse> Post(string path, object body = null)
        {
            var (code, text) = await Send("POST", path, body != null ? JsonUtility.ToJson(body) : "{}");
            return Parse(code, text);
        }

        static ApiResponse Parse(long code, string text)
        {
            if (code == 0 || string.IsNullOrEmpty(text)) return new ApiResponse { error = OfflineError };
            try { return JsonUtility.FromJson<ApiResponse>(text); }
            catch (Exception) { return new ApiResponse { error = OfflineError }; }
        }

        async Task<(long, string)> Send(string method, string path, string json)
        {
            using var req = new UnityWebRequest(baseUrl + path, method);
            req.downloadHandler = new DownloadHandlerBuffer();
            if (json != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            if (HasToken) req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = 10;
            await req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.ConnectionError) return (0, null);
            return (req.responseCode, req.downloadHandler.text);
        }
    }
}
