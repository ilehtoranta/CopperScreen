using System.Buffers.Binary;

namespace CopperMod.Amiga.Lightweight;

// The CD DAC consumes 588 stereo samples per sector at 44,100 Hz. Resample
// its held level at Paula's output sample edges on the same board clock.
internal sealed class LightweightCd32Audio(int clockFrequency)
{
    private const int CdSampleRate = 44_100;
    private const int OutputRate = LightweightPaulaAudio.OutputSampleRate;
    private readonly byte[] _sector = new byte[LightweightCd32DiscImage.RawSectorBytes];
    private readonly short[] _mix = new short[2048];
    private long _nextOutputSample = 1;
    private long _sectorStartCycle;
    private int _frameIndex;
    private bool _audible;
    private bool _hasMixedSamples;

    internal Span<byte> Sector => _sector;

    internal void BeginSector(long cycle)
    {
        _sectorStartCycle = cycle;
        _audible = true;
    }

    internal void Resume(long pausedCycles)
    {
        _sectorStartCycle += pausedCycles;
        _audible = true;
    }

    internal void Silence(long cycle)
    {
        RenderTo(cycle);
        _audible = false;
    }

    internal void Reset(long cycle, bool cold)
    {
        if (cold)
        {
            _nextOutputSample = 1;
            _frameIndex = 0;
            if (_hasMixedSamples) Array.Clear(_mix);
            _hasMixedSamples = false;
        }
        else RenderTo(cycle);
        _audible = false;
    }

    internal void Mix(Span<short> samples, long cycle)
    {
        RenderTo(cycle);
        System.Diagnostics.Debug.Assert(samples.Length == _frameIndex);
        if (_hasMixedSamples)
        {
            for (var i = 0; i < samples.Length; i++)
                samples[i] = (short)Math.Clamp(samples[i] + _mix[i], short.MinValue, short.MaxValue);
            Array.Clear(_mix, 0, _frameIndex);
        }
        _frameIndex = 0;
        _hasMixedSamples = false;
    }

    internal void RenderTo(long cycle)
    {
        var sampleAtCycle = cycle * OutputRate / clockFrequency;
        if (sampleAtCycle < _nextOutputSample) return;
        if (!_audible)
        {
            _frameIndex += (int)(sampleAtCycle - _nextOutputSample + 1) * 2;
            _nextOutputSample = sampleAtCycle + 1;
            return;
        }

        var offset = _nextOutputSample * clockFrequency - _sectorStartCycle * OutputRate;
        var scale = (long)clockFrequency * OutputRate;
        while (_nextOutputSample <= sampleAtCycle)
        {
            var cdSample = (int)Math.Clamp(offset * CdSampleRate / scale, 0, 587) * 4;
            _mix[_frameIndex++] = BinaryPrimitives.ReadInt16LittleEndian(_sector.AsSpan(cdSample));
            _mix[_frameIndex++] = BinaryPrimitives.ReadInt16LittleEndian(_sector.AsSpan(cdSample + 2));
            offset += clockFrequency;
            _nextOutputSample++;
        }
        _hasMixedSamples = true;
    }
}
