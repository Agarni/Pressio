using System;
using System.Collections.Generic;
using System.Text;

namespace Pressio.Services;

// Reconhecedor de dígitos de 7 segmentos (displays de monitores de pressão) a partir de pixels.
// O Apple Vision não lê esse tipo de display: os números são traços discretos, não letras.
// Aqui classificamos cada dígito por quais dos 7 segmentos estão acesos.
public static class SevenSegmentDecoder
{
    // Padrões canônicos na ordem fixa a,b,c,d,e,f,g -> dígito.
    private static readonly Dictionary<string, char> Patterns = new()
    {
        ["abcdef"] = '0',
        ["bc"] = '1',
        ["abdeg"] = '2',
        ["abcdg"] = '3',
        ["bcfg"] = '4',
        ["acdfg"] = '5',
        ["acdefg"] = '6',
        ["abc"] = '7',
        ["abcdefg"] = '8',
        ["abcdfg"] = '9',
    };

    public static char Decode(bool a, bool b, bool c, bool d, bool e, bool f, bool g)
    {
        var key = new StringBuilder(7);
        if (a) key.Append('a');
        if (b) key.Append('b');
        if (c) key.Append('c');
        if (d) key.Append('d');
        if (e) key.Append('e');
        if (f) key.Append('f');
        if (g) key.Append('g');
        return Patterns.TryGetValue(key.ToString(), out var ch) ? ch : '?';
    }

    // Decodifica uma região que contém uma única linha de dígitos 7-seg.
    // gray: pixels em tons de cinza [0..255], largura/altura da imagem inteira.
    // region: (x0,y0,x1,y1) em pixels da imagem.
    public static string DecodeRow(float[] gray, int width, int height, int x0, int y0, int x1, int y1)
    {
        if (width <= 0 || height <= 0 || x1 <= x0 || y1 <= y0) return string.Empty;
        x0 = Math.Max(0, x0); y0 = Math.Max(0, y0); x1 = Math.Min(width, x1); y1 = Math.Min(height, y1);
        var regionW = x1 - x0;
        var regionH = y1 - y0;
        if (regionW <= 0 || regionH <= 0) return string.Empty;

        var values = new float[regionH * regionW];
        var sum = 0f;
        for (var yy = y0; yy < y1; yy++)
            for (var xx = x0; xx < x1; xx++)
            {
                var v = gray[yy * width + xx];
                values[(yy - y0) * regionW + (xx - x0)] = v;
                sum += v;
            }
        var mean = sum / values.Length;
        var darkOnLight = IsDarkOnLight(values, regionW, regionH, mean);

        // Projeção vertical: colunas vazias separam os dígitos.
        var darkColumns = new bool[regionW];
        for (var c = 0; c < regionW; c++)
        {
            var any = false;
            for (var r = 0; r < regionH; r++)
                if (IsSegment(values[r * regionW + c], mean, darkOnLight)) { any = true; break; }
            darkColumns[c] = any;
        }

        var glyphsX = new List<(int start, int end)>();
        var start = -1;
        for (var c = 0; c < regionW; c++)
        {
            if (darkColumns[c]) { if (start < 0) start = c; }
            else if (start >= 0) { if (c - start >= 3) glyphsX.Add((start, c)); start = -1; }
        }
        if (start >= 0 && regionW - start >= 3) glyphsX.Add((start, regionW));

        var result = new StringBuilder(glyphsX.Count);
        foreach (var g in glyphsX)
            result.Append(DecodeCell(values, regionW, regionH, g.start, g.end, mean, darkOnLight));
        return result.ToString();
    }

    private static char DecodeCell(float[] values, int regionW, int regionH, int gx0, int gx1, float mean, bool darkOnLight)
    {
        // bbox real do glifo (remove linhas vazias no topo/base).
        var gy0 = 0;
        var gy1 = regionH;
        while (gy0 < gy1 && RowEmpty(values, regionW, gy0, gx0, gx1, mean, darkOnLight)) gy0++;
        while (gy1 > gy0 && RowEmpty(values, regionW, gy1 - 1, gx0, gx1, mean, darkOnLight)) gy1--;
        if (gy1 - gy0 < 4 || gx1 - gx0 < 3) return '?';

        // Dígitos como o '1' têm só barras verticais à direita: a projeção colapsa a célula e
        // os segmentos horizontais (a,d,g) "acendem" falsamente. Expande a célula p/ a direita
        // (aspecto típico 7-seg ~0.55 da altura) mantendo a borda direita do glifo no lugar.
        var glyphHeight = gy1 - gy0;
        if (gx1 - gx0 < glyphHeight * 0.45f)
        {
            var effWidth = (int)(glyphHeight * 0.55f);
            gx0 = Math.Max(0, gx1 - effWidth);
        }

        var dirs = new[]
        {
            (0.5f, 0.12f), (0.86f, 0.34f), (0.86f, 0.66f), (0.5f, 0.88f),
            (0.14f, 0.66f), (0.14f, 0.34f), (0.5f, 0.5f)
        };
        var lit = new bool[7];
        for (var s = 0; s < 7; s++)
        {
            var cx = (int)Math.Clamp(gx0 + dirs[s].Item1 * (gx1 - gx0), gx0, gx1 - 1);
            var cy = (int)Math.Clamp(gy0 + dirs[s].Item2 * (gy1 - gy0), gy0, gy1 - 1);
            lit[s] = SampleOn(values, regionW, regionH, cx, cy, mean, darkOnLight);
        }
        return Decode(lit[0], lit[1], lit[2], lit[3], lit[4], lit[5], lit[6]);
    }

    private static bool SampleOn(float[] values, int w, int h, int cx, int cy, float mean, bool darkOnLight)
    {
        var on = 0; var tot = 0;
        for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var x = cx + dx; var y = cy + dy;
                if (x < 0 || x >= w || y < 0 || y >= h) continue;
                if (IsSegment(values[y * w + x], mean, darkOnLight)) on++;
                tot++;
            }
        return tot > 0 && on > tot / 2;
    }

    private static bool RowEmpty(float[] values, int w, int r, int x0, int x1, float mean, bool darkOnLight)
    {
        for (var c = x0; c < x1; c++) if (IsSegment(values[r * w + c], mean, darkOnLight)) return false;
        return true;
    }

    private static bool IsDarkOnLight(float[] values, int w, int h, float mean)
    {
        // Usa os cantos da região (fundo, fora dos dígitos) para decidir a polaridade.
        var cw = Math.Max(1, w / 6); var ch = Math.Max(1, h / 6);
        float cs = 0f; var n = 0;
        for (var cy = 0; cy < ch; cy++)
            for (var cx = 0; cx < cw; cx++)
            {
                cs += values[cy * w + cx]; n++;
                cs += values[cy * w + (w - 1 - cx)]; n++;
                cs += values[(h - 1 - cy) * w + cx]; n++;
                cs += values[(h - 1 - cy) * w + (w - 1 - cx)]; n++;
            }
        if (n == 0) return true;
        var cornerMean = cs / n;
        // Se o fundo for mais claro que a média, os dígitos são escuros.
        return cornerMean > mean;
    }

    private static bool IsSegment(float v, float mean, bool darkOnLight)
        => darkOnLight ? v < mean : v > mean;
}
