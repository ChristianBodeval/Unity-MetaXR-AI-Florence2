using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

public class CocoSegmentationBridge : MonoBehaviour
{
    [Header("COCO Segmentation API")]
    [Tooltip("Optional HuggingFace API token (anonymous is fine but slower).")]
    public string huggingFaceApiKey = "";
    private const string ApiUrl = "https://api-inference.huggingface.co/models/facebook/maskrcnn-resnet50-fpn";

    [Header("Debug")]
    public RawImage debugMaskOutput;
    public Color maskColor = new Color(0f, 1f, 0f, 0.3f);

    private HttpClient client;

    private void Awake()
    {
        client = new HttpClient();
        if (!string.IsNullOrEmpty(huggingFaceApiKey))
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", huggingFaceApiKey);
    }

    /// <summary>
    /// Sends an image to COCO segmentation model and returns a mask covering (u,v).
    /// </summary>
    public async Task<Texture2D> GetMaskAtPointAsync(Texture2D image, Vector2 normalizedUV)
    {
        if (image == null)
        {
            Debug.LogError("[COCO] Image is null");
            return null;
        }

        byte[] jpgBytes = image.EncodeToJPG(75);
        var content = new ByteArrayContent(jpgBytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");

        try
        {
            var response = await client.PostAsync(ApiUrl, content);
            string json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Debug.LogError($"[COCO] Error: {response.StatusCode}\n{json}");
                return null;
            }

            // The endpoint returns a list of predictions (dicts)
            var results = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
            if (results == null || results.Count == 0)
            {
                Debug.LogWarning("[COCO] No detections.");
                return null;
            }

            int px = Mathf.RoundToInt(normalizedUV.x * image.width);
            int py = Mathf.RoundToInt((1f - normalizedUV.y) * image.height); // invert Y

            foreach (var item in results)
            {
                if (!item.ContainsKey("mask")) continue;
                string maskBase64 = item["mask"].ToString();
                byte[] maskBytes = Convert.FromBase64String(maskBase64);

                Texture2D maskTex = new Texture2D(2, 2);
                maskTex.LoadImage(maskBytes);

                Color c = maskTex.GetPixel(px, py);
                if (c.a > 0.1f || c.grayscale > 0.1f)
                    return maskTex;
            }

            Debug.Log("[COCO] No mask covered that point.");
            return null;
        }
        catch (Exception ex)
        {
            Debug.LogError("[COCO] Exception: " + ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Overlays the mask onto a RawImage for visualization.
    /// </summary>
    public void DrawMaskOverlay(Texture2D maskTex, RawImage target)
    {
        if (maskTex == null || target == null) return;

        Texture2D overlay = new Texture2D(maskTex.width, maskTex.height, TextureFormat.RGBA32, false);
        for (int y = 0; y < maskTex.height; y++)
        {
            for (int x = 0; x < maskTex.width; x++)
            {
                float a = maskTex.GetPixel(x, y).grayscale;
                overlay.SetPixel(x, y, new Color(maskColor.r, maskColor.g, maskColor.b, a * maskColor.a));
            }
        }

        overlay.Apply();
        target.texture = overlay;
        if (debugMaskOutput) debugMaskOutput.texture = overlay;
    }
}
