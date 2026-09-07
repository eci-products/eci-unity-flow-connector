using EciFlowConnector.Runtime;
using EciFlowConnector.Editor.App;
using EciFlowConnector.Editor.Domain.Entries;
using EciFlowConnector.Editor.Domain.Versioning;
using EciFlowConnector.Editor.Features.EntryDetails;
using EciFlowConnector.Editor.Features.Export;
using EciFlowConnector.Editor.Features.Highlighter;
using EciFlowConnector.Editor.Features.Import;
using EciFlowConnector.Editor.Features.Screenshots;
using EciFlowConnector.Editor.Features.Settings;
using EciFlowConnector.Editor.Features.TableBrowser;
using EciFlowConnector.Editor.Infrastructure.Api;
using EciFlowConnector.Editor.Infrastructure.Api.Contracts;
using EciFlowConnector.Editor.Infrastructure.Images;
using EciFlowConnector.Editor.Infrastructure.Localization;
using EciFlowConnector.Editor.Infrastructure.Persistence;
using EciFlowConnector.Editor.UI.Components.MultiDropdown;
using EciFlowConnector.Editor.UI.Components.ResizablePanel;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace EciFlowConnector.Editor.Infrastructure.Api
{

internal sealed class FlowApiClient
{
    public async Task<string> PostAsync<T>(string url, T data, string apiKey)
    {
        using var handler = new HttpClientHandler
        {
            SslProtocols = System.Security.Authentication.SslProtocols.Tls12 |
                           System.Security.Authentication.SslProtocols.Tls13
        };

        using (var client = new HttpClient(handler))
        {
            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            client.DefaultRequestHeaders.Add("X-Apikey", apiKey);

            var response = await client.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                EditorUtility.DisplayDialog(
                "HTTP Error",
                $"{response.StatusCode}",
                "OK");
            }

            return await response.Content.ReadAsStringAsync();
        }
    }

    /// <summary>
    /// 通用 GET 请求，返回 byte[]
    /// 适用于：文件 / 图片 / AB 包 / 二进制数据
    /// </summary>
    public async Task<byte[]> GetBytesAsync(string url)
    {
        using var handler = new HttpClientHandler
        {
            SslProtocols = System.Security.Authentication.SslProtocols.Tls12 |
                           System.Security.Authentication.SslProtocols.Tls13
        };

        using (var client = new HttpClient(handler))
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("url cannot be null or empty");

            try
            {
                return await client.GetByteArrayAsync(url);
            }
            catch (HttpRequestException ex)
            {
                // 这里你可以接日志系统
                throw new Exception($"HTTP GET failed: {url}", ex);
            }
        }
    }
}
}
