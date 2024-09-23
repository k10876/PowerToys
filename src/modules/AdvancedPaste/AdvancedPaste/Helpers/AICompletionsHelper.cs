// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Azure;
using Azure.AI.OpenAI;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
using Windows.Security.Credentials;

namespace AdvancedPaste.Helpers
{
    public class AICompletionsHelper
    {
        // Return Response and Status code from the request.
        public struct AICompletionsResponse
        {
            public AICompletionsResponse(string response, int apiRequestStatus)
            {
                Response = response;
                ApiRequestStatus = apiRequestStatus;
            }

            public string Response { get; }

            public int ApiRequestStatus { get; }
        }

        public struct DashScopeResponse
        {
            public DashScopeResponse(int promptTokens, int completionTokens, string content)
            {
                PromptTokens = promptTokens;
                CompletionTokens = completionTokens;
                Content = content;
            }

            public int PromptTokens { get; set; }

            public int CompletionTokens { get; set; }

            public string Content { get; set; }
        }

        private string _openAIKey;

        private string _modelName = "gpt-3.5-turbo-instruct";

        public bool IsAIEnabled => !string.IsNullOrEmpty(this._openAIKey);

        public AICompletionsHelper()
        {
            this._openAIKey = LoadOpenAIKey();
        }

        public void SetOpenAIKey(string openAIKey)
        {
            this._openAIKey = openAIKey;
        }

        public string GetKey()
        {
            return _openAIKey;
        }

        public static string LoadOpenAIKey()
        {
            PasswordVault vault = new PasswordVault();

            try
            {
                PasswordCredential cred = vault.Retrieve("https://platform.openai.com/api-keys", "PowerToys_AdvancedPaste_OpenAIKey");
                if (cred is not null)
                {
                    return cred.Password.ToString();
                }
            }
            catch (Exception)
            {
            }

            return string.Empty;
        }

        public static async Task<DashScopeResponse> MakeQwenRequests(string systemInstructions, string userMessage, string openAIKey)
        {
            using (HttpClient client = new HttpClient())
            {
                // Set up the request headers
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openAIKey);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Set up the request body
                var requestBody = new
                {
                    model = "qwen-plus-latest",
                    input = new
                    {
                        messages = new[]
                        {
                            new { role = "system", content = systemInstructions },
                            new { role = "user", content = userMessage },
                        },
                    },
                };

                // Serialize the request body to JSON
                string json = System.Text.Json.JsonSerializer.Serialize(requestBody);
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

                // Make the request
                HttpResponseMessage response = await client.PostAsync("https://dashscope.aliyuncs.com/api/v1/services/aigc/text-generation/generation", content);
                try
                {
                    response.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException error)
                {
                    string errorResponseBody = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException(errorResponseBody, error);
                }

                // Read the Response
                string responseBody = await response.Content.ReadAsStringAsync();

                // Parse the JSON response
                var responseJson = JsonDocument.Parse(responseBody);

                // Extract Parameters
                var promptTokens = responseJson.RootElement.GetProperty("usage").GetProperty("input_tokens").GetInt32();
                var completionTokens = responseJson.RootElement.GetProperty("usage").GetProperty("output_tokens").GetInt32();
                var messageContent = responseJson.RootElement.GetProperty("output").GetProperty("text").GetString();
                var finishReason = responseJson.RootElement.GetProperty("output").GetProperty("finish_reason").GetString();

                // Unescape the AI-Generated Text
                try
                {
                    messageContent = Regex.Unescape(messageContent);
                }
                catch (Exception error)
                {
                    PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteGenerateCustomErrorEvent(error.Message));
                }

                if (finishReason == "length")
                {
                    Console.WriteLine("Cut off due to length constraints");
                }

                // Return the extracted values
                return new DashScopeResponse
                {
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    Content = messageContent,
                };
            }
        }

        public async Task<AICompletionsResponse> AIFormatString(string inputInstructions, string inputString)
        {
            string systemInstructions = $@"You are tasked with reformatting user's clipboard data. Use the user's instructions, and the content of their clipboard below to edit their clipboard content as they have requested it.

Do not output anything else besides the reformatted clipboard content.";

            string userMessage = $@"User instructions:
{inputInstructions}

Clipboard Content:
{inputString}

Output:
";

            string aiResponse = null;
            int apiRequestStatus = (int)HttpStatusCode.OK;
            try
            {
                string openAIKey = GetKey();
                DashScopeResponse rawAIResponse = await MakeQwenRequests(systemInstructions, userMessage, openAIKey);
                aiResponse = rawAIResponse.Content;

                int promptTokens = rawAIResponse.PromptTokens;
                int completionTokens = rawAIResponse.CompletionTokens;
                PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteGenerateCustomFormatEvent(promptTokens, completionTokens, _modelName));
            }
            catch (Exception error)
            {
                Logger.LogError("GetAICompletion failed", error);
                PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteGenerateCustomErrorEvent(error.Message));
                apiRequestStatus = -1;
            }

            return new AICompletionsResponse(aiResponse, apiRequestStatus);
        }
    }
}
