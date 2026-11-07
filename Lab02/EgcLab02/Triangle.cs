using OpenTK.Graphics.OpenGL4;

namespace Egc;

/// <summary>
/// Un triunghi desenat în stilul OpenGL modern:
/// date în VBO, indici în EBO, descrierea formatului în VAO.
/// </summary>
public sealed class Triangle : IDisposable
{
    // Un vârf = 6 numere float: x, y, z (poziție) + r, g, b (culoare).
    private const int FloatsPerVertex = 6;

    /// <summary>
    /// Datele vârfurilor, în memoria RAM (CPU). Modificarea lor NU schimbă imaginea
    /// până când nu apelăm UpdateVertices(), care copiază tabloul în memoria GPU.
    /// </summary>
    public float[] Vertices { get; } = new float[]
    {
        //  poziție (x, y, z)      culoare (r, g, b)
        -0.5f, -0.5f, 0.0f,      1.0f, 0.0f, 0.0f,   // vârful 0: stânga-jos, roșu
         0.5f, -0.5f, 0.0f,      0.0f, 1.0f, 0.0f,   // vârful 1: dreapta-jos, verde
         0.0f,  0.5f, 0.0f,      0.0f, 0.0f, 1.0f    // vârful 2: sus, albastru
    };

    // Indicii: ordinea în care sunt luate vârfurile pentru a forma triunghiuri.
    private readonly uint[] _indices = new uint[] { 0, 1, 2 };

    private readonly int _vao;   // Vertex Array Object   – "cum se citesc datele"
    private readonly int _vbo;   // Vertex Buffer Object  – "datele vârfurilor"
    private readonly int _ebo;   // Element Buffer Object – "indicii"

    public Triangle()
    {
        // 1. Creăm obiectele (primim doar niște numere-identificatori).
        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        _ebo = GL.GenBuffer();

        // 2. Legăm VAO: de acum toată configurarea de mai jos este înregistrată în el.
        GL.BindVertexArray(_vao);

        // 3. VBO: copiem vârfurile în memoria GPU.
        //    DynamicDraw = "datele se vor modifica des" (ne trebuie la exercițiul de deformare).
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, Vertices.Length * sizeof(float),
                      Vertices, BufferUsageHint.DynamicDraw);

        // 4. EBO: copiem indicii. Legarea EBO este reținută de VAO-ul activ.
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, _indices.Length * sizeof(uint),
                      _indices, BufferUsageHint.StaticDraw);

        // 5. Descriem formatul datelor din VBO.
        int stride = FloatsPerVertex * sizeof(float);   // pasul: câți octeți are un vârf întreg

        // atributul 0 (layout location = 0 în vertex shader): poziția, 3 float, la offset 0
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(0);

        // atributul 1 (layout location = 1): culoarea, 3 float, după cei 3 float ai poziției
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        // 6. Dezlegăm VAO (NU dezlegăm EBO înainte de VAO: ar fi scos din VAO).
        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }

    /// <summary>Setează poziția vârfului <paramref name="vertex"/> (0, 1 sau 2) în tabloul din RAM.</summary>
    public void SetPosition(int vertex, float x, float y, float z = 0.0f)
    {
        int i = vertex * FloatsPerVertex;
        Vertices[i] = x;
        Vertices[i + 1] = y;
        Vertices[i + 2] = z;
    }

    /// <summary>Setează culoarea vârfului <paramref name="vertex"/> în tabloul din RAM.</summary>
    public void SetColor(int vertex, float r, float g, float b)
    {
        int i = vertex * FloatsPerVertex + 3;
        Vertices[i] = r;
        Vertices[i + 1] = g;
        Vertices[i + 2] = b;
    }

    /// <summary>Copiază din nou tabloul Vertices (RAM) în VBO (memoria GPU).</summary>
    public void UpdateVertices()
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero,
                         Vertices.Length * sizeof(float), Vertices);
    }

    /// <summary>Desenează triunghiul (programul shader trebuie activat înainte).</summary>
    public void Draw()
    {
        GL.BindVertexArray(_vao);
        GL.DrawElements(PrimitiveType.Triangles, _indices.Length, DrawElementsType.UnsignedInt, 0);
    }

    public void Dispose()
    {
        GL.DeleteBuffer(_vbo);
        GL.DeleteBuffer(_ebo);
        GL.DeleteVertexArray(_vao);
    }
}
