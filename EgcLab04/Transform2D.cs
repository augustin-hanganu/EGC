using OpenTK.Mathematics;

namespace Egc;

/// <summary>
/// Aici țin poziția, rotația și scalarea unui obiect din scenă. Din cele trei valori
/// construiesc matricea model, adică matricea care duce vârfurile obiectului din
/// object space (forma desenată în jurul originii) în world space (locul real din scenă).
/// Am păstrat clasa simplă ca să se vadă clar ce intră în matrice.
/// </summary>
public class Transform2D
{
    public Vector2 Position = Vector2.Zero;   // translația, în unități de lume
    public float Rotation = 0.0f;             // unghiul, în GRADE, pozitiv = sens trigonometric
    public Vector2 Scale = Vector2.One;       // factorii de scalare pe x și pe y
    public float Depth = 0.0f;                // coordonata z; contează doar în perspectivă (secțiunea 7.2)

    /// <summary>
    /// T1: matricea model, în ordinea scalare → rotație → translație.
    ///
    /// Ce am scris aici: trei matrice predefinite din OpenTK, înmulțite una după alta.
    /// De ce în ordinea asta: în C# (OpenTK folosește vectori linie) matricele se înmulțesc
    /// în ordinea în care se aplică transformările, de la stânga la dreapta. Deci vârful trece
    /// întâi prin scalare, apoi prin rotație, apoi prin translație.
    ///
    /// De ce nu altfel: scalarea și rotația se fac mereu față de ORIGINE. Cât timp obiectul
    /// are încă centrul în origine (adică înainte de translație), el se scalează și se rotește
    /// "pe loc". Abia la sfârșit îl mut în poziția lui din scenă.
    ///
    /// T1 (comentariul cerut): dacă translația ar fi aplicată ÎNAINTEA rotației (T * R în C#),
    /// obiectul ar fi mutat întâi în Position, iar rotația l-ar roti apoi față de originea lumii,
    /// nu față de centrul lui. Nu s-ar mai învârti pe loc, ci ar descrie un arc de cerc în jurul
    /// originii (de exemplu un obiect din (2; 1) rotit cu 90° ar ajunge în (-1; 2)).
    /// La fel s-ar întâmpla cu scalarea: ar înmulți și deplasarea, deci obiectul s-ar îndepărta
    /// de origine pe măsură ce îl mărim. Pentru unghiul 0 și scara 1 nu s-ar vedea nicio diferență,
    /// de aceea eroarea apare abia când încep să rotesc sau să scalez.
    ///
    /// Detalii mici: pe z scalarea este 1 (obiectele sunt plate, nu vreau să le modific
    /// adâncimea), iar Depth intră direct ca z în translație (se vede doar în perspectivă).
    /// Rotation este în grade, dar CreateRotationZ vrea radiani, de aceea apelez
    /// MathHelper.DegreesToRadians; fără conversie rotația ar fi de ~57 de ori prea mare.
    /// </summary>
    public Matrix4 ModelMatrix =>
        Matrix4.CreateScale(Scale.X, Scale.Y, 1.0f)
      * Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(Rotation))
      * Matrix4.CreateTranslation(Position.X, Position.Y, Depth);

    /// <summary>O copie independentă (folosită pentru readucerea scenei în starea inițială).</summary>
    public Transform2D Clone() => (Transform2D)MemberwiseClone();
}
