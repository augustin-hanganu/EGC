using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// Fereastra aplicației și bucla principală (main game loop).
/// Structura este cea din laboratorul 2; în plus avem mai multe forme (Mesh), timp cu pauză
/// și uniforme noi pentru vertex shader.
/// </summary>
public class Game : GameWindow
{
    // Formele disponibile; ordinea trebuie să coincidă cu ordinea din OnLoad și din ShapeNames.
    private enum ShapeKind { Triangle, Quad, Polygon, Circle, Ring, Grid }

    private static readonly string[] ShapeNames = { "triunghi", "dreptunghi", "poligon", "cerc", "inel", "grilă" };

    // Modurile de desenare parcurse cu tasta M pentru inel (secțiunea 4.5, facultativ).
    private static readonly PrimitiveType[] RingModes =
    {
        PrimitiveType.Points, PrimitiveType.Lines, PrimitiveType.LineStrip, PrimitiveType.LineLoop,
        PrimitiveType.Triangles, PrimitiveType.TriangleStrip, PrimitiveType.TriangleFan
    };

    // Obiecte care NU depind de OpenGL pot fi create imediat.
    private readonly Camera _camera = new();

    // Obiectele care folosesc OpenGL se creează în OnLoad (abia atunci există contextul OpenGL).
    private Shader _shader = null!;
    private Mesh[] _meshes = null!;   // câte un Mesh pentru fiecare ShapeKind

    // Starea aplicației (se modifică în OnUpdateFrame, se folosește în OnRenderFrame).
    private Color4 _clearColor = new(0.2f, 0.3f, 0.3f, 1.0f);   // culoarea de fundal
    private Color4 _tint = Color4.White;                          // nuanța formei (alb = neschimbat)
    private Vector3 _offset = Vector3.Zero;                       // deplasarea formei
    private bool _wireframe;                                      // contur (true) sau plin (false)

    // Starea nouă din laboratorul 3.
    private ShapeKind _shape = ShapeKind.Triangle;   // forma desenată acum
    private int _sides = 5;                          // numărul de laturi al poligonului
    private int _ringMode = 3;                       // indexul în RingModes (3 = LineLoop)
    private bool _paused = false;                    // pauză: timpul animației nu mai curge (se folosește la P5b)
    private bool _waveOn;                            // unda din vertex shader
    private int _colorMode;                          // 0 = culoarea din VBO, 1 = culoare din poziție
    private double _elapsed;                         // timpul animației (secunde)

    // Contoare afișate în titlul ferestrei.
    private readonly string _baseTitle;
    private double _secondTimer;
    private int _updates, _frames;                   // contoare în secunda curentă
    private int _lastUpdates, _lastFrames;           // valorile afișate
    private bool _titleDirty = true;                 // titlul trebuie rescris

    public Game(int width, int height, string title)
        : base(GameWindowSettings.Default,
               new NativeWindowSettings
               {
                   ClientSize = new Vector2i(width, height),
                   Title = title,
                   APIVersion = new Version(3, 3),          // cerem OpenGL 3.3 ...
                   Profile = ContextProfile.Core,           // ... profil Core (fără modul imediat)
                   Flags = ContextFlags.ForwardCompatible
               })
    {
        _baseTitle = title;
        VSync = VSyncMode.On;   // swap-ul bufferelor așteaptă reîmprospătarea monitorului
    }

    /// <summary>Apelată O SINGURĂ DATĂ, după crearea ferestrei și a contextului OpenGL.</summary>
    protected override void OnLoad()
    {
        base.OnLoad();

        Console.WriteLine($"OpenGL  : {GL.GetString(StringName.Version)}");
        Console.WriteLine($"Vendor  : {GL.GetString(StringName.Vendor)}");
        Console.WriteLine($"Renderer: {GL.GetString(StringName.Renderer)}");

        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        GL.PointSize(8.0f);   // dimensiunea punctelor (PrimitiveType.Points și PolygonMode.Point)

        _shader = Shader.FromFiles("Shaders/shader.vert", "Shaders/shader.frag");

        // Ordinea trebuie să fie cea din enum-ul ShapeKind.
        _meshes = new[]
        {
            Shapes.Triangle(),
            Shapes.Quad(),
            Shapes.RegularPolygon(_sides, 0.5f),
            Shapes.RegularPolygon(64, 0.5f),      // "cercul": un poligon cu foarte multe laturi
            Shapes.Ring(6, 0.6f),
            Shapes.Grid(16, 8, 1.6f, 0.8f)
        };
        _meshes[(int)ShapeKind.Ring].Primitive = RingModes[_ringMode];
    }

