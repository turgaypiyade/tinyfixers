#if UNITY_EDITOR
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// Editor-only gameplay video capture for social clips. Each end-of-frame the Game view (overlay canvases
/// included) is copied into one reusable RenderTexture/Texture2D and piped as raw RGBA into ffmpeg — no
/// per-frame allocations, no image files on disk. Game time is locked to the video by Time.captureFramerate,
/// so a slow encoder never makes the clip stutter. Game audio is rendered in lock-step by AudioRenderer into a
/// 16-bit WAV and muxed into the final mp4 when the clip ends.
/// </summary>
public sealed class RuntimeGameplayRecorder : MonoBehaviour
{
    private string _ffmpeg;
    private int _fps;
    private string _pendingPath;
    private Process _process;
    private Stream _stdin;
    private RenderTexture _rt;
    private Texture2D _readback;
    private byte[] _frame;
    private int _oldCaptureFramerate;
    private string _finalPath, _videoPath, _wavPath;
    private FileStream _wav;
    private long _wavSamples;
    private int _channels, _sampleRate;
    private NativeArray<float> _audio;
    private byte[] _pcm = Array.Empty<byte>();

    public static RuntimeGameplayRecorder Create(string ffmpegPath, int fps)
    {
        var go = new GameObject("Gameplay Recorder");
        DontDestroyOnLoad(go);
        var recorder = go.AddComponent<RuntimeGameplayRecorder>();
        recorder._ffmpeg = ffmpegPath;
        recorder._fps = Mathf.Clamp(fps, 15, 60);
        recorder._oldCaptureFramerate = Time.captureFramerate;
        Time.captureFramerate = recorder._fps;
        return recorder;
    }

    /// <summary>Starts a new clip; the encoder opens on the next captured frame (size is known then).</summary>
    public void BeginClip(string path)
    {
        EndClip();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        _finalPath = path;
        _videoPath = Path.ChangeExtension(path, ".video.mp4");
        _wavPath = Path.ChangeExtension(path, ".wav");
        _pendingPath = _videoPath;
    }

    public void EndClip()
    {
        _pendingPath = null;
        if (_process == null) return;
        try { _stdin?.Flush(); _stdin?.Close(); } catch (IOException) { }
        try { _process.WaitForExit(60000); } catch (InvalidOperationException) { }
        _process.Dispose();
        _process = null;
        _stdin = null;
        StopAudio();
        Mux();
    }

    private void StartAudio()
    {
        _sampleRate = AudioSettings.outputSampleRate;
        _channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
        _wav = new FileStream(_wavPath, FileMode.Create, FileAccess.Write);
        _wav.Write(new byte[44], 0, 44);   // header patched in StopAudio
        _wavSamples = 0;
        AudioRenderer.Start();
    }

    private void CaptureAudio()
    {
        if (_wav == null) return;
        int frames = AudioRenderer.GetSampleCountForCaptureFrame();
        int count = frames * _channels;
        if (count <= 0) return;
        if (!_audio.IsCreated || _audio.Length < count)
        {
            if (_audio.IsCreated) _audio.Dispose();
            _audio = new NativeArray<float>(count * 2, Allocator.Persistent);
        }
        var slice = _audio.GetSubArray(0, count);
        if (!AudioRenderer.Render(slice)) return;
        if (_pcm.Length < count * 2) _pcm = new byte[count * 4];
        for (int s = 0; s < count; s++)
        {
            short v = (short)(Mathf.Clamp(slice[s], -1f, 1f) * 32767f);
            _pcm[s * 2] = (byte)v;
            _pcm[s * 2 + 1] = (byte)(v >> 8);
        }
        _wav.Write(_pcm, 0, count * 2);
        _wavSamples += count;
    }

    private void StopAudio()
    {
        if (_wav == null) return;
        AudioRenderer.Stop();
        int dataBytes = (int)(_wavSamples * 2);
        _wav.Position = 0;
        using (var w = new BinaryWriter(_wav, System.Text.Encoding.ASCII, true))
        {
            w.Write("RIFF".ToCharArray()); w.Write(36 + dataBytes); w.Write("WAVE".ToCharArray());
            w.Write("fmt ".ToCharArray()); w.Write(16); w.Write((short)1); w.Write((short)_channels);
            w.Write(_sampleRate); w.Write(_sampleRate * _channels * 2); w.Write((short)(_channels * 2)); w.Write((short)16);
            w.Write("data".ToCharArray()); w.Write(dataBytes);
        }
        _wav.Dispose();
        _wav = null;
    }

