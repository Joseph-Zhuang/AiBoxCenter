using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using AiBoxCenter.Models;

namespace AiBoxCenter.Services
{
    public class BoxApiService
    {
        private readonly HttpClient _http;

        public BoxApiService(HttpClient http)
        {
            _http = http;
        }

        private string GetMd5Hash(string input)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] data = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder sBuilder = new StringBuilder();
                for (int i = 0; i < data.Length; i++)
                {
                    sBuilder.Append(data[i].ToString("x2"));
                }
                return sBuilder.ToString();
            }
        }

        public async Task<string?> LoginAsync(string ip, string port, string user, string pwd)
        {
            try
            {
                var url = $"http://{ip}:{port}/api/login";
                var req = new LoginRequest { user = user, password = GetMd5Hash(pwd) };
                var res = await _http.PostAsJsonAsync(url, req);
                if (res.IsSuccessStatusCode)
                {
                    var data = await res.Content.ReadFromJsonAsync<LoginResponse>();
                    if (data?.status == "200" || data?.reason == "OK") return data.token;
                }
            }
            catch { }
            return null;
        }

        public async Task<List<ApiCamera>> GetCamerasAsync(string ip, string port, string token)
        {
            try
            {
                var url = $"http://{ip}:{port}/api/manager/device/list";
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.TryAddWithoutValidation("Authorization", token);
                var res = await _http.SendAsync(req);
                if (res.IsSuccessStatusCode)
                {
                    var data = await res.Content.ReadFromJsonAsync<CameraListResponse>();
                    if (data?.devices != null) return data.devices;
                }
            }
            catch { }
            return new List<ApiCamera>();
        }

        public async Task<List<ApiArea>> GetAreaListAsync(string ip, string port, string token)
        {
            try
            {
                var url = $"http://{ip}:{port}/api/manager/area/list";
                var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Headers.TryAddWithoutValidation("Authorization", token);
                req.Content = JsonContent.Create(new { });
                var res = await _http.SendAsync(req);
                if (res.IsSuccessStatusCode)
                {
                    var data = await res.Content.ReadFromJsonAsync<ApiAreaResponse>();
                    if (data?.data != null) return data.data;
                }
            }
            catch { }
            return new List<ApiArea>();
        }

        // 修改點：回傳 Tuple，包含是否成功以及新生成的 ID
        public async Task<(bool IsSuccess, string? NewId)> AddAreaAsync(string ip, string port, string token, string name, string parentId, string id = null)
        {
            try
            {
                var url = $"http://{ip}:{port}/api/manager/area/add";
                var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Headers.TryAddWithoutValidation("Authorization", token);

                object payload;
                if (!string.IsNullOrEmpty(id))
                {
                    payload = new { id = id, name = name, parentId = parentId };
                }
                else
                {
                    payload = new { name = name, parentId = parentId };
                }

                req.Content = JsonContent.Create(payload);
                var res = await _http.SendAsync(req);
                if (res.IsSuccessStatusCode)
                {
                    // 根據 API 文件，解析返回的新增區域對象來獲得 Node ID
                    var data = await res.Content.ReadFromJsonAsync<ApiSingleAreaResponse>();
                    return (true, data?.data?.id);
                }
            }
            catch { }
            return (false, null);
        }

        public async Task<bool> UpdateAreaAsync(string ip, string port, string token, string id, string name, string parentId)
        {
            try
            {
                var url = $"http://{ip}:{port}/api/manager/area/update";
                var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Headers.TryAddWithoutValidation("Authorization", token);

                req.Content = JsonContent.Create(new { id = id, name = name, parentId = parentId });
                var res = await _http.SendAsync(req);
                return res.IsSuccessStatusCode;
            }
            catch { }
            return false;
        }

        public async Task<bool> DeleteAreaAsync(string ip, string port, string token, string id)
        {
            try
            {
                var url = $"http://{ip}:{port}/api/manager/area/delete";
                var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Headers.TryAddWithoutValidation("Authorization", token);

                req.Content = JsonContent.Create(new { id = id });
                var res = await _http.SendAsync(req);
                return res.IsSuccessStatusCode;
            }
            catch { }
            return false;
        }

        public async Task<bool> AddCameraAsync(string ip, string port, string token, ApiCamera camera)
        {
            try
            {
                var url = $"http://{ip}:{port}/api/manager/device/add";
                var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Headers.TryAddWithoutValidation("Authorization", token);

                object payload = new
                {
                    id = 0,
                    name = camera.name,
                    url = camera.url,
                    type = camera.type,
                    accessType = camera.accessType,
                    areaId = string.IsNullOrEmpty(camera.areaId) ? "0" : camera.areaId,
                    code = camera.code ?? ""
                };

                req.Content = JsonContent.Create(payload);
                var res = await _http.SendAsync(req);
                if (res.IsSuccessStatusCode)
                {
                    var data = await res.Content.ReadFromJsonAsync<BaseResponse>();
                    return data?.resultCode == 0 || data?.code == 0 || data?.status == "200";
                }
            }
            catch { }
            return false;
        }

        public async Task<bool> UpdateCameraAsync(string ip, string port, string token, ApiCamera camera)
        {
            try
            {
                var url = $"http://{ip}:{port}/api/manager/device/update";
                var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Headers.TryAddWithoutValidation("Authorization", token);

                long.TryParse(camera.id, out long idLong);

                object payload = new
                {
                    id = idLong,
                    name = camera.name,
                    url = camera.url,
                    type = camera.type,
                    accessType = camera.accessType,
                    areaId = string.IsNullOrEmpty(camera.areaId) ? "0" : camera.areaId,
                    code = camera.code ?? ""
                };

                req.Content = JsonContent.Create(payload);
                var res = await _http.SendAsync(req);
                if (res.IsSuccessStatusCode)
                {
                    var data = await res.Content.ReadFromJsonAsync<BaseResponse>();
                    return data?.resultCode == 0 || data?.code == 0 || data?.status == "200";
                }
            }
            catch { }
            return false;
        }

        public async Task<bool> DeleteCameraAsync(string ip, string port, string token, string cameraId)
        {
            try
            {
                var url = $"http://{ip}:{port}/api/manager/device/delete";
                var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Headers.TryAddWithoutValidation("Authorization", token);

                long.TryParse(cameraId, out long idLong);
                req.Content = JsonContent.Create(new { id = idLong });

                var res = await _http.SendAsync(req);
                if (res.IsSuccessStatusCode)
                {
                    var data = await res.Content.ReadFromJsonAsync<BaseResponse>();
                    return data?.resultCode == 0 || data?.code == 0 || data?.status == "200";
                }
            }
            catch { }
            return false;
        }
    }
}