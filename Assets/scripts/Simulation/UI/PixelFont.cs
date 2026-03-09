using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Minimal 5x7 bitmap font for rendering text directly into Texture2D pixel arrays.
/// Used by thermal graph components to draw axis labels and tick values.
/// </summary>
public static class PixelFont
{
    public const int CharWidth = 5;
    public const int CharHeight = 7;
    public const int CharSpacing = 1;

    // Each glyph is 7 rows (top to bottom), each row is 5 bits (bit4=left, bit0=right)
    private static readonly Dictionary<char, byte[]> Glyphs = new Dictionary<char, byte[]>
    {
        { '0', new byte[] { 0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E } },
        { '1', new byte[] { 0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E } },
        { '2', new byte[] { 0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F } },
        { '3', new byte[] { 0x0E, 0x11, 0x01, 0x06, 0x01, 0x11, 0x0E } },
        { '4', new byte[] { 0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02 } },
        { '5', new byte[] { 0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E } },
        { '6', new byte[] { 0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E } },
        { '7', new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08 } },
        { '8', new byte[] { 0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E } },
        { '9', new byte[] { 0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C } },
        { '-', new byte[] { 0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00 } },
        { '.', new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x06, 0x06 } },
        { ' ', new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 } },
        { '(', new byte[] { 0x02, 0x04, 0x08, 0x08, 0x08, 0x04, 0x02 } },
        { ')', new byte[] { 0x08, 0x04, 0x02, 0x02, 0x02, 0x04, 0x08 } },
        // Letters for axis labels
        { 'T', new byte[] { 0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 } },
        { 'e', new byte[] { 0x00, 0x00, 0x0E, 0x11, 0x1F, 0x10, 0x0E } },
        { 'm', new byte[] { 0x00, 0x00, 0x1A, 0x15, 0x15, 0x11, 0x11 } },
        { 'p', new byte[] { 0x00, 0x00, 0x1E, 0x11, 0x1E, 0x10, 0x10 } },
        { 'r', new byte[] { 0x00, 0x00, 0x16, 0x19, 0x10, 0x10, 0x10 } },
        { 'f', new byte[] { 0x06, 0x08, 0x1E, 0x08, 0x08, 0x08, 0x08 } },
        { 'C', new byte[] { 0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E } },
        { 'P', new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10 } },
        { 'a', new byte[] { 0x00, 0x00, 0x0E, 0x01, 0x0F, 0x11, 0x0F } },
        { 'n', new byte[] { 0x00, 0x00, 0x16, 0x19, 0x11, 0x11, 0x11 } },
        { 'c', new byte[] { 0x00, 0x00, 0x0E, 0x10, 0x10, 0x11, 0x0E } },
        { 'o', new byte[] { 0x00, 0x00, 0x0E, 0x11, 0x11, 0x11, 0x0E } },
        { 'u', new byte[] { 0x00, 0x00, 0x11, 0x11, 0x11, 0x13, 0x0D } },
        { 'i', new byte[] { 0x04, 0x00, 0x0C, 0x04, 0x04, 0x04, 0x0E } },
        { 't', new byte[] { 0x08, 0x08, 0x1E, 0x08, 0x08, 0x09, 0x06 } },
        { 'l', new byte[] { 0x0C, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E } },
    };

    public static int MeasureString(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return text.Length * (CharWidth + CharSpacing) - CharSpacing;
    }

    public static void DrawChar(Color[] pixels, int texWidth, int texHeight,
                                char c, int x, int y, Color color)
    {
        if (!Glyphs.TryGetValue(c, out byte[] glyph)) return;

        for (int row = 0; row < CharHeight; row++)
        {
            byte rowBits = glyph[row];
            int py = y + (CharHeight - 1 - row); // top row of glyph drawn at top
            if (py < 0 || py >= texHeight) continue;

            for (int col = 0; col < CharWidth; col++)
            {
                int px = x + col;
                if (px < 0 || px >= texWidth) continue;

                // bit4 = leftmost pixel
                if ((rowBits & (1 << (CharWidth - 1 - col))) != 0)
                {
                    int idx = py * texWidth + px;
                    pixels[idx] = color;
                }
            }
        }
    }

    public static void DrawString(Color[] pixels, int texWidth, int texHeight,
                                  string text, int x, int y, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;

        int cx = x;
        foreach (char c in text)
        {
            DrawChar(pixels, texWidth, texHeight, c, cx, y, color);
            cx += CharWidth + CharSpacing;
        }
    }

    public static void DrawStringCentered(Color[] pixels, int texWidth, int texHeight,
                                          string text, int centerX, int y, Color color)
    {
        int w = MeasureString(text);
        DrawString(pixels, texWidth, texHeight, text, centerX - w / 2, y, color);
    }

    /// <summary>
    /// Draw text rotated 90 degrees CCW, centered vertically at centerY.
    /// Text reads bottom-to-top.
    /// </summary>
    public static void DrawStringVertical(Color[] pixels, int texWidth, int texHeight,
                                          string text, int x, int centerY, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;

        int totalHeight = text.Length * (CharWidth + CharSpacing) - CharSpacing;
        int startY = centerY - totalHeight / 2;

        for (int ci = 0; ci < text.Length; ci++)
        {
            char c = text[ci];
            if (!Glyphs.TryGetValue(c, out byte[] glyph)) continue;

            int charBaseY = startY + ci * (CharWidth + CharSpacing);

            // Rotate each glyph 90 CCW: glyph row -> texture column (going left),
            // glyph column -> texture row (going up)
            for (int row = 0; row < CharHeight; row++)
            {
                byte rowBits = glyph[row];
                int px = x + (CharHeight - 1 - row); // glyph rows map to x (right to left)

                if (px < 0 || px >= texWidth) continue;

                for (int col = 0; col < CharWidth; col++)
                {
                    int py = charBaseY + (CharWidth - 1 - col); // glyph columns map to y
                    if (py < 0 || py >= texHeight) continue;

                    if ((rowBits & (1 << (CharWidth - 1 - col))) != 0)
                    {
                        int idx = py * texWidth + px;
                        pixels[idx] = color;
                    }
                }
            }
        }
    }

    public static void DrawStringRightAligned(Color[] pixels, int texWidth, int texHeight,
                                              string text, int rightX, int y, Color color)
    {
        int w = MeasureString(text);
        DrawString(pixels, texWidth, texHeight, text, rightX - w, y, color);
    }
}