    // Video + WAV → final mp4 (AAC). Temp files are removed only after a successful mux.
    private void Mux()
    {
        if (_finalPath == null || !File.Exists(_videoPath)) return;
        bool hasAudio = File.Exists(_wavPath) && new FileInfo(_wavPath).Length > 44;
        string args = hasAudio
            ? $"-y -loglevel error -i \"{_videoPath}\" -i \"{_wavPath}\" -c:v copy -c:a aac -b:a 192k -shortest -movflags +faststart \"{_finalPath}\""
            : $"-y -loglevel error -i \"{_videoPath}\" -c copy \"{_finalPath}\"";
        try
        {
            using var mux = Process.Start(new ProcessStartInfo(_ffmpeg, args) { UseShellExecute = false, CreateNoWindow = true });
            mux.WaitForExit(120000);
            if (mux.ExitCode == 0)
            {
                File.Delete(_videoPath);
                if (hasAudio) File.Delete(_wavPath);
            }
            else UnityEngine.Debug.LogError("[Recorder] mux failed for " + _finalPath);
        }
        catch (Exception ex) { UnityEngine.Debug.LogError("[Recorder] mux could not run: " + ex.Message); }
        _finalPath = null;
    }

    private void OnEnable() => StartCoroutine(CaptureLoop());

    private IEnumerator CaptureLoop()
    {
        var endOfFrame = new WaitForEndOfFrame();
        while (true)
        {
            yield return endOfFrame;
            if (_pendingPath == null && _process == null) continue;
            int w = Screen.width & ~1, h = Screen.height & ~1;   // x264 needs even sizes
            if (w <= 0 || h <= 0) continue;
            EnsureBuffers(w, h);
            if (_pendingPath != null && _process == null)
            {
                StartEncoder(_pendingPath, w, h);
                if (_process != null) StartAudio();
            }
            if (_process == null) continue;
            CaptureAudio();

            ScreenCapture.CaptureScreenshotIntoRenderTexture(_rt);
            var previous = RenderTexture.active;
            RenderTexture.active = _rt;
            _readback.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            RenderTexture.active = previous;
            _readback.GetRawTextureData<byte>().CopyTo(_frame);
            try { _stdin.Write(_frame, 0, _frame.Length); }
            catch (IOException) { UnityEngine.Debug.LogError("[Recorder] ffmpeg pipe closed."); EndClip(); }
        }
    }

    private void EnsureBuffers(int w, int h)
    {
        if (_rt != null && _rt.width == w && _rt.height == h) return;
        if (_process != null) EndClip();   // size changed mid-clip: close cleanly rather than corrupt
        if (_rt != null) { _rt.Release(); Destroy(_rt); }
        if (_readback != null) Destroy(_readback);
        _rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
        _readback = new Texture2D(w, h, TextureFormat.RGBA32, false);
        _frame = new byte[w * h * 4];
    }

    private void StartEncoder(string path, int w, int h)
    {
        // Rows arrive bottom-up → vflip. yuv420p + faststart = plays everywhere incl. Instagram.
        string args = $"-y -loglevel error -f rawvideo -pix_fmt rgba -s {w}x{h} -r {_fps} -i - " +
                      $"-vf vflip -c:v libx264 -preset medium -crf 17 -pix_fmt yuv420p -movflags +faststart \"{path}\"";
        var info = new ProcessStartInfo(_ffmpeg, args)
        {
            UseShellExecute = false, RedirectStandardInput = true, RedirectStandardError = false, CreateNoWindow = true,
        };
        try
        {
            _process = Process.Start(info);
            _stdin = _process.StandardInput.BaseStream;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError("[Recorder] ffmpeg could not start: " + ex.Message);
            _process = null;
        }
        _pendingPath = null;
    }

    private void OnDestroy()
    {
        EndClip();
        if (_audio.IsCreated) _audio.Dispose();
        Time.captureFramerate = _oldCaptureFramerate;
        if (_rt != null) { _rt.Release(); Destroy(_rt); }
        if (_readback != null) Destroy(_readback);
    }
}
#endif
