using OpenTK.Mathematics;

namespace Egc;

/// <summary>
/// Schelet minimal de cameră (NESCHIMBAT față de laboratorul 2).
/// Ambele matrice sunt matricea identitate, deci vârfurile rămân în coordonatele
/// lor "native" (NDC). Proiecția va fi completată în laboratorul următor; ea va
/// rezolva și deformarea formelor la redimensionarea ferestrei.
/// </summary>
public class Camera
{
    /// <summary>Raportul lățime / înălțime al ferestrei (încă nefolosit la desenare).</summary>
    public float AspectRatio { get; private set; } = 1.0f;

    /// <summary>Matricea de vizualizare (view): poziția/orientarea camerei.</summary>
    public Matrix4 ViewMatrix { get; private set; } = Matrix4.Identity;

    /// <summary>Matricea de proiecție (projection): ortografică sau perspectivă.</summary>
    public Matrix4 ProjectionMatrix { get; private set; } = Matrix4.Identity;

    /// <summary>Apelată din Game.OnResize: reține noul raport de aspect.</summary>
    public void Resize(int width, int height)
    {
        AspectRatio = width / (float)Math.Max(height, 1);
    }
}
