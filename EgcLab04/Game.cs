using System.Text;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// Fereastra aplicației și bucla principală (main game loop).
/// Aici se adună tot: scena cu patru obiecte, camera, tastatura, mouse-ul și meniul din consolă.
/// Rezolvă temele T1-T5 din laboratorul 04 (T1 stă în Transform2D.cs, restul sunt aici).
/// </summary>
public class Game : GameWindow
{
    // Camera nu depinde de OpenGL, deci o pot crea imediat.
    private readonly Camera _camera = new();

    // Tot ce folosește OpenGL (shader, mesh-uri) se creează în OnLoad, pentru că abia atunci
    // există contextul OpenGL.
    private Shader _shader = null!;
    private readonly List<SceneObject> _scene = new();
    private readonly List<Transform2D> _initialTransforms = new();   // pentru tasta R

    // Starea aplicației (se modifică în OnUpdateFrame, se citește în OnRenderFrame).
    private int _selected;            // indexul obiectului selectat în _scene (tasta Tab)
    private bool _wireframe;          // contur (true) sau plin (false)

    // --- T4: pivotul ----------------------------------------------------------------
    // Am pus un bool separat în loc de un Vector2? (nullable) ca să fie evident în cod
    // când există un pivot ales și când nu.
    private bool _hasPivot;
    private Vector2 _pivot;           // poziția pivotului, în unități de lume
    private float _pivotDepth;        // adâncimea la care l-am ales (ca marcajul să apară sub cursor și în perspectivă)
    private SceneObject _pivotMarker = null!;   // micul pătrat galben desenat în pivot

    // --- T5: satelitul --------------------------------------------------------------
    // Satelitul NU face parte din _scene, intenționat: altfel Tab l-ar putea selecta, iar
    // tasta R l-ar căuta în lista de transformări inițiale. Îl țin separat și îl desenez la final.
    private SceneObject _satellite = null!;
    private float _orbitAngle;                      // unghiul orbitei, în grade
    private const float OrbitDegPerSec = 360.0f / 4.0f;   // o rotație completă la 4 secunde
    private const float OrbitRadius = 1.2f;         // distanța față de obiectul selectat
    private const float SatelliteScale = 0.3f;

    // Contoare afișate în titlul ferestrei.
    private readonly string _baseTitle;
    private double _secondTimer;
    private int _frames, _lastFrames;
    private bool _titleDirty = true;

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

        // Consola Windows nu afișează implicit diacriticele din meniu, așa că forțez UTF-8.
        // Dacă tot îți apar semne ciudate, setează fontul consolei pe unul care are diacritice
        // (de exemplu Consolas) sau rulează din Windows Terminal.
        Console.OutputEncoding = Encoding.UTF8;

        Console.WriteLine($"OpenGL  : {GL.GetString(StringName.Version)}");
        Console.WriteLine($"Renderer: {GL.GetString(StringName.Renderer)}");

        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        _camera.Resize(ClientSize.X, ClientSize.Y);

        _shader = Shader.FromFiles("Shaders/shader.vert", "Shaders/shader.frag");

        // Scena: cele patru forme din laboratorul 3, plasate în lume prin Transform2D.
        // Depth (z) contează doar în perspectivă (tasta O): pătratul e mai departe, cercul mai aproape.
        AddObject("pătrat", Shapes.Quad(),
                  new Transform2D { Position = new Vector2(-1.8f, -1.0f), Depth = -3.0f });
        AddObject("triunghi", Shapes.Triangle(),
                  new Transform2D { Position = new Vector2(-1.2f, 1.0f) });
        AddObject("hexagon", Shapes.RegularPolygon(6, 0.5f),
                  new Transform2D { Position = new Vector2(2.0f, 1.0f) });
        AddObject("cerc", Shapes.RegularPolygon(64, 0.5f),
                  new Transform2D { Position = new Vector2(0.8f, -0.6f), Depth = 2.0f });

        // T4: marcajul pivotului. Un pătrat mic (scara 0,15) galben, desenat doar când există pivot.
        _pivotMarker = new SceneObject("pivot", Shapes.SolidSquare(),
                                       new Transform2D { Scale = new Vector2(0.15f, 0.15f) })
        {
            Tint = new Color4(1.0f, 0.9f, 0.1f, 1.0f)
        };

