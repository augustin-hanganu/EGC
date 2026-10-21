using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Egc;

/// <summary>
/// Fabrica de forme: fiecare metodă construiește un obiect Mesh (vârfuri + indici).
/// Un vârf = 6 float: x, y, z, r, g, b (aceeași structură ca în laboratorul 2).
/// Formele sunt plate (z = 0) și sunt scrise direct în coordonate NDC.
/// </summary>
public static class Shapes
{
    /// <summary>Triunghiul din laboratorul 2 (dat).</summary>
    public static Mesh Triangle()
    {
        float[] vertices =
        {
            //  x      y     z       r     g     b
            -0.5f, -0.5f, 0.0f,   1.0f, 0.0f, 0.0f,   // 0: stânga-jos, roșu
             0.5f, -0.5f, 0.0f,   0.0f, 1.0f, 0.0f,   // 1: dreapta-jos, verde
             0.0f,  0.5f, 0.0f,   0.0f, 0.0f, 1.0f    // 2: sus, albastru
        };
        uint[] indices = { 0, 1, 2 };
        return new Mesh(vertices, indices);
    }

    /// <summary>
    /// Dreptunghiul din 2 triunghiuri: 4 vârfuri și 6 indici.
    /// Problema 1: completați vârfurile și indicii (tabelul din secțiunea 4.2).
    /// Până atunci tablourile conțin doar zerouri și nu se vede nimic.
    /// </summary>
    public static Mesh Quad()
    {
        var vertices = new float[4 * Mesh.FloatsPerVertex];
        var indices = new uint[6];

        // P1a: cele 4 vârfuri (poziție + culoare)
        SetVertex(vertices, 0, -0.5f, -0.5f, 0.0f,   1.0f, 0.0f, 0.0f);   // vârful 0: stânga-jos, roșu
        SetVertex(vertices, 1,  0.5f, -0.5f, 0.0f,   0.0f, 1.0f, 0.0f);   // vârful 1: dreapta-jos, verde
        SetVertex(vertices, 2,  0.5f,  0.5f, 0.0f,   0.0f, 0.0f, 1.0f);   // vârful 2: dreapta-sus, albastru
        SetVertex(vertices, 3, -0.5f,  0.5f, 0.0f,   1.0f, 1.0f, 0.0f);   // vârful 3: stânga-sus, galben

        // P1b: 6 indici = 2 triunghiuri, ambele în sens trigonometric
        indices[0] = 0; indices[1] = 1; indices[2] = 2;   // primul triunghi
        indices[3] = 2; indices[4] = 3; indices[5] = 0;   // al doilea triunghi

        return new Mesh(vertices, indices);
    }

    /// <summary>
    /// Poligon regulat cu <paramref name="sides"/> laturi, centrat în origine.
    /// Vârful 0 = centrul; vârfurile 1..n = cele n colțuri, pe un cerc de rază <paramref name="radius"/>.
    /// Problema 2: completați funcția (vezi secțiunea 4.3). Până atunci nu se vede nimic.
    /// </summary>
    public static Mesh RegularPolygon(int sides, float radius)
    {
        // n laturi => n + 1 vârfuri (centrul și cele n colțuri) și n triunghiuri => 3 * n indici
        var vertices = new float[(sides + 1) * Mesh.FloatsPerVertex];
        var indices = new uint[sides * 3];

        // P2a: vârful 0 este centrul (0, 0, 0), de culoare albă
        SetVertex(vertices, 0, 0.0f, 0.0f, 0.0f, Color4.White);

        for (int i = 0; i < sides; i++)
        {
            // Unghiul colțului i: începem de sus (90 de grade) și mergem în sens trigonometric.
            float angle = MathF.PI / 2.0f + 2.0f * MathF.PI * i / sides;

            // P2b: coordonatele colțului
            float x = radius * MathF.Cos(angle);
            float y = radius * MathF.Sin(angle);

            // P2c: culoarea colțului din cercul culorilor ((float)i / sides, nu i / sides!)
            Color4 color = ColorUtil.FromHsv((float)i / sides, 1.0f, 1.0f);

            // P2d: colțul i se memorează în vârful 1 + i
            SetVertex(vertices, 1 + i, x, y, 0.0f, color);

            // P2e: triunghiul i = centrul, colțul i, colțul următor (% sides închide ultimul triunghi)
            indices[3 * i]     = 0;
            indices[3 * i + 1] = (uint)(1 + i);
            indices[3 * i + 2] = (uint)(1 + (i + 1) % sides);
        }

        return new Mesh(vertices, indices);
    }

