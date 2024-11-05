using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FenixProLoudnessMatch.Models
{
    public class Stream
    {
        [JsonPropertyName("index")]
        public int? Index { get; set; }

        [JsonPropertyName("codec_name")]
        public string? CodecName { get; set; }

        [JsonPropertyName("codec_long_name")]
        public string? CodecLongName { get; set; }

        [JsonPropertyName("codec_type")]
        public string? CodecType { get; set; }

        [JsonPropertyName("codec_time_base")]
        public string? CodecTimeBase { get; set; }

        [JsonPropertyName("codec_tag_string")]
        public string? CodecTagString { get; set; }

        [JsonPropertyName("codec_tag")]
        public string? CodecTag { get; set; }

        [JsonPropertyName("width")]
        public int? Width { get; set; }

        [JsonPropertyName("height")]
        public int? Height { get; set; }

        [JsonPropertyName("has_b_frames")]
        public int? HasBFrames { get; set; }

        [JsonPropertyName("pix_fmt")]
        public string? PixFmt { get; set; }

        [JsonPropertyName("level")]
        public int? Level { get; set; }

        [JsonPropertyName("is_avc")]
        public string? IsAvc { get; set; }

        [JsonPropertyName("nal_length_size")]
        public string? NalLengthSize { get; set; }

        [JsonPropertyName("r_frame_rate")]
        public string? RFrameRate { get; set; }

        [JsonPropertyName("avg_frame_rate")]
        public string? AvgFrameRate { get; set; }

        [JsonPropertyName("time_base")]
        public string? TimeBase { get; set; }

        [JsonPropertyName("start_time")]
        public string? StartTime { get; set; }

        [JsonPropertyName("duration")]
        public string? Duration { get; set; }

        [JsonPropertyName("bit_rate")]
        public string? BitRate { get; set; }

        [JsonPropertyName("nb_frames")]
        public string? NbFrames { get; set; }

        [JsonPropertyName("sample_fmt")]
        public string? SampleFmt { get; set; }

        [JsonPropertyName("sample_rate")]
        public string SampleRate { get; set; }

        [JsonPropertyName("channels")]
        public int Channels { get; set; }

        [JsonPropertyName("bits_per_sample")]
        public int? BitsPerSample { get; set; }

        [JsonPropertyName("tags")]
        public Tags? Tags { get; set; } = new Tags();
    }

    public class Tags
    {
        [JsonPropertyName("creation_time")]
        public string? CreationTime { get; set; }

        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("handler_name")]
        public string? HandlerName { get; set; }
    }

    public class FormatTags
    {
        [JsonPropertyName("major_brand")]
        public string? MajorBrand { get; set; }

        [JsonPropertyName("minor_version")]
        public string? MinorVersion { get; set; }

        [JsonPropertyName("compatible_brands")]
        public string? CompatibleBrands { get; set; }

        [JsonPropertyName("creation_time")]
        public string? CreationTime { get; set; }
    }

    public class Format
    {
        [JsonPropertyName("filename")]
        public string? Filename { get; set; }

        [JsonPropertyName("nb_streams")]
        public int? NbStreams { get; set; }

        [JsonPropertyName("format_name")]
        public string? FormatName { get; set; }

        [JsonPropertyName("format_long_name")]
        public string? FormatLongName { get; set; }

        [JsonPropertyName("start_time")]
        public string? StartTime { get; set; }

        [JsonPropertyName("duration")]
        public string? Duration { get; set; }

        [JsonPropertyName("size")]
        public string? Size { get; set; }

        [JsonPropertyName("bit_rate")]
        public string? BitRate { get; set; }

        [JsonPropertyName("tags")]
        public FormatTags? Tags { get; set; } = new FormatTags();
    }

    public class FFProbeOutput
    {
        [JsonPropertyName("streams")]
        public List<Stream>? Streams { get; set; } = new List<Stream>();

        [JsonPropertyName("format")]
        public Format? Format { get; set; } = new Format();
    }
}