        // T5: satelitul este un triunghi obișnuit. Transformarea lui nu stă în Transform2D,
        // pentru că o calculez în OnRenderFrame din orbită + matricea obiectului selectat.
        _satellite = new SceneObject("satelit", Shapes.Triangle(), new Transform2D());

        PrintMenu();
    }

    /// <summary>Adaugă un obiect în scenă și reține transformarea lui inițială (pentru tasta R).</summary>
    private void AddObject(string name, Mesh mesh, Transform2D transform)
    {
        _scene.Add(new SceneObject(name, mesh, transform));
        _initialTransforms.Add(transform.Clone());
    }

    /// <summary>Apelată când se schimbă dimensiunea ferestrei.</summary>
    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        GL.Viewport(0, 0, e.Width, e.Height);   // zona ferestrei în care se desenează
        _camera.Resize(e.Width, e.Height);      // proiecția se adaptează la noul raport de aspect
    }

    /// <summary>Apelată la fiecare iterație a buclei: LOGICA aplicației (intrări, mișcare, timp).</summary>
    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        if (KeyboardState.IsKeyDown(Keys.Escape))
        {
            Close();
        }

        // dt = cât a durat cadrul, în secunde. Înmulțesc orice viteză cu dt ca mișcarea să fie
        // la fel de rapidă pe orice calculator, indiferent de câte cadre pe secundă are.
        float dt = (float)args.Time;
        SceneObject sel = _scene[_selected];

        // Tastele de ajutor din consolă (meniul, demonstrația matematică, starea curentă).
        if (KeyboardState.IsKeyPressed(Keys.H)) PrintMenu();
        if (KeyboardState.IsKeyPressed(Keys.M)) MathDemo();
        if (KeyboardState.IsKeyPressed(Keys.I)) PrintStatus();

        // --- Obiectul selectat: tastatura (secțiunea 8) ------------------------------

        // T2: Shift ținut = toate mișcările obiectului de 3 ori mai rapide. Calculez factorul o
        // singură dată aici și îl folosesc la săgeți, la rotație și la scalare, ca să nu repet
        // condiția de trei ori. (Accept și Shift-ul din dreapta, e mai comod.)
        float boost = (KeyboardState.IsKeyDown(Keys.LeftShift) || KeyboardState.IsKeyDown(Keys.RightShift))
            ? 3.0f : 1.0f;

        // Translația cu săgețile: viteză × dt, ca în șablon, plus factorul de Shift.
        float moveSpeed = 2.0f * boost;   // unități de lume pe secundă
        if (KeyboardState.IsKeyDown(Keys.Left))  sel.Transform.Position.X -= moveSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.Right)) sel.Transform.Position.X += moveSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.Down))  sel.Transform.Position.Y -= moveSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.Up))    sel.Transform.Position.Y += moveSpeed * dt;

        // T2: rotația. Q = sens trigonometric (+), E = sens orar (−), 90 de grade pe secundă.
        // Aici ADUN grade pe secundă la unghi, pentru că rotația se compune prin adunare
        // (90° + 90° = 180°). Unghiul îl aduc în [0; 360) doar ca să nu crească la nesfârșit.
        const float rotSpeed = 90.0f;     // grade pe secundă
        if (KeyboardState.IsKeyDown(Keys.Q)) sel.Transform.Rotation += rotSpeed * boost * dt;
        if (KeyboardState.IsKeyDown(Keys.E)) sel.Transform.Rotation -= rotSpeed * boost * dt;
        sel.Transform.Rotation = ((sel.Transform.Rotation % 360.0f) + 360.0f) % 360.0f;

        // T2: scalarea. Z micșorează, X mărește, cu factorul 1,5 pe secundă.
        // Aici ÎNMULȚESC, nu adun. Motivul (comentariul cerut la T2): dacă aș scădea ceva din scară
        // (de exemplu -0,5 pe secundă), scara ar ajunge la 0 și apoi la valori negative, iar o
        // scară negativă oglindește forma: obiectul ar dispărea și ar reapărea întors. Prin
        // înmulțire scara se apropie de 0 fără să-l atingă niciodată, iar mărirea și micșorarea
        // sunt simetrice: după X apăsat o secundă și Z apăsat o secundă revin exact unde eram
        // (1,5 · 1/1,5 = 1), ceea ce la adunare nu e valabil în același fel. Un factor de 1,5 pe
        // secundă pe un cadru de durată dt înseamnă înmulțire cu 1,5^dt, de aceea apar MathF.Pow și dt.
        // Cu Shift, exponentul se înmulțește cu 3 (adică factorul pe secundă devine 1,5^3).
        float scaleStep = MathF.Pow(1.5f, boost * dt);
        float scale = sel.Transform.Scale.X;
        if (KeyboardState.IsKeyDown(Keys.X)) scale *= scaleStep;
        if (KeyboardState.IsKeyDown(Keys.Z)) scale /= scaleStep;
        scale = Math.Clamp(scale, 0.1f, 10.0f);              // limitele cerute: între 0,1 și 10
        sel.Transform.Scale = new Vector2(scale, scale);     // scalare uniformă: aceeași valoare pe x și pe y

        // Mesajele din consolă apar doar în cadrul în care tasta tocmai a fost apăsată (IsKeyPressed),
        // nu în fiecare cadru, altfel consola s-ar umple de sute de linii pe secundă.
        if (KeyboardState.IsKeyPressed(Keys.Q)) Console.WriteLine($"[Q] rotește „{sel.Name}” în sens trigonometric (Shift = x3)");
        if (KeyboardState.IsKeyPressed(Keys.E)) Console.WriteLine($"[E] rotește „{sel.Name}” în sens orar (Shift = x3)");
        if (KeyboardState.IsKeyPressed(Keys.Z)) Console.WriteLine($"[Z] micșorează „{sel.Name}” (minim 0,1; Shift = x3)");
        if (KeyboardState.IsKeyPressed(Keys.X)) Console.WriteLine($"[X] mărește „{sel.Name}” (maxim 10; Shift = x3)");

        // Comutările (Tab, O, F, R) folosesc IsKeyPressed, nu IsKeyDown: o apăsare durează ~100 ms,
        // adică vreo 6 cadre, iar cu IsKeyDown comutarea s-ar executa de 6 ori și rezultatul ar fi întâmplător.
        if (KeyboardState.IsKeyPressed(Keys.Tab))
        {
            _selected = (_selected + 1) % _scene.Count;
            Console.WriteLine($"[Tab] obiect selectat: „{_scene[_selected].Name}”");
        }
        if (KeyboardState.IsKeyPressed(Keys.O))
        {
            _camera.IsPerspective = !_camera.IsPerspective;
            Console.WriteLine($"[O] proiecție: {(_camera.IsPerspective ? "în perspectivă" : "ortografică")}");
        }
        if (KeyboardState.IsKeyPressed(Keys.F))
        {
            _wireframe = !_wireframe;
            Console.WriteLine($"[F] mod desenare: {(_wireframe ? "contur" : "plin")}");
        }
        if (KeyboardState.IsKeyPressed(Keys.R))
        {
            ResetScene();
            Console.WriteLine("[R] scena și camera au revenit la starea inițială");
        }

        if (KeyboardState.IsKeyPressed(Keys.Tab) || KeyboardState.IsKeyPressed(Keys.O) ||
            KeyboardState.IsKeyPressed(Keys.R))
        {
            _titleDirty = true;
        }

        // Ținta (obiectul selectat) s-ar fi putut schimba mai sus prin Tab sau R; o iau din nou
        // ca mouse-ul și pivotul de mai jos să lucreze cu obiectul corect.
        sel = _scene[_selected];

        // --- Obiectul selectat: mouse-ul (secțiunea 9) -------------------------------

        // T3: clic stânga = mut obiectul selectat sub cursor.
        // Îi dau lui ScreenToWorld adâncimea obiectului, ca centrul lui să ajungă sub cursor
        // și în perspectivă; în ortografic adâncimea nu schimbă rezultatul. Depth rămâne neatins,
        // pentru că modific doar Position (x, y).
        if (MouseState.IsButtonPressed(MouseButton.Left))
        {
            Vector2 world = ScreenToWorld(MouseState.Position, sel.Transform.Depth);
            sel.Transform.Position = world;
            Console.WriteLine($"[Clic stânga] „{sel.Name}” mutat în ({world.X:F2}; {world.Y:F2})");
        }

        // T4 (facultativ): clic dreapta = aleg pivotul în punctul din lume aflat sub cursor.
        // Folosesc aceeași ScreenToWorld din T3, deci pivotul se comportă corect și după
        // mutarea camerei, zoom sau redimensionare.
        if (MouseState.IsButtonPressed(MouseButton.Right))
        {
            _pivot = ScreenToWorld(MouseState.Position, sel.Transform.Depth);
            _pivotDepth = sel.Transform.Depth;
            _hasPivot = true;
            Console.WriteLine($"[Clic dreapta] pivot ales în ({_pivot.X:F2}; {_pivot.Y:F2})");
        }

        // T4: cât timp țin P apăsată, obiectul se rotește în jurul pivotului cu 90°/s (x3 cu Shift).
        if (KeyboardState.IsKeyPressed(Keys.P))
        {
            Console.WriteLine(_hasPivot
                ? $"[P] „{sel.Name}” orbitează în jurul pivotului cât ții tasta apăsată"
                : "[P] nu există pivot: dă mai întâi clic dreapta undeva în scenă");
        }
        if (KeyboardState.IsKeyDown(Keys.P) && _hasPivot)
        {
            float step = rotSpeed * boost * dt;   // câte grade fac în acest cadru

            // Matricea din secțiunea 5.2: aduc pivotul în origine, rotesc, îl duc înapoi.
            // E ordinea T(−p) · R · T(p) scrisă în stilul C# (de la stânga la dreapta).
            Matrix4 aroundPivot = Matrix4.CreateTranslation(-_pivot.X, -_pivot.Y, 0.0f)
                                * Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(step))
                                * Matrix4.CreateTranslation(_pivot.X, _pivot.Y, 0.0f);

            // Rotesc POZIȚIA obiectului (un punct, deci a patra coordonată este 1).
            Vector4 moved = new Vector4(sel.Transform.Position.X, sel.Transform.Position.Y, 0.0f, 1.0f) * aroundPivot;
            sel.Transform.Position = new Vector2(moved.X, moved.Y);

            // Cerința spune că și unghiul propriu al obiectului crește cu aceeași valoare. Fără asta
            // obiectul s-ar plimba pe cerc, dar ar rămâne orientat la fel; așa privește mereu spre
            // pivot în același fel, ca Luna față de Pământ.
            sel.Transform.Rotation = (sel.Transform.Rotation + step) % 360.0f;
        }

        // --- Camera: tastatura și rotița (secțiunea 6.2) -----------------------------

        // Împart viteza la Zoom ca, pe ecran, scena să se miște la fel de repede la orice zoom.
        float camSpeed = 3.0f / _camera.Zoom;                    // unități de lume pe secundă
        if (KeyboardState.IsKeyDown(Keys.A)) _camera.Position.X -= camSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.D)) _camera.Position.X += camSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.S)) _camera.Position.Y -= camSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.W)) _camera.Position.Y += camSpeed * dt;

        _camera.Zoom *= MathF.Pow(1.1f, MouseState.ScrollDelta.Y);   // un pas al rotiței = ±10 %
        _camera.Zoom = Math.Clamp(_camera.Zoom, 0.1f, 10.0f);

        // --- T5: orbita satelitului ----------------------------------------------------
        // Unghiul orbitei crește mereu, indiferent de taste: 360° / 4 s = 90° pe secundă.
        _orbitAngle = (_orbitAngle + OrbitDegPerSec * dt) % 360.0f;

        // --- Titlul ferestrei: obiectul selectat, proiecția, cadre pe secundă --------

        _secondTimer += args.Time;
        if (_secondTimer >= 1.0)
        {
            _lastFrames = _frames;
            _frames = 0;
            _secondTimer -= 1.0;
            _titleDirty = true;
        }

        if (_titleDirty)
        {
            string projection = _camera.IsPerspective ? "perspectivă" : "ortografică";
            Title = $"{_baseTitle} | selectat: {_scene[_selected].Name} | proiecție {projection} | FPS: {_lastFrames}";
            _titleDirty = false;
        }
    }

    /// <summary>Apelată la fiecare iterație a buclei: DESENAREA unui cadru.</summary>
    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        _frames++;

        // 1. Ștergem imaginea veche din back buffer.
        GL.ClearColor(0.12f, 0.14f, 0.18f, 1.0f);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        // 2. Starea de rasterizare și programul shader.
        SetWireframe(_wireframe);
        _shader.Use();

        // 3. View și projection sunt aceleași pentru toate obiectele: le trimit o singură dată.
        _shader.SetMatrix4("uView", _camera.ViewMatrix);
        _shader.SetMatrix4("uProjection", _camera.ProjectionMatrix);

        // 4. Fiecare obiect: matricea lui model, nuanța, apoi desenarea.
        foreach (SceneObject obj in _scene)
        {
            _shader.SetMatrix4("uModel", obj.Transform.ModelMatrix);
            _shader.SetColor("uTint", obj.Tint);
            obj.Mesh.Draw();
        }

        // Marcajul pivotului (T4), desenat doar dacă am ales un pivot. Îl pun la adâncimea la
        // care l-am ales, ca în perspectivă să apară exact sub locul unde am dat clic.
        if (_hasPivot)
        {
            _pivotMarker.Transform.Position = _pivot;
            _pivotMarker.Transform.Depth = _pivotDepth;
            _shader.SetMatrix4("uModel", _pivotMarker.Transform.ModelMatrix);
            _shader.SetColor("uTint", _pivotMarker.Tint);
            _pivotMarker.Mesh.Draw();
        }

        // T5: satelitul obiectului selectat.
        // Transformarea LOCALĂ (față de părinte), în ordinea din laborator, scrisă de la stânga la dreapta:
        //   1. scalez triunghiul la 0,3 (să fie mic);
        //   2. îl mut la 1,2 pe axa x (aceasta este distanța față de părinte);
        //   3. îl rotesc cu unghiul orbitei. Rotația se face față de originea părintelui, deci
        //      satelitul merge pe cerc în jurul lui (exact cazul "T apoi R" din Fig. 3b).
        // Apoi înmulțesc cu matricea model a părintelui: model_copil = local * model_părinte.
        // Așa, când părintele se mută, se rotește sau se scalează, satelitul îl urmează automat.
        // Alegerea altui obiect cu Tab mută satelitul pe acesta, fiindcă citesc mereu _scene[_selected].
        // T5 (comentariul cerut): dacă obiectul selectat este scalat, ORBITA se mărește sau se
        // micșorează proporțional (și satelitul însuși). Motivul: matricea părintelui conține și
        // scalarea, iar eu o aplic ultima asupra întregii transformări locale. Distanța 1,2 este
        // măsurată în "unitățile locale" ale părintelui, deci la scara 2 devine 2,4 unități de lume
        // (iar satelitul, de 0,3, apare cu mărimea 0,6). La scara 0,5 orbita scade la 0,6.
        SceneObject parent = _scene[_selected];
        Matrix4 local = Matrix4.CreateScale(SatelliteScale, SatelliteScale, 1.0f)
                      * Matrix4.CreateTranslation(OrbitRadius, 0.0f, 0.0f)
                      * Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(_orbitAngle));
        _shader.SetMatrix4("uModel", local * parent.Transform.ModelMatrix);
        _shader.SetColor("uTint", _satellite.Tint);
        _satellite.Mesh.Draw();

        // 5. Afișăm cadrul terminat: back buffer <-> front buffer.
        SwapBuffers();
    }

    /// <summary>Apelată O SINGURĂ DATĂ, la închiderea ferestrei: eliberăm resursele GPU.</summary>
    protected override void OnUnload()
    {
        foreach (SceneObject obj in _scene)
        {
            obj.Dispose();
        }
        _pivotMarker.Dispose();
        _satellite.Dispose();
        _shader.Dispose();

        base.OnUnload();
    }

    /// <summary>
    /// T3: transformă poziția mouse-ului (pixeli, origine stânga-sus) în coordonate de lume.
    /// Practic parcurg lanțul de spații din secțiunea 2 în sens invers: pixel → NDC → lume.
    /// </summary>
    /// <param name="mousePixels">Poziția cursorului în pixeli, față de colțul stânga-sus al zonei de desenare.</param>
    /// <param name="planeZ">Adâncimea planului pe care vreau punctul (Depth-ul obiectului). În ortografic nu contează.</param>
    private Vector2 ScreenToWorld(Vector2 mousePixels, float planeZ = 0.0f)
    {
        // Pasul 1: pixel → NDC. Folosesc ClientSize (zona de desenare), nu Size (fereastra cu
        // bordură), altfel apare o mică abatere care crește spre margini.
        float width = ClientSize.X;
        float height = ClientSize.Y;

        float xNdc = 2.0f * mousePixels.X / width - 1.0f;

        // T3 (comentariul cerut): de ce apare "1 −" în formula lui y. În NDC axa y merge în SUS
        // (−1 jos, +1 sus), dar mouse-ul și sistemul de operare își măsoară y în JOS, de la colțul
        // stânga-sus al ferestrei. Axele sunt deci opuse și trebuie întoarsă axa: fără semnul minus
        // obiectul ar ajunge în punctul simetric față de axa orizontală. Verificare: y_mouse = 0
        // (marginea de sus) dă 1 − 0 = 1 (sus în NDC), iar y_mouse = H (jos) dă 1 − 2 = −1 (jos în NDC).
        float yNdc = 1.0f - 2.0f * mousePixels.Y / height;

        // Pasul 2: NDC → lume, cu inversa lui View * Projection (în ordinea din C#, secțiunea 4.2).
        // O calculez la fiecare clic, pentru că se schimbă când mișc camera, dau zoom sau
        // redimensionez fereastra; așa nu pot rămâne într-o stare veche.
        Matrix4 inverse = (_camera.ViewMatrix * _camera.ProjectionMatrix).Inverted();

        // Un pixel nu are adâncime proprie: el corespunde unei RAZE care pornește din cameră.
        // Transform deci două puncte ale razei, unul pe planul near (z_ndc = −1) și unul pe
        // planul far (z_ndc = +1), le împart la w (la perspectivă w ≠ 1; la ortografic w = 1,
        // deci împărțirea nu strică nimic) și intersectez dreapta obținută cu planul z = planeZ.
        // În ortografic cele două puncte au același (x, y), deci rezultatul e chiar cel cerut în
        // laborator; în perspectivă obțin în plus punctul corect de sub cursor.
        Vector4 nearH = new Vector4(xNdc, yNdc, -1.0f, 1.0f) * inverse;
        Vector4 farH = new Vector4(xNdc, yNdc, 1.0f, 1.0f) * inverse;
        Vector3 nearP = nearH.Xyz / nearH.W;
        Vector3 farP = farH.Xyz / farH.W;

        float dz = farP.Z - nearP.Z;
        float t = MathF.Abs(dz) < 1e-6f ? 0.0f : (planeZ - nearP.Z) / dz;   // protecție la împărțirea la ~0
        Vector3 hit = nearP + t * (farP - nearP);

        return new Vector2(hit.X, hit.Y);
    }

    /// <summary>Tasta R: obiectele revin la transformările inițiale, camera la starea inițială.</summary>
    private void ResetScene()
    {
        for (int i = 0; i < _scene.Count; i++)
        {
            _scene[i].Transform = _initialTransforms[i].Clone();
        }
        _camera.Reset();

        // Pivotul și orbita fac și ele parte din "starea scenei", așa că le readuc la zero.
        _hasPivot = false;
        _orbitAngle = 0.0f;
    }

    /// <summary>
    /// Contur / plin. Dacă Visual Studio semnalează o eroare la primul parametru,
    /// folosiți aici tipul acceptat de versiunea voastră (vezi laboratorul 2, Problema 3):
    /// TriangleFace.FrontAndBack sau MaterialFace.FrontAndBack.
    /// </summary>
    private static void SetWireframe(bool wireframe)
    {
        GL.PolygonMode(TriangleFace.FrontAndBack, wireframe ? PolygonMode.Line : PolygonMode.Fill);
    }

    // =====================================================================================
    //  Meniul și mesajele din consolă
    // =====================================================================================

    /// <summary>
    /// Afișează în consolă ce face fiecare tastă și fiecare buton. Îl tipăresc la pornire
    /// și oricând apas H, ca să nu fie nevoie să țin minte comenzile.
    /// </summary>
    private static void PrintMenu()
    {
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                 EGC - LABORATOR 04 - MENIU COMENZI               ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║ OBIECTUL SELECTAT                                                ║");
        Console.WriteLine("║  Tab ............ selectează obiectul următor                    ║");
        Console.WriteLine("║  Săgeți ......... deplasează obiectul (translație, 2 unități/s)  ║");
        Console.WriteLine("║  Q / E .......... rotește: trigonometric / orar (90°/s)          ║");
        Console.WriteLine("║  Z / X .......... micșorează / mărește (factor 1,5/s, 0,1-10)    ║");
        Console.WriteLine("║  Shift (ținut) .. toate mișcările de mai sus, de 3 ori mai rapid ║");
        Console.WriteLine("║  Clic stânga .... mută obiectul sub cursor                       ║");
        Console.WriteLine("║  Clic dreapta ... alege un pivot (apare un pătrat galben)        ║");
        Console.WriteLine("║  P (ținut) ...... rotește obiectul în jurul pivotului (90°/s)    ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║ CAMERA ȘI AFIȘARE                                                ║");
        Console.WriteLine("║  W A S D ........ deplasează camera                              ║");
        Console.WriteLine("║  Rotița mouse ... zoom (apropie / depărtează camera)             ║");
        Console.WriteLine("║  O .............. comută proiecția: ortografică / perspectivă    ║");
        Console.WriteLine("║  F .............. comută desenarea: plin / contur                ║");
        Console.WriteLine("║  R .............. readuce scena și camera la starea inițială     ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║ CONSOLĂ                                                          ║");
        Console.WriteLine("║  H .............. afișează din nou acest meniu                   ║");
        Console.WriteLine("║  I .............. afișează starea curentă (poziție, unghi, scară)║");
        Console.WriteLine("║  M .............. demonstrația matricelor (exercițiile 4.1, 5.1) ║");
        Console.WriteLine("║  Esc ............ închide aplicația                              ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║ Satelitul (triunghiul mic) orbitează mereu în jurul obiectului   ║");
        Console.WriteLine("║ selectat: o rotație completă la 4 secunde, la distanța 1,2.      ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
    }

    /// <summary>Tasta I: scriu în consolă starea obiectului selectat, a camerei și a pivotului.</summary>
    private void PrintStatus()
    {
        Transform2D t = _scene[_selected].Transform;
        Console.WriteLine($"[I] obiect „{_scene[_selected].Name}”: poziție ({t.Position.X:F2}; {t.Position.Y:F2}), " +
                          $"adâncime {t.Depth:F1}, unghi {t.Rotation:F1}°, scară {t.Scale.X:F2}");
        Console.WriteLine($"    cameră: poziție ({_camera.Position.X:F2}; {_camera.Position.Y:F2}), zoom {_camera.Zoom:F2}, " +
                          $"proiecție {(_camera.IsPerspective ? "perspectivă" : "ortografică")}");
        Console.WriteLine(_hasPivot
            ? $"    pivot: ({_pivot.X:F2}; {_pivot.Y:F2})"
            : "    pivot: nu a fost ales");
    }

    /// <summary>
    /// Tasta M: refac în consolă exercițiile din clasă 3.1 / 4.1 și 5.1, ca să văd cu ochii mei
    /// că matricele din OpenTK dau aceleași rezultate ca pe hârtie. Mici diferențe de tipul
    /// -1.0000001 sunt normale: sunt din precizia limitată a lui float, nu din cod.
    /// </summary>
    private static void MathDemo()
    {
        Console.WriteLine();
        Console.WriteLine("--- Demonstrație matematică (exercițiile 4.1 și 5.1) ---");

        var p = new Vector4(2, 1, 0, 1);    // punctul (2; 1), cu a patra coordonată 1 (e un PUNCT)
        Console.WriteLine($"Rotație 90° a lui (2;1)      -> {p * Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(90))}   (așteptat: (-1; 2))");
        Console.WriteLine($"Scalare (-1;1) a lui (2;1)   -> {p * Matrix4.CreateScale(-1, 1, 1)}   (așteptat: (-2; 1))");
        Console.WriteLine($"Translație (3;-2) a lui (2;1)-> {p * Matrix4.CreateTranslation(3, -2, 0)}   (așteptat: (5; -1))");
        Console.WriteLine($"Matricea translației (3;-2), cu valorile 3 și -2 pe ultima linie (Row3):");
        Console.WriteLine(Matrix4.CreateTranslation(3, -2, 0));

        // Ordinea contează: S * T nu este același lucru cu T * S.
        var q = new Vector4(1, 0, 0, 1);
        Matrix4 s = Matrix4.CreateScale(2);
        Matrix4 t = Matrix4.CreateTranslation(3, 0, 0);
        Console.WriteLine($"Întâi S, apoi T (S * T)      -> {q * (s * t)}   (așteptat: (5; 0))");
        Console.WriteLine($"Întâi T, apoi S (T * S)      -> {q * (t * s)}   (așteptat: (8; 0))");

        // Rotația lui (2;1) cu 90° în jurul pivotului (1;0): T(-p) * R * T(p).
        Matrix4 aroundPivot = Matrix4.CreateTranslation(-1, 0, 0)
                            * Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(90))
                            * Matrix4.CreateTranslation(1, 0, 0);
        Console.WriteLine($"(2;1) rotit 90° în jurul (1;0)-> {p * aroundPivot}   (așteptat: (0; 1))");
        Console.WriteLine();
    }
}
