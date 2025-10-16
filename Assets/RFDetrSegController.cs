using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RFDetrSegController : MonoBehaviour
{
    public enum InputMode { Camera, UITexture }

    [Header("Input Settings")]
    [Tooltip("Choose whether to use camera input or a 2D RawImage as source")]
    public InputMode inputMode = InputMode.UITexture;

    [Tooltip("Camera to use if Input Mode = Camera")]
    public Camera sourceCamera;

    [Tooltip("RawImage to use if Input Mode = UITexture")]
    public RawImage sourceImage;

    [Header("Roboflow API Settings")]
    [Tooltip("Base endpoint for Roboflow inference (hosted: https://detect.roboflow.com)")]
    public string inferenceServer = "https://detect.roboflow.com";

    [Tooltip("API route (usually '/' for hosted)")]
    public string inferRoute = "/";

    [Tooltip("Model ID, e.g. coco-2017/1 or rfdetr-seg-preview")]
    public string modelId = "rfdetr-seg-preview";

    [Tooltip("Your Roboflow API Key")]
    public string apiKey = "YOUR_API_KEY";

    [Header("UI Elements")]
    public TMP_Text statusText;
    public RawImage segmentationOverlay;

    [Header("Capture Settings")]
    [Tooltip("Resolution for camera capture (if using camera input)")]
    public Vector2Int captureResolution = new Vector2Int(512, 512);

    private HttpClient _client;

    private void Awake()
    {
        _client = new HttpClient();
    }

    [ContextMenu("Send Image for Segmentation")]
    public void SendImageForSegmentation()
    {
        StartCoroutine(SendImageCoroutine());
    }

    private IEnumerator SendImageCoroutine()
    {
        if (statusText) statusText.text = "Preparing image...";

        Texture2D tex = null;

        if (inputMode == InputMode.Camera && sourceCamera != null)
        {
            tex = CaptureFromCamera();
        }
        else if (inputMode == InputMode.UITexture && sourceImage != null)
        {
            tex = ConvertToTexture2D(sourceImage.texture);
        }

        if (tex == null)
        {
            Debug.LogError("No input texture found!");
            if (statusText) statusText.text = "Error: No input found.";
            yield break;
        }

        byte[] imageBytes = tex.EncodeToJPG(75);
        string imageBase64 = Convert.ToBase64String(imageBytes);

        if (statusText) statusText.text = "Sending to Roboflow...";
        yield return SendRequestAsync(imageBase64);
    }

    private IEnumerator SendRequestAsync(string imageBase64)
    {
        string url = $"{inferenceServer.TrimEnd('/')}/{modelId}?api_key={apiKey}&confidence=0.5&overlap=0.5";

        var jsonBody = new { image = imageBase64 };
        string json = JsonConvert.SerializeObject(jsonBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        Task<HttpResponseMessage> postTask = _client.PostAsync(url, content);
        while (!postTask.IsCompleted) yield return null;

        if (postTask.IsFaulted || !postTask.Result.IsSuccessStatusCode)
        {
            string errorMsg = postTask.IsFaulted
                ? postTask.Exception?.ToString()
                : postTask.Result.ReasonPhrase;
            Debug.LogError($"Request failed: {errorMsg}");
            if (statusText) statusText.text = $"Error: {errorMsg}";
            yield break;
        }

        string result = postTask.Result.Content.ReadAsStringAsync().Result;
        ProcessResponse(result);
    }

    private void ProcessResponse(string json)
    {
        try
        {
            var response = JsonConvert.DeserializeObject<RoboflowResponse>(json);

            if (response == null || response.predictions == null || response.predictions.Count == 0)
            {
                if (statusText) statusText.text = "No objects detected.";
                Debug.LogWarning("No predictions received.");
                return;
            }

            if (statusText) statusText.text = $"Detected {response.predictions.Count} objects.";

            if (segmentationOverlay != null && sourceImage != null)
            {
                segmentationOverlay.texture = CreateMaskOverlay(response,
                    sourceImage.texture.width, sourceImage.texture.height);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing response: {e}");
            if (statusText) statusText.text = "Failed to parse response.";
        }
    }

    private Texture2D CreateMaskOverlay(RoboflowResponse response, int width, int height)
    {
        Texture2D maskTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);

        foreach (var pred in response.predictions)
        {
            if (string.IsNullOrEmpty(pred.mask)) continue;

            try
            {
                byte[] maskBytes = Convert.FromBase64String(pred.mask);
                Texture2D decodedMask = new Texture2D(1, 1);
                decodedMask.LoadImage(maskBytes);

                Color randColor = new Color(UnityEngine.Random.value,
                                            UnityEngine.Random.value,
                                            UnityEngine.Random.value, 0.5f);

                for (int y = 0; y < decodedMask.height; y++)
                {
                    for (int x = 0; x < decodedMask.width; x++)
                    {
                        Color c = decodedMask.GetPixel(x, y);
                        if (c.a > 0.1f)
                        {
                            int idx = y * width + x;
                            if (idx < pixels.Length)
                                pixels[idx] = Color.Lerp(pixels[idx], randColor, c.a);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to decode mask: {e.Message}");
            }
        }

        maskTex.SetPixels32(pixels);
        maskTex.Apply();
        return maskTex;
    }

    private Texture2D CaptureFromCamera()
    {
        RenderTexture rt = new RenderTexture(captureResolution.x, captureResolution.y, 24);
        sourceCamera.targetTexture = rt;

        Texture2D tex = new Texture2D(captureResolution.x, captureResolution.y, TextureFormat.RGB24, false);
        sourceCamera.Render();
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, captureResolution.x, captureResolution.y), 0, 0);
        tex.Apply();

        sourceCamera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);

        return tex;
    }

    public static Texture2D ConvertToTexture2D(Texture texture)
    {
        if (texture is Texture2D t2d) return t2d;

        RenderTexture tmp = RenderTexture.GetTemporary(texture.width, texture.height, 0);
        Graphics.Blit(texture, tmp);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = tmp;
        Texture2D result = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        result.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(tmp);
        return result;
    }
}

[System.Serializable]
public class RoboflowResponse
{
    public List<RFDetection> predictions;
}

[System.Serializable]
public class RFDetection
{
    public string @class;
    public float confidence;
    public float x;
    public float y;
    public float width;
    public float height;
    public string mask; // base64-encoded PNG mask for segmentation
}
