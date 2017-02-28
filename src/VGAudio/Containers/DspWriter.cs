﻿using System;
using System.IO;
using VGAudio.Containers.Dsp;
using VGAudio.Formats;
using VGAudio.Utilities;
using static VGAudio.Formats.GcAdpcm.GcAdpcmHelpers;
using static VGAudio.Utilities.Helpers;

#if NET20
using VGAudio.Compatibility.LinqBridge;
#else
using System.Linq;
#endif

namespace VGAudio.Containers
{
    public class DspWriter : AudioWriter<DspWriter, DspConfiguration>
    {
        private GcAdpcmFormat Adpcm { get; set; }

        protected override int FileSize => (HeaderSize + AudioDataSize) * ChannelCount;

        private static int HeaderSize => 0x60;
        private int ChannelCount => Adpcm.ChannelCount;

        private int SampleCount => (Configuration.TrimFile && Adpcm.Looping ? LoopEnd : Math.Max(Adpcm.SampleCount, LoopEnd));
        private short Format { get; } = 0; /* 0 for ADPCM */

        private int SamplesPerInterleave => Configuration.SamplesPerInterleave;
        private int BytesPerInterleave => SampleCountToByteCount(SamplesPerInterleave);
        private int FramesPerInterleave => BytesPerInterleave / BytesPerFrame;

        private int AlignmentSamples => GetNextMultiple(Adpcm.LoopStart, Configuration.LoopPointAlignment) - Adpcm.LoopStart;
        private int LoopStart => Adpcm.LoopStart + AlignmentSamples;
        private int LoopEnd => Adpcm.LoopEnd + AlignmentSamples;

        private int StartAddr => SampleToNibble(Adpcm.Looping ? LoopStart : 0);
        private int EndAddr => SampleToNibble(Adpcm.Looping ? LoopEnd : SampleCount - 1);
        private static int CurAddr => SampleToNibble(0);

        protected override void SetupWriter(AudioData audio)
        {
            Adpcm = audio.GetFormat<GcAdpcmFormat>();
        }

        protected override void WriteStream(Stream stream)
        {
            //RecalculateData();

            using (BinaryWriter writer = GetBinaryWriter(stream, Endianness.BigEndian))
            {
                stream.Position = 0;
                WriteHeader(writer);
                WriteData(writer);
            }
        }

        private void WriteHeader(BinaryWriter writer)
        {
            for (int i = 0; i < ChannelCount; i++)
            {
                var channel = Adpcm.Channels[i];
                writer.BaseStream.Position = HeaderSize * i;
                writer.Write(SampleCount);
                writer.Write(SampleCountToNibbleCount(SampleCount));
                writer.Write(Adpcm.SampleRate);
                writer.Write((short)(Adpcm.Looping ? 1 : 0));
                writer.Write(Format);
                writer.Write(StartAddr);
                writer.Write(EndAddr);
                writer.Write(CurAddr);
                writer.Write(channel.Coefs.ToByteArray(Endianness.BigEndian));
                writer.Write(channel.Gain);
                writer.Write(channel.PredScale);
                writer.Write(channel.Hist1);
                writer.Write(channel.Hist2);
                writer.Write(channel.LoopPredScale(LoopStart));
                writer.Write(channel.LoopHist1(LoopStart));
                writer.Write(channel.LoopHist2(LoopStart));
                writer.Write((short)(ChannelCount == 1 ? 0 : ChannelCount));
                writer.Write((short)(ChannelCount == 1 ? 0 : FramesPerInterleave));
            }
        }

        private void WriteData(BinaryWriter writer)
        {
            writer.BaseStream.Position = HeaderSize * ChannelCount;
            if (ChannelCount == 1)
            {
                writer.Write(Adpcm.Channels[0].GetAudioData(), 0, SampleCountToByteCount(SampleCount));
            }
            else
            {
                byte[][] channels = Adpcm.Channels.Select(x => x.GetAudioData()).ToArray();
                channels.Interleave(writer.BaseStream, BytesPerInterleave, AudioDataSize);
            }
        }

        /// <summary>
        /// Size of a single channel's ADPCM audio data with padding when written to a file
        /// </summary>
        private int AudioDataSize
            => GetNextMultiple(SampleCountToByteCount(SampleCount), ChannelCount == 1 ? 1 : BytesPerFrame);
    }
}