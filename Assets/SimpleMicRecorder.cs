using System;
using System.IO;
using UnityEngine;
using NaughtyAttributes;

public class SimpleMicRecorder : MonoBehaviour
{
    [Header("Mic Settings")]
    [Tooltip("Length of the recording loop buffer in seconds.")]
    public int bufferLength = 5;   // seconds

    [Tooltip("Sample rate in Hz (16000, 44100, 48000 etc.).")]
    public int sampleRate = 16000;

    [Tooltip("Index of microphone device (0 = first).")]
    public int deviceIndex = 0;

    [Header("Save Options")]
    [Tooltip("For 'Save Last N Seconds' button.")]
    [MinValue(1)]
    public int lastSecondsToSave = 3;

    [Header("Runtime Info (read-only)")]
    [ReadOnly] public AudioClip micClip;
    [ReadOnly] public string deviceName;
    [ReadOnly] public int clipPosition; // frames
    [ReadOnly] public bool isRecording;
    [ReadOnly] public int channels;

    // ─────────────────────────────────────────────────────────────────────
    // Buttons (NaughtyAttributes)
    // ─────────────────────────────────────────────────────────────────────

    [Button("Start Recording")]
    public void StartRecording()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("No microphone devices found!");
            return;
        }

        if (isRecording)
        {
            Debug.LogWarning("Already recording!");
            return;
        }

        deviceIndex = Mathf.Clamp(deviceIndex, 0, Microphone.devices.Length - 1);
        deviceName = Microphone.devices[deviceIndex];

        micClip = Microphone.Start(deviceName, true, Mathf.Max(1, bufferLength), sampleRate);
        if (micClip == null)
        {
            Debug.LogError("Microphone.Start returned null AudioClip.");
            return;
        }

        // Unity returns 0 channels at first frame sometimes; grab on next update if needed
        channels = Mathf.Max(1, micClip.channels);
        isRecording = true;
        Debug.Log($"[Mic] Started '{deviceName}', {sampleRate} Hz, {bufferLength}s buffer.");
    }

    [Button("Stop Recording")]
    public void StopRecording()
    {
        if (!isRecording || string.IsNullOrEmpty(deviceName))
        {
            Debug.LogWarning("Not recording.");
            return;
        }

        Microphone.End(deviceName);
        isRecording = false;
        Debug.Log($"[Mic] Stopped '{deviceName}'.");
    }

    [Button("Save WAV (Full Buffer)")]
    public void SaveFullBuffer()
    {
        if (!ValidateReadyForSave()) return;

        // Pull whole buffer (all frames currently allocated in the clip)
        int totalFrames = micClip.samples;           // frames per channel
        float[] interleaved = ReadInterleavedCircular(lastFrames: totalFrames);
        string path = MakeWavPath("full");
        WriteWav16(path, interleaved, sampleRate, channels);
        Debug.Log($"[Mic] Saved full buffer WAV: {path}");
    }

    [Button("Save WAV (Last N Seconds)")]
    public void SaveLastNSeconds()
    {
        if (!ValidateReadyForSave()) return;

        int framesRequested = Mathf.Clamp(lastSecondsToSave, 1, bufferLength) * sampleRate;
        float[] interleaved = ReadInterleavedCircular(lastFrames: framesRequested);
        string path = MakeWavPath($"{lastSecondsToSave}s");
        WriteWav16(path, interleaved, sampleRate, channels);
        Debug.Log($"[Mic] Saved last {lastSecondsToSave}s WAV: {path}");
    }

    // ─────────────────────────────────────────────────────────────────────
    // MonoBehaviour
    // ─────────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (isRecording && !string.IsNullOrEmpty(deviceName))
        {
            clipPosition = Microphone.GetPosition(deviceName); // in frames
            if (micClip) channels = Mathf.Max(1, micClip.channels);
        }
    }

    private void OnDisable()
    {
        if (isRecording) StopRecording();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────

    private bool ValidateReadyForSave()
    {
        if (!micClip)
        {
            Debug.LogWarning("No mic clip to save.");
            return false;
        }
        if (!isRecording || string.IsNullOrEmpty(deviceName))
        {
            // You *can* save after stopping, but for latest audio we typically grab while recording.
            Debug.Log($"[Mic] Not currently recording; using last captured data in clip.");
        }
        if (!micClip.isReadyToPlay)
        {
            Debug.LogWarning("Mic clip not ready.");
            return false;
        }
        channels = Mathf.Max(1, micClip.channels);
        return true;
    }

    private string MakeWavPath(string suffix)
    {
        string file = $"mic_{DateTime.Now:yyyyMMdd_HHmmss}_{suffix}.wav";
        return Path.Combine(Application.persistentDataPath, file);
    }

    /// <summary>
    /// Reads the latest N frames (per channel) from the circular buffer and returns an interleaved float[].
    /// Handles wrap-around using the current mic write-head (clipPosition).
    /// </summary>
    private float[] ReadInterleavedCircular(int lastFrames)
    {
        int totalFrames = micClip.samples; // frames per channel
        int framesToCopy = Mathf.Clamp(lastFrames, 1, totalFrames);

        // Current write head in frames (per channel)
        int writeHead = Mathf.Clamp(clipPosition, 0, totalFrames);

        // Compute start frame for the segment we want (wrap around if negative)
        int startFrame = writeHead - framesToCopy;
        if (startFrame < 0) startFrame += totalFrames;

        // Pull entire clip data once, then slice
        float[] full = new float[totalFrames * channels]; // interleaved
        micClip.GetData(full, 0);

        // Extract interleaved segment with wrap
        float[] segment = new float[framesToCopy * channels];
        int framesRemaining = framesToCopy;

        // First chunk: from startFrame to end of buffer (or framesToCopy)
        int firstChunkFrames = Mathf.Min(framesRemaining, totalFrames - startFrame);
        CopyInterleavedFrames(full, segment, startFrame, 0, firstChunkFrames, channels, totalFrames);

        framesRemaining -= firstChunkFrames;
        if (framesRemaining > 0)
        {
            // Wrap chunk: from 0 to framesRemaining
            CopyInterleavedFrames(full, segment, 0, firstChunkFrames, framesRemaining, channels, totalFrames);
        }

        return segment;
    }

    /// <summary>
    /// Copies a range of frames (interleaved) from src to dst.
    /// </summary>
    private static void CopyInterleavedFrames(
        float[] src, float[] dst,
        int srcStartFrame, int dstStartFrame,
        int frameCount, int channels, int totalFrames)
    {
        // Convert frames to sample indices (interleaved)
        int srcStartSample = srcStartFrame * channels;
        int dstStartSample = dstStartFrame * channels;
        int samplesToCopy = frameCount * channels;

        Array.Copy(src, srcStartSample, dst, dstStartSample, samplesToCopy);
    }

    // ─────────────────────────────────────────────────────────────────────
    // WAV Writer (16-bit PCM)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes a standard RIFF/WAVE file (16-bit PCM) from interleaved float samples (-1..1).
    /// </summary>
    private static void WriteWav16(string path, float[] interleaved, int sampleRate, int channels)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var bw = new BinaryWriter(fs))
        {
            int bitsPerSample = 16;
            int byteRate = sampleRate * channels * bitsPerSample / 8;
            short blockAlign = (short)(channels * bitsPerSample / 8);
            byte[] pcm16 = FloatToPCM16(interleaved);

            // RIFF header
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + pcm16.Length);                     // ChunkSize = 36 + Subchunk2Size
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            // fmt  subchunk
            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);                                    // Subchunk1Size for PCM
            bw.Write((short)1);                              // AudioFormat = 1 (PCM)
            bw.Write((short)channels);                       // NumChannels
            bw.Write(sampleRate);                            // SampleRate
            bw.Write(byteRate);                              // ByteRate
            bw.Write(blockAlign);                            // BlockAlign
            bw.Write((short)bitsPerSample);                  // BitsPerSample

            // data subchunk
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(pcm16.Length);                          // Subchunk2Size
            bw.Write(pcm16);
        }
    }

    private static byte[] FloatToPCM16(float[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        int i = 0, bi = 0;
        while (i < samples.Length)
        {
            // clamp & convert
            float f = Mathf.Clamp(samples[i], -1f, 1f);
            short s = (short)Mathf.RoundToInt(f * 32767f);
            bytes[bi++] = (byte)(s & 0xff);
            bytes[bi++] = (byte)((s >> 8) & 0xff);
            i++;
        }
        return bytes;
    }
}
