using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AlphaTab.Core;
using AlphaTab.Core.EcmaScript;
using AlphaTab.Synth;
using AlphaTab.Synth.Ds;
using NAudio.Wave;

namespace AlphaTab;

internal class NAudioSynthOutput : WaveProvider32, ISynthOutput, IDisposable
{
    private const int BufferSize = 4096;
    private const int PreferredSampleRate = 44100;
    private const int DirectSoundLatency = 40;

    private DirectSoundOut? _context;
    private CircularSampleBuffer _circularBuffer;
    private int _bufferCount;
    private int _requestedBufferCount;
    private ISynthOutputDevice? _device;

    public double SampleRate => PreferredSampleRate;

    public NAudioSynthOutput()
        : base(PreferredSampleRate, (int)SynthConstants.AudioChannels)
    {
        _circularBuffer = null!;
    }

    public void Activate()
    {
    }

    public void Open(double bufferTimeInMilliseconds)
    {
        _context = new DirectSoundOut(DirectSoundLatency);
        _context.Init(this);
        _bufferCount = (int)(
            (bufferTimeInMilliseconds * PreferredSampleRate) /
            1000 /
            BufferSize
        ) * 4;
        _circularBuffer = new CircularSampleBuffer(BufferSize * _bufferCount);
        ((EventEmitter)Ready).Trigger();
    }

    public void Destroy()
    {
        Dispose();
    }

    public void Dispose()
    {
        Close();
    }

    public void Close()
    {
        _context!.Stop();
        _circularBuffer.Clear();
        _context.Dispose();
    }

    public void Play()
    {
        RequestBuffers();
        _context!.Play();
    }

    public void Pause()
    {
        if (_context!.PlaybackState == PlaybackState.Playing)
        {
            _context.Pause();
        }
    }

    public void AddSamples(Float32Array f)
    {
        _circularBuffer.Write(f, 0, f.Length);
        _requestedBufferCount--;
    }

    public void ResetSamples()
    {
        _circularBuffer.Clear();
    }

    private void RequestBuffers()
    {
        var halfBufferCount = _bufferCount / 2;
        var halfSamples = halfBufferCount * BufferSize;
        var bufferedSamples = _circularBuffer.Count + _requestedBufferCount * BufferSize;

        if (bufferedSamples < halfSamples)
        {
            for (var i = 0; i < halfBufferCount; i++)
            {
                ((EventEmitter)SampleRequest).Trigger();
                _requestedBufferCount++;
            }
        }
    }

    public override int Read(float[] buffer, int offset, int count)
    {
        var read = new Float32Array(count);
        var samplesFromBuffer = (int)_circularBuffer.Read(read, 0,
            System.Math.Min(read.Length, _circularBuffer.Count));

        Buffer.BlockCopy(read.Data.Array!, read.Data.Offset, buffer, offset * sizeof(float),
            samplesFromBuffer * sizeof(float));

        ((EventEmitterOfT<double>)SamplesPlayed).Trigger(samplesFromBuffer /
                                                         SynthConstants.AudioChannels);
        RequestBuffers();
        return count;
    }

    public IEventEmitter Ready { get; } = new EventEmitter();
    public IEventEmitterOfT<double> SamplesPlayed { get; } = new EventEmitterOfT<double>();
    public IEventEmitter SampleRequest { get; } = new EventEmitter();

    public Task<IList<ISynthOutputDevice>> EnumerateOutputDevices()
    {
        return Task.FromResult(EnumerateDirectSoundOutputDevices());
    }

    internal static IList<ISynthOutputDevice> EnumerateDirectSoundOutputDevices()
    {
        var defaultPlayback = DirectSoundOut.DSDEVID_DefaultPlayback;
        GetDeviceID(ref defaultPlayback, out var realDefault);

        return DirectSoundOut.Devices
            .Where(d => d.Guid != Guid.Empty)
            .Map(d => (ISynthOutputDevice)new NAudioOutputDevice(d, realDefault));
    }

    [DllImport("dsound.dll", CharSet = CharSet.Unicode,
        CallingConvention = CallingConvention.StdCall, SetLastError = true,
        PreserveSig = false)]
    private static extern void GetDeviceID(ref Guid pGuidSrc, out Guid pGuidDest);

    public Task SetOutputDevice(ISynthOutputDevice? device)
    {
        if (_context != null)
        {
            _context.Stop();
            _circularBuffer.Clear();
            _context.Dispose();
        }

        _context = new DirectSoundOut(
            device == null
                ? DirectSoundOut.DSDEVID_DefaultPlayback
                : ((NAudioOutputDevice)device).Device.Guid,
            DirectSoundLatency);
        _device = device;
        _context.Init(this);
        return Task.CompletedTask;
    }

    public Task<ISynthOutputDevice?> GetOutputDevice()
    {
        return Task.FromResult(_device);
    }
}

internal class NAudioOutputDevice : ISynthOutputDevice
{
    public DirectSoundDeviceInfo Device { get; }

    public NAudioOutputDevice(DirectSoundDeviceInfo device, Guid defaultDevice)
    {
        Device = device;
        IsDefault = device.Guid == defaultDevice;
    }

    public string DeviceId => Device.Guid.ToString("N");
    public string Label => Device.Description;
    public bool IsDefault { get; }
}