using System;
using Pressio.Services;
using Xunit;

namespace Pressio.Tests;

public class SevenSegmentDecoderTests
{
    // segmentos na ordem (a,b,c,d,e,f,g) para cada dígito.
    private static readonly bool[][] Patterns =
    {
        new[]{true,true,true,true,true,true,false},   // 0
        new[]{false,true,true,false,false,false,false}, // 1
        new[]{true,true,false,true,true,false,true},  // 2
        new[]{true,true,true,true,false,false,true},  // 3
        new[]{false,true,true,false,false,true,true}, // 4
        new[]{true,false,true,true,false,true,true},  // 5
        new[]{true,false,true,true,true,true,true},   // 6
        new[]{true,true,true,false,false,false,false}, // 7
        new[]{true,true,true,true,true,true,true},    // 8
        new[]{true,true,true,true,false,true,true},   // 9
    };

    [Fact]
    public void Decode_MapsAllDigits()
    {
        for (var d = 0; d <= 9; d++)
        {
            var s = Patterns[d];
            Assert.Equal((char)('0' + d), SevenSegmentDecoder.Decode(s[0], s[1], s[2], s[3], s[4], s[5], s[6]));
        }
    }

    [Theory]
    [InlineData('0')]
    [InlineData('1')]
    [InlineData('2')]
    [InlineData('3')]
    [InlineData('4')]
    [InlineData('5')]
    [InlineData('6')]
    [InlineData('7')]
    [InlineData('8')]
    [InlineData('9')]
    public void DecodeRow_RecognizesSingleDigit(char digit)
    {
        var img = Render(digit, out var w, out var h);
        var result = SevenSegmentDecoder.DecodeRow(img, w, h, 0, 0, w, h);
        Assert.Equal(digit.ToString(), result);
    }

    [Fact]
    public void DecodeRow_RecognizesMultiDigitRow()
    {
        var text = "129";
        var (img, w, h) = RenderWord(text);
        var result = SevenSegmentDecoder.DecodeRow(img, w, h, 0, 0, w, h);
        Assert.Equal(text, result);
    }

    private static (float[] img, int w, int h) RenderWord(string text)
    {
        _ = Render(text[0], out var w, out var h);
        var totalWidth = w * text.Length;
        var canvas = new float[totalWidth * h];
        Array.Fill(canvas, 255f);
        for (var i = 0; i < text.Length; i++)
        {
            var digit = Render(text[i], out _, out _);
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                    canvas[y * totalWidth + (x + i * w)] = digit[y * w + x];
        }
        return (canvas, totalWidth, h);
    }

    private static float[] Render(char digit, out int w, out int h)
    {
        w = 48; h = 76;
        var img = new float[w * h];
        Array.Fill(img, 255f);
        var segments = Patterns[digit - '0'];
        var dirs = new[]
        {
            (0.5f, 0.12f), (0.86f, 0.34f), (0.86f, 0.66f), (0.5f, 0.88f),
            (0.14f, 0.66f), (0.14f, 0.34f), (0.5f, 0.5f)
        };
        for (var s = 0; s < 7; s++)
        {
            if (!segments[s]) continue;
            var (fx, fy) = dirs[s];
            var cx = fx * w; var cy = fy * h;
            int x0, x1, y0, y1;
            if (s is 0 or 3 or 6) // horizontais: a, d, g
            {
                x0 = (int)(cx - 0.30f * w); x1 = (int)(cx + 0.30f * w);
                y0 = (int)(cy - 5); y1 = (int)(cy + 5);
            }
            else // verticais: b, c, e, f
            {
                x0 = (int)(cx - 4); x1 = (int)(cx + 4);
                y0 = (int)(cy - 0.20f * h); y1 = (int)(cy + 0.20f * h);
            }
            for (var y = Math.Max(0, y0); y < Math.Min(h, y1); y++)
                for (var x = Math.Max(0, x0); x < Math.Min(w, x1); x++)
                    img[y * w + x] = 0f;
        }
        return img;
    }
}