    /// <summary>
    /// Inelul: <paramref name="count"/> puncte pe un cerc, fără centru. Indicii sunt 0, 1, ..., count - 1.
    /// Folosit la secțiunea 4.5 (facultativ) pentru a compara tipurile de primitive. Dat.
    /// </summary>
    public static Mesh Ring(int count, float radius)
    {
        var vertices = new float[count * Mesh.FloatsPerVertex];
        var indices = new uint[count];

        for (int i = 0; i < count; i++)
        {
            float angle = MathF.PI / 2.0f + 2.0f * MathF.PI * i / count;
            SetVertex(vertices, i, radius * MathF.Cos(angle), radius * MathF.Sin(angle), 0.0f,
                      ColorUtil.FromHsv((float)i / count, 1.0f, 1.0f));
            indices[i] = (uint)i;
        }

        return new Mesh(vertices, indices, PrimitiveType.LineLoop);
    }

    /// <summary>
    /// Grila: un dreptunghi împărțit în <paramref name="columns"/> x <paramref name="rows"/> celule,
    /// fiecare celulă din 2 triunghiuri. Are multe vârfuri, deci o deformare în vertex shader se vede bine
    /// (Problema 8, facultativă). Dat.
    /// </summary>
    public static Mesh Grid(int columns, int rows, float width, float height)
    {
        int perRow = columns + 1;
        var vertices = new float[perRow * (rows + 1) * Mesh.FloatsPerVertex];
        var indices = new uint[columns * rows * 6];

        for (int row = 0; row <= rows; row++)
        {
            for (int col = 0; col <= columns; col++)
            {
                float u = col / (float)columns;   // 0..1 de la stânga la dreapta
                float v = row / (float)rows;      // 0..1 de jos în sus
                SetVertex(vertices, row * perRow + col,
                          (u - 0.5f) * width, (v - 0.5f) * height, 0.0f,
                          ColorUtil.FromHsv(u * 0.8f, 1.0f, 1.0f));
            }
        }

        int k = 0;
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                uint i0 = (uint)(row * perRow + col);   // stânga-jos
                uint i1 = i0 + 1;                       // dreapta-jos
                uint i2 = i1 + (uint)perRow;            // dreapta-sus
                uint i3 = i0 + (uint)perRow;            // stânga-sus
                indices[k++] = i0; indices[k++] = i1; indices[k++] = i2;
                indices[k++] = i2; indices[k++] = i3; indices[k++] = i0;
            }
        }

        return new Mesh(vertices, indices);
    }

    // --- Funcții ajutătoare ----------------------------------------------

    /// <summary>Scrie un vârf (poziție + culoare) în tabloul <paramref name="vertices"/>, pe poziția <paramref name="index"/>.</summary>
    private static void SetVertex(float[] vertices, int index, float x, float y, float z, float r, float g, float b)
    {
        int i = index * Mesh.FloatsPerVertex;
        vertices[i] = x;
        vertices[i + 1] = y;
        vertices[i + 2] = z;
        vertices[i + 3] = r;
        vertices[i + 4] = g;
        vertices[i + 5] = b;
    }

    /// <summary>Aceeași funcție, cu culoarea dată ca Color4.</summary>
    private static void SetVertex(float[] vertices, int index, float x, float y, float z, Color4 color)
    {
        SetVertex(vertices, index, x, y, z, color.R, color.G, color.B);
    }
}
