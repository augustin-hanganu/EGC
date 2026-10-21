#version 330 core

// Atributele vertexului (din VBO, descrise in VAO)
layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aColor;

// Uniforme: aceeasi valoare pentru toate varfurile unui apel de desenare
uniform mat4 uView;
uniform mat4 uProjection;
uniform vec3 uOffset;

// Iesire catre fragment shader (valoarea va fi interpolata la rasterizare)
out vec3 vColor;

void main()
{
    vColor = aColor;
    gl_Position = uProjection * uView * vec4(aPosition + uOffset, 1.0);
}