    /// <summary>Apelată când se schimbă dimensiunea ferestrei.</summary>
    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        GL.Viewport(0, 0, e.Width, e.Height);   // zona ferestrei în care se desenează
        _camera.Resize(e.Width, e.Height);
    }

    /// <summary>Apelată la fiecare iterație a buclei: LOGICA aplicației (intrări, mișcare, timp).</summary>
    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        float dt = (float)args.Time;   // secundele scurse de la apelul anterior
        _updates++;

        if (KeyboardState.IsKeyDown(Keys.Escape))
        {
            Close();
        }

        // --- Din laboratorul 2 (rezolvat; comparați cu soluția voastră) ---------------

        // P2b: nuanța formei (0 = alb, 1 = roșu, 2 = verde, 3 = albastru)
        if (KeyboardState.IsKeyPressed(Keys.D0)) _tint = Color4.White;
        if (KeyboardState.IsKeyPressed(Keys.D1)) _tint = new Color4(1.0f, 0.3f, 0.3f, 1.0f);
        if (KeyboardState.IsKeyPressed(Keys.D2)) _tint = new Color4(0.3f, 1.0f, 0.3f, 1.0f);
        if (KeyboardState.IsKeyPressed(Keys.D3)) _tint = new Color4(0.3f, 0.3f, 1.0f, 1.0f);

        // P3b: tasta F comută între contur și plin (IsKeyPressed: o singură dată pe apăsare)
        if (KeyboardState.IsKeyPressed(Keys.F)) _wireframe = !_wireframe;

        // P4c: deplasare cu săgețile, viteză constantă (1 unitate NDC pe secundă) înmulțită cu Δt
        const float speed = 1.0f;
        if (KeyboardState.IsKeyDown(Keys.Left))  _offset.X -= speed * dt;
        if (KeyboardState.IsKeyDown(Keys.Right)) _offset.X += speed * dt;
        if (KeyboardState.IsKeyDown(Keys.Down))  _offset.Y -= speed * dt;
        if (KeyboardState.IsKeyDown(Keys.Up))    _offset.Y += speed * dt;

        // --- Laborator 3: alegerea formei ---------------------------------------------

        // Tab: următoarea formă (triunghi -> dreptunghi -> poligon -> cerc -> inel -> grilă -> triunghi ...)
        if (KeyboardState.IsKeyPressed(Keys.Tab))
        {
            _shape = (ShapeKind)(((int)_shape + 1) % _meshes.Length);
            _titleDirty = true;
        }

        // + / - : numărul de laturi al poligonului (tastele de pe rândul de sus sau din blocul numeric)
        if (KeyboardState.IsKeyPressed(Keys.Equal) || KeyboardState.IsKeyPressed(Keys.KeyPadAdd)) ChangeSides(+1);
        if (KeyboardState.IsKeyPressed(Keys.Minus) || KeyboardState.IsKeyPressed(Keys.KeyPadSubtract)) ChangeSides(-1);

        // M: modul de desenare al inelului (facultativ, secțiunea 4.5)
        if (KeyboardState.IsKeyPressed(Keys.M))
        {
            _ringMode = (_ringMode + 1) % RingModes.Length;
            _meshes[(int)ShapeKind.Ring].Primitive = RingModes[_ringMode];
            _titleDirty = true;
        }

        // --- Laborator 3: vertex shader --------------------------------------------

        // W: unda în vertex shader pornită / oprită.  C: culoarea din VBO / calculată din poziție.
        if (KeyboardState.IsKeyPressed(Keys.W)) _waveOn = !_waveOn;
        if (KeyboardState.IsKeyPressed(Keys.C)) _colorMode = 1 - _colorMode;

        // --- Laborator 3: timpul animației ---------------------------------------------

        // P5b: tasta P comuta pauza; timpul animatiei creste doar cand NU suntem in pauza
        if (KeyboardState.IsKeyPressed(Keys.P))
        {
            _paused = !_paused;
            _titleDirty = true;
        }
        if (!_paused)
        {
            _elapsed += args.Time;
        }

        // P4a: fundalul parcurge toate nuantele in 20 s (0,05 nuante/s); FromHsv aduce h in [0, 1)
        _clearColor = ColorUtil.FromHsv((float)(_elapsed * 0.05), 0.5f, 0.35f);

        // --- Titlul ferestrei: forma curentă + câte actualizări / cadre au avut loc în ultima secundă ---
        _secondTimer += args.Time;
        if (_secondTimer >= 1.0)
        {
            _lastUpdates = _updates;
            _lastFrames = _frames;
            _updates = 0;
            _frames = 0;
            _secondTimer -= 1.0;
            _titleDirty = true;
        }

        if (_titleDirty)
        {
            RefreshTitle();
            _titleDirty = false;
        }
    }

    /// <summary>Apelată la fiecare iterație a buclei: DESENAREA unui cadru.</summary>
    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        _frames++;

        // 1. Ștergem imaginea veche din back buffer.
        GL.ClearColor(_clearColor.R, _clearColor.G, _clearColor.B, _clearColor.A);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        // 2. Starea de rasterizare (contur / plin) și programul shader cu uniformele lui.
        SetWireframe(_wireframe);

        _shader.Use();
        _shader.SetMatrix4("uView", _camera.ViewMatrix);
        _shader.SetMatrix4("uProjection", _camera.ProjectionMatrix);
        _shader.SetVector3("uOffset", _offset);
        _shader.SetColor("uTint", _tint);

        // Uniformele noi din laboratorul 3 (le citește vertex shaderul)
        _shader.SetFloat("uTime", (float)_elapsed);
        _shader.SetFloat("uWaveAmplitude", _waveOn ? 0.08f : 0.0f);
        _shader.SetFloat("uWaveFrequency", 6.0f);
        _shader.SetInt("uColorMode", _colorMode);

        // 3. Desenăm forma aleasă.
        _meshes[(int)_shape].Draw();

        // 4. Afișăm cadrul terminat: back buffer <-> front buffer.
        SwapBuffers();
    }

    /// <summary>Apelată O SINGURĂ DATĂ, la închiderea ferestrei: eliberăm resursele GPU.</summary>
    protected override void OnUnload()
    {
        foreach (Mesh mesh in _meshes)
        {
            mesh.Dispose();
        }
        _shader.Dispose();

        base.OnUnload();
    }

    /// <summary>
    /// Contur / plin. Dacă Visual Studio semnalează o eroare la primul parametru,
    /// folosiți aici tipul acceptat de versiunea voastră (vezi laboratorul 2, Problema 3):
    /// TriangleFace.FrontAndBack sau MaterialFace.FrontAndBack. Este singurul loc din proiect care îl folosește.
    /// </summary>
    private static void SetWireframe(bool wireframe)
    {
        GL.PolygonMode(TriangleFace.FrontAndBack, wireframe ? PolygonMode.Line : PolygonMode.Fill);
    }

    /// <summary>Schimbă numărul de laturi al poligonului (3..64) și reconstruiește forma.</summary>
    private void ChangeSides(int delta)
    {
        int sides = Math.Clamp(_sides + delta, 3, 64);
        if (sides == _sides)
        {
            return;
        }

        _sides = sides;

        // O formă nouă = buffere noi în GPU. Eliberăm mai întâi pe cele vechi.
        int index = (int)ShapeKind.Polygon;
        _meshes[index].Dispose();
        _meshes[index] = Shapes.RegularPolygon(_sides, 0.5f);
        _titleDirty = true;
    }

    private void RefreshTitle()
    {
        string name = ShapeNames[(int)_shape];
        string detail = _shape switch
        {
            ShapeKind.Polygon => $" (n = {_sides})",
            ShapeKind.Ring => $" ({RingModes[_ringMode]})",
            _ => ""
        };
        string pause = _paused ? " | PAUZĂ" : "";

        Title = $"{_baseTitle} | {name}{detail} | update/s: {_lastUpdates} | render/s: {_lastFrames}{pause}";
    }
}
