﻿using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.IO;

namespace Soundboard4MacroDeck.Services;

/// <summary>
/// AudioFileReader simplifies opening an audio file in NAudio
/// Simply pass in the bytes, and it will attempt to open the
/// file and set up a conversion path that turns into PCM IEEE float.
/// ACM codecs will be used for conversion.
/// It provides a volume property and implements both WaveStream and
/// ISampleProvider, making it possibly the only stage in your audio
/// pipeline necessary for simple playback scenarios
/// </summary>
public class AudioBytesReader : WaveStream, ISampleProvider
{
    private Stream _sourceStream; // take the byte array and hold it here
    private WaveStream _readerStream; // the waveStream which we will use for all positioning
    private readonly SampleChannel _sampleChannel; // sample provider that gives us most stuff we need
    private readonly int _destBytesPerSample;
    private readonly int _sourceBytesPerSample;
    private readonly long _length;
    private readonly object _lockObject;

    /// <summary>
    /// Initializes a new instance of AudioFileReader
    /// </summary>
    /// <param name="fileName">The file to open</param>
    /// <param name="fileData"></param>
    public AudioBytesReader(string fileName, byte[] fileData)
    {
        _lockObject = new();
        FileName = fileName;
        _sourceStream = new MemoryStream(fileData);
        CreateReaderStream(fileName);
        _sourceBytesPerSample = (_readerStream.WaveFormat.BitsPerSample / 8) * _readerStream.WaveFormat.Channels;
        _sampleChannel = new(_readerStream, false);
        _destBytesPerSample = 4 * _sampleChannel.WaveFormat.Channels;
        _length = SourceToDest(_readerStream.Length);
    }

    /// <summary>
    /// Creates the reader stream, supporting all filetypes in the core NAudio library,
    /// and ensuring we are in PCM format
    /// </summary>
    /// <param name="fileName">File Name</param>
    private void CreateReaderStream(string fileName)
    {
        if (fileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
        {
            _readerStream = new WaveFileReader(_sourceStream);
            if (_readerStream.WaveFormat.Encoding != WaveFormatEncoding.Pcm && _readerStream.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            {
                _readerStream = WaveFormatConversionStream.CreatePcmStream(_readerStream);
                _readerStream = new BlockAlignReductionStream(_readerStream);
            }
        }
        else if (fileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            _readerStream = new Mp3FileReader(_sourceStream);
        }
        else if (fileName.EndsWith(".aiff", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".aif", StringComparison.OrdinalIgnoreCase))
        {
            _readerStream = new AiffFileReader(_sourceStream);
        }
        else
        {
            // fall back to media foundation reader, see if that can play it
            _readerStream = new StreamMediaFoundationReader(_sourceStream);
        }
    }
    /// <summary>
    /// File Name
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// WaveFormat of this stream
    /// </summary>
    public override WaveFormat WaveFormat => _sampleChannel.WaveFormat;

    /// <summary>
    /// Length of this stream (in bytes)
    /// </summary>
    public override long Length => _length;

    /// <summary>
    /// Position of this stream (in bytes)
    /// </summary>
    public override long Position
    {
        get { return SourceToDest(_readerStream.Position); }
        set { lock (_lockObject) { _readerStream.Position = DestToSource(value); } }
    }

    /// <summary>
    /// Reads from this wave stream, choosing whether to loop or read once
    /// </summary>
    /// <param name="buffer">Audio buffer</param>
    /// <param name="offset">Offset into buffer</param>
    /// <param name="count">Number of bytes required</param>
    /// <returns>Number of bytes read</returns>
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (!LoopingEnabled)
        {
            return ReadInt(buffer, offset, count);
        }

        return ReadLoop(buffer, offset, count);
    }

    /// <summary>
    /// Reads audio from this sample provider
    /// </summary>
    /// <param name="buffer">Sample buffer</param>
    /// <param name="offset">Offset into sample buffer</param>
    /// <param name="count">Number of samples required</param>
    /// <returns>Number of samples read</returns>
    public int Read(float[] buffer, int offset, int count)
    {
        lock (_lockObject)
        {
            return _sampleChannel.Read(buffer, offset, count);
        }
    }

    private int ReadInt(byte[] buffer, int offset, int count)
    {
        var waveBuffer = new WaveBuffer(buffer);
        int samplesRequired = count / 4;
        int samplesRead = Read(waveBuffer.FloatBuffer, offset / 4, samplesRequired);
        return samplesRead * 4;
    }

    private int ReadLoop(byte[] buffer, int offset, int count)
    {
        int read = 0;
        while (read < count)
        {
            int required = count - read;
            int readThisTime = ReadInt(buffer, offset + read, required);
            if (readThisTime < required)
            {
                Position = 0;
            }

            if (Position >= Length)
            {
                Position = 0;
            }
            read += readThisTime;
        }
        return read;
    }

    public bool LoopingEnabled { get; set; }

    /// <summary>
    /// Gets or Sets the Volume of this AudioFileReader. 1.0f is full volume
    /// </summary>
    public float Volume
    {
        get { return _sampleChannel.Volume; }
        set { _sampleChannel.Volume = value; }
    }

    /// <summary>
    /// Helper to convert source to dest bytes
    /// </summary>
    private long SourceToDest(long sourceBytes)
    {
        return _destBytesPerSample * (sourceBytes / _sourceBytesPerSample);
    }

    /// <summary>
    /// Helper to convert dest to source bytes
    /// </summary>
    private long DestToSource(long destBytes)
    {
        return _sourceBytesPerSample * (destBytes / _destBytesPerSample);
    }

    /// <summary>
    /// Disposes this AudioFileReader
    /// </summary>
    /// <param name="disposing">True if called from Dispose</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_readerStream != null)
            {
                _readerStream.Dispose();
                _readerStream = null;
            }
            if (_sourceStream != null)
            {
                _sourceStream.Dispose();
                _sourceStream = null;
            }
        }
        base.Dispose(disposing);
    }
}