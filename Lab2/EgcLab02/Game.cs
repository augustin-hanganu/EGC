using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// Fereastra aplicației și bucla principală (main game loop).
/// GameWindow apelează automat metodele On... de mai jos, în ordinea descrisă în laborator.
/// </summary>
public class Game : GameWindow
{
    // Obiecte care NU depind de OpenGL pot fi create imediat.
    private readonly Camera _camera = new();

    // Obiectele care folosesc OpenGL se creează în OnLoad (abia atunci există contextul OpenGL).
    private Shader _shader = null!;
    private Triangle _triangle = null!;

    // Starea aplicației (se modifică în OnUpdateFrame, se folosește în OnRenderFrame).
    private Color4 _clearColor = new(0.05f, 0.08f, 0.25f, 1.0f); // culoarea de fundal (P1a: albastru inchis)
    private Color4 _tint = Color4.White;                          // nuanța triunghiului (alb = neschimbat)
    private Vector3 _offset = Vector3.Zero;                       // deplasarea triunghiului

    // Contoare pentru exemplul cu bucla principală (afișate în titlul ferestrei).
    private readonly string _baseTitle;
    private double _elapsed;     // timpul total scurs (secunde)
    private double _secondTimer; // cronometru pentru actualizarea titlului o dată pe secundă
    private int _updates;        // câte OnUpdateFrame în ultima secundă
    private int _frames;         // câte OnRenderFrame în ultima secundă

    // --- Starea adăugată pentru problemele 1-5 ---
    private bool _animBackground;                  // P1: fundal animat (tasta B)
    private bool _wireframe;                       // P3: contur / plin (tasta F)
    private bool _autoMove;                        // P4b: mișcare sinusoidală (tasta M)
    private bool _deform;                          // P5: deformare vârfuri (tasta D)
    private Vector3 _manual = Vector3.Zero;        // P4c: deplasare cu săgețile
    private readonly Random _rng = new();
    private const float Speed = 1.0f;              // P4c: viteza, în unități NDC pe secundă

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

        // Informații despre driverul și placa grafică folosite (vezi Problema 1 din Laboratorul 01).
        Console.WriteLine($"OpenGL  : {GL.GetString(StringName.Version)}");
        Console.WriteLine($"Vendor  : {GL.GetString(StringName.Vendor)}");
        Console.WriteLine($"Renderer: {GL.GetString(StringName.Renderer)}");

        Console.WriteLine("Taste: B fundal animat | 0,1,2,3 nuanta | C culoare aleatoare varf 0 | F contur/plin");
        Console.WriteLine("       sageti deplasare | M miscare automata | R reset pozitie | D deformare | V VSync | Esc iesire");

        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);

        _shader = Shader.FromFiles("Shaders/shader.vert", "Shaders/shader.frag");
        _triangle = new Triangle();
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

        // args.Time = secundele scurse de la apelul anterior (delta time).
        _elapsed += args.Time;
        _updates++;

        if (KeyboardState.IsKeyDown(Keys.Escape))
        {
            Close();
        }

        float dt = (float)args.Time;   // delta time (secunde)
        float t = (float)_elapsed;     // timpul total (secunde)

        // P1: fundal animat; R, G, B oscileaza cu defazaje 0, 2*pi/3, 4*pi/3
        if (KeyboardState.IsKeyPressed(Keys.B)) _animBackground = !_animBackground;
        if (_animBackground)
        {
            _clearColor = new Color4(
                0.5f + 0.5f * MathF.Sin(t),
                0.5f + 0.5f * MathF.Sin(t + 2.094f),
                0.5f + 0.5f * MathF.Sin(t + 4.189f),
                1.0f);
        }

        // P2: nuanta triunghiului (uniforma uTint)
        if (KeyboardState.IsKeyPressed(Keys.D0)) _tint = Color4.White;
        if (KeyboardState.IsKeyPressed(Keys.D1)) _tint = new Color4(1.0f, 0.0f, 0.0f, 1.0f);
        if (KeyboardState.IsKeyPressed(Keys.D2)) _tint = new Color4(0.0f, 1.0f, 0.0f, 1.0f);
        if (KeyboardState.IsKeyPressed(Keys.D3)) _tint = new Color4(0.0f, 0.0f, 1.0f, 1.0f);

        // P2e: culoare aleatoare pentru varful 0 (RAM -> VBO)
        if (KeyboardState.IsKeyPressed(Keys.C))
        {
            _triangle.SetColor(0, (float)_rng.NextDouble(), (float)_rng.NextDouble(), (float)_rng.NextDouble());
            _triangle.UpdateVertices();
        }

        // P3b: contur / plin; IsKeyPressed = o singura data pe apasare (IsKeyDown ar comuta la fiecare cadru)
        if (KeyboardState.IsKeyPressed(Keys.F)) _wireframe = !_wireframe;

        // P4: deplasare = viteza * dt (independenta de FPS)
        if (KeyboardState.IsKeyDown(Keys.Left))  _manual.X -= Speed * dt;
        if (KeyboardState.IsKeyDown(Keys.Right)) _manual.X += Speed * dt;
        if (KeyboardState.IsKeyDown(Keys.Down))  _manual.Y -= Speed * dt;
        if (KeyboardState.IsKeyDown(Keys.Up))    _manual.Y += Speed * dt;
        if (KeyboardState.IsKeyPressed(Keys.R))  _manual = Vector3.Zero;
        if (KeyboardState.IsKeyPressed(Keys.M))  _autoMove = !_autoMove;

        _offset = _manual;
        if (_autoMove) _offset.X += 0.5f * MathF.Sin(t);   // P4b

        // P5: deformarea varfurilor (date noi in VBO la fiecare cadru)
        if (KeyboardState.IsKeyPressed(Keys.D))
        {
            _deform = !_deform;
            if (!_deform)
            {
                // revenim la triunghiul initial
                _triangle.SetPosition(0, -0.5f, -0.5f);
                _triangle.SetPosition(1,  0.5f, -0.5f);
                _triangle.SetPosition(2,  0.0f,  0.5f);
                _triangle.UpdateVertices();
            }
        }
        if (_deform)
        {
            _triangle.SetPosition(0, -0.5f + 0.2f * MathF.Sin(t), -0.5f);           // stanga-dreapta
            _triangle.SetPosition(1,  0.5f, -0.5f + 0.2f * MathF.Sin(1.5f * t));    // sus-jos
            _triangle.SetPosition(2,  0.0f,  0.5f + 0.3f * MathF.Sin(2.3f * t));    // sus-jos, alta frecventa
            _triangle.UpdateVertices();
        }

        // VSync pornit / oprit (sectiunea 5.5)
        if (KeyboardState.IsKeyPressed(Keys.V))
            VSync = VSync == VSyncMode.On ? VSyncMode.Off : VSyncMode.On;


        // Exemplul bucla principală: o dată pe secundă afișăm în titlu câte
        // actualizări și câte cadre desenate au avut loc.
        _secondTimer += args.Time;
        if (_secondTimer >= 1.0)
        {
            Title = $"{_baseTitle} | update/s: {_updates} | render/s: {_frames}";
            _updates = 0;
            _frames = 0;
            _secondTimer -= 1.0;
        }
    }

    /// <summary>Apelată la fiecare iterație a buclei: DESENAREA unui cadru.</summary>
    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        _frames++;

        // 1. Ștergem imaginea veche din back buffer. GL.ClearColor SETEAZĂ starea (culoarea de ștergere),
        //    GL.Clear o FOLOSEȘTE. Aici o setăm la fiecare cadru pentru că vom putea s-o schimbăm (P1).
        GL.ClearColor(_clearColor.R, _clearColor.G, _clearColor.B, _clearColor.A);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        // 2. Activăm programul shader și îi trimitem uniformele.
        _shader.Use();
        _shader.SetMatrix4("uView", _camera.ViewMatrix);
        _shader.SetMatrix4("uProjection", _camera.ProjectionMatrix);
        _shader.SetVector3("uOffset", _offset);
        _shader.SetColor("uTint", _tint);

        // 3. Desenăm triunghiul.
        GL.PolygonMode(TriangleFace.FrontAndBack, _wireframe ? PolygonMode.Line : PolygonMode.Fill);
        _triangle.Draw();

        // 4. Afișăm cadrul terminat: back buffer <-> front buffer.
        SwapBuffers();
    }

    /// <summary>Apelată O SINGURĂ DATĂ, la închiderea ferestrei: eliberăm resursele GPU.</summary>
    protected override void OnUnload()
    {
        _triangle.Dispose();
        _shader.Dispose();

        base.OnUnload();
    }
}
