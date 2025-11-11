using OpenTK.Graphics.OpenGL;
using System.Collections.Generic;

namespace AudioViz
{
    public class ShaderManager
    {
        private readonly Dictionary<string, int> _shaders = new Dictionary<string, int>();

        public int GetShader(string name) => _shaders.TryGetValue(name, out int shader) ? shader : 0;

        public void LoadAllShaders()
        {
            _shaders["waveform"] = CreateShaderProgram(GetWaveformVertexShader(), GetWaveformFragmentShader());
            _shaders["spectrum"] = CreateShaderProgram(GetSpectrumVertexShader(), GetSpectrumFragmentShader());
            _shaders["circle"] = CreateShaderProgram(GetCircleVertexShader(), GetCircleFragmentShader());
            _shaders["particles"] = CreateShaderProgram(GetParticleVertexShader(), GetParticleFragmentShader());
            _shaders["bars"] = CreateShaderProgram(GetBarsVertexShader(), GetBarsFragmentShader());
            _shaders["postprocess"] = CreateShaderProgram(GetPostProcessVertexShader(), GetPostProcessFragmentShader());
            _shaders["background"] = CreateShaderProgram(GetBackgroundVertexShader(), GetBackgroundFragmentShader());
            _shaders["circlebars"] = CreateShaderProgram(GetCircleBarsVertexShader(), GetCircleBarsFragmentShader());
            _shaders["backgroundParticles"] = CreateShaderProgram(GetBackgroundParticlesVertexShader(), GetBackgroundParticlesFragmentShader());
        }

        private int CreateShaderProgram(string vertexSource, string fragmentSource)
        {
            var vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, vertexSource);
            GL.CompileShader(vertexShader);
            CheckShaderError(vertexShader, "Vertex");

            var fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, fragmentSource);
            GL.CompileShader(fragmentShader);
            CheckShaderError(fragmentShader, "Fragment");

            var program = GL.CreateProgram();
            GL.AttachShader(program, vertexShader);
            GL.AttachShader(program, fragmentShader);
            GL.LinkProgram(program);
            CheckProgramError(program);

            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);

            return program;
        }

        private void CheckShaderError(int shader, string type)
        {
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string log = GL.GetShaderInfoLog(shader);
                Console.WriteLine("======================================");
                Console.WriteLine($" SHADER COMPILATION ERROR ({type})");
                Console.WriteLine("======================================");
                Console.WriteLine(log);
                Console.WriteLine("======================================");
            }
        }

        private void CheckProgramError(int program)
        {
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int success);
            if (success == 0)
            {
                string log = GL.GetProgramInfoLog(program);
                Console.WriteLine("======================================");
                Console.WriteLine(" PROGRAM LINK ERROR");
                Console.WriteLine("======================================");
                Console.WriteLine(log);
                Console.WriteLine("======================================");
            }
        }
        private string GetCircleBarsVertexShader() => @"
            #version 330 core
            layout (location = 0) in vec2 aPos;
            layout (location = 1) in float aIntensity;
            
            out float intensity;
            out float angleNorm;
            
            uniform float uTime;
            
            void main()
            {
                intensity = aIntensity;
                angleNorm = aPos.x;
                
                float angle = aPos.x * 2.0 * 3.14159265359;
                float radius = aPos.y;
                
                float wave = sin(uTime * 4.0 + aPos.x * 15.0) * 0.02 * intensity;
                radius += wave;
                
                float x = cos(angle) * radius;
                float y = sin(angle) * radius;
                
                gl_Position = vec4(x, y, 0.0, 1.0);
            }";

        private string GetCircleBarsFragmentShader() => @"
            #version 330 core
            in float intensity;
            in float angleNorm;
            out vec4 FragColor;
            uniform float uTime;
            uniform float uBeat;
            
            void main()
            {
                float hue = (angleNorm + uTime * 0.2) * 0.8;
                vec3 color = vec3(
                    sin(hue * 6.28318) * 0.5 + 0.5,
                    sin(hue * 6.28318 + 2.094) * 0.5 + 0.5,
                    sin(hue * 6.28318 + 4.189) * 0.5 + 0.5
                );
                
                float glow = intensity * 4.0;
                float pulse = sin(uTime * 3.0 + angleNorm * 10.0) * 0.3 + 0.8;
                float beatBoost = 1.0 + uBeat * 1.5;
                
                vec3 finalColor = color * glow * pulse * beatBoost * 1.3;
                
                FragColor = vec4(finalColor, 1.0);
            }";
        private string GetBackgroundParticlesVertexShader() => @"
            #version 330 core
            layout (location = 0) in vec2 aPosition;
            layout (location = 1) in vec3 aColor;
            layout (location = 2) in float aSize;
            layout (location = 3) in float aLife;
            layout (location = 4) in float aMaxLife;
            
            out vec3 color;
            out float life;
            out float maxLife;
            
            uniform float uTime;
            
            void main()
            {
                color = aColor;
                life = aLife;
                maxLife = aMaxLife;
                
                float jitter = sin(uTime * 5.0 + aPosition.x * 20.0) * 0.01;
                vec2 animatedPos = aPosition + vec2(jitter, jitter * 0.5);
                
                gl_Position = vec4(animatedPos, 0.0, 1.0);
                
                float lifeFactor = life / maxLife;
                gl_PointSize = aSize * (0.5 + 0.5 * lifeFactor);
            }";
        private string GetBackgroundParticlesFragmentShader() => @"
            #version 330 core
            in vec3 color;
            in float life;
            in float maxLife;
            out vec4 FragColor;
            
            uniform float uTime;
            
            void main()
            {
                vec2 coord = gl_PointCoord - vec2(0.5);
                float dist = length(coord);
                
                if (dist > 0.5)
                    discard;
                
                float lifeFactor = life / maxLife;
                float alpha = (1.0 - dist * 2.0) * lifeFactor * 0.8;
                
                float glow = pow(1.0 - dist * 2.0, 2.0) * 0.3;
                
                vec3 finalColor = color + vec3(glow);
                
                FragColor = vec4(finalColor, alpha);
            }";
        private string GetBackgroundVertexShader() => @"
            #version 330 core
            layout (location = 0) in vec2 aPosition;
            layout (location = 1) in vec2 aTexCoord;
            out vec2 texCoord;
            void main()
            {
                texCoord = aTexCoord;
                gl_Position = vec4(aPosition, 0.0, 1.0);
            }";

        private string GetBackgroundFragmentShader() => @"
            #version 330 core
            in vec2 texCoord;
            out vec4 FragColor;
            uniform sampler2D uBackground;
            uniform float uTime;
            
            void main()
            {
                vec2 uv = texCoord;
                
                uv.x += sin(uTime * 0.1 + uv.y * 3.0) * 0.002;
                uv.y += cos(uTime * 0.08 + uv.x * 2.0) * 0.002;
                
                vec4 bgColor = texture(uBackground, uv);
                
                FragColor = vec4(bgColor.rgb, 1.0);
            }";

        private string GetWaveformVertexShader() => @"
            #version 330 core
            layout (location = 0) in vec2 aPosition;
            layout (location = 1) in float aIntensity;
            out float intensity;
            out float position;
            uniform float uTime;
            uniform vec2 uResolution;
            void main()
            {
                intensity = aIntensity;
                position = aPosition.x;
                float pulse = sin(uTime * 3.0 + aPosition.x * 8.0) * 0.15 + 0.85;
                float wave = sin(uTime * 5.0 + aPosition.x * 20.0) * 0.1;
                float y = aPosition.y * pulse + wave;
                gl_Position = vec4(aPosition.x * 2.0 - 1.0, y, 0.0, 1.0);
            }";

        private string GetWaveformFragmentShader() => @"
            #version 330 core
            in float intensity;
            in float position;
            out vec4 FragColor;
            uniform float uTime;
            uniform vec2 uResolution;
            void main()
            {
                vec3 color1 = vec3(0.2, 0.6, 1.0);
                vec3 color2 = vec3(1.0, 0.4, 0.9);
                vec3 color3 = vec3(0.4, 1.0, 0.8);
                vec3 baseColor = mix(color1, color2, sin(uTime * 2.0 + position * 5.0) * 0.5 + 0.5);
                baseColor = mix(baseColor, color3, cos(uTime * 1.5 + position * 3.0) * 0.5 + 0.5);
                
                float glow = intensity * 3.5;
                float pulse = sin(uTime * 4.0 + intensity * 10.0) * 0.4 + 0.8;
                vec3 finalColor = baseColor * glow * pulse;
                
                float alpha = intensity * 0.98 + 0.15;
                FragColor = vec4(finalColor, alpha);
            }";

        private string GetSpectrumVertexShader() => @"
            #version 330 core
            layout (location = 0) in vec2 aPosition;
            out float frequency;
            uniform float uTime;
            void main()
            {
                frequency = aPosition.x;
                float ripple = sin(uTime * 6.0 + aPosition.x * 30.0) * 0.08;
                float wave = cos(uTime * 4.0 + aPosition.x * 15.0) * 0.05;
                gl_Position = vec4(aPosition.x * 2.0 - 1.0, aPosition.y + ripple + wave, 0.0, 1.0);
            }";

        private string GetSpectrumFragmentShader() => @"
            #version 330 core
            in float frequency;
            out vec4 FragColor;
            uniform float uTime;
            void main()
            {
                vec3 lowFreq = vec3(1.0, 0.3, 0.5);
                vec3 midFreq = vec3(0.3, 1.0, 0.6);
                vec3 highFreq = vec3(0.4, 0.6, 1.0);
                
                float mix1 = smoothstep(0.0, 0.5, frequency);
                float mix2 = smoothstep(0.5, 1.0, frequency);
                vec3 color = mix(lowFreq, midFreq, mix1);
                color = mix(color, highFreq, mix2);
                
                float pulse = sin(uTime * 3.0 + frequency * 10.0) * 0.5 + 0.8;
                color *= pulse * 1.3;
                
                FragColor = vec4(color, 1.0);
            }";

        private string GetCircleVertexShader() => @"
            #version 330 core
            layout(location = 0) in vec2 aPos;
            layout(location = 1) in float aIntensity;

            out float intensity;
            out float barIndex;

            uniform float uTime;

            void main()
            {
                intensity = aIntensity;
                barIndex = aPos.x;

                float angle = aPos.x * 6.28318;
                float r = aPos.y;

                if (r > 0.29) {
                    float wave = sin(uTime * 6.0 + aPos.x * 20.0) * 0.03 * intensity;
                    r += wave;
                }

                float x = cos(angle) * r;
                float y = sin(angle) * r;

                gl_Position = vec4(x, y, 0.0, 1.0);
            }";

        private string GetCircleFragmentShader() => @"
            #version 330 core
            in float intensity;
            in float barIndex;
            out vec4 FragColor;
            uniform float uTime;
            uniform float uBeat;

            void main()
            {
                float hue = (barIndex + uTime * 0.3) * 0.8;

                vec3 color = vec3(
                    sin(hue * 6.28) * 0.5 + 0.5,
                    sin(hue * 6.28 + 2.094) * 0.5 + 0.5,
                    sin(hue * 6.28 + 4.189) * 0.5 + 0.5
                );

                float glow = intensity * 3.5;
                float pulse = sin(uTime * 5.0 + barIndex * 8.0) * 0.4 + 0.8;

                float beatBoost = 1.0 + uBeat * 1.4;

                vec3 finalColor = color * glow * pulse * 1.2 * beatBoost;

                FragColor = vec4(finalColor, 1.0);
            }";

        private string GetParticleVertexShader() => @"
            #version 330 core
            layout (location = 0) in vec2 aPosition;
            layout (location = 1) in float aIntensity;
            out float intensity;
            uniform float uTime;
            uniform vec2 uResolution;
            void main()
            {
                intensity = aIntensity;
                float angle = aPosition.x * 3.14159 * 2.0;
                float radius = 0.1 + aIntensity * 0.8;
                float rotation = uTime * 0.5;
                float x = cos(angle + rotation) * radius;
                float y = sin(angle + rotation) * radius;
                gl_Position = vec4(x, y, 0.0, 1.0);
                gl_PointSize = 15.0 + aIntensity * 25.0;
            }";

        private string GetParticleFragmentShader() => @"
            #version 330 core
            in float intensity;
            out vec4 FragColor;
            uniform float uTime;
            void main()
            {
                vec2 center = gl_PointCoord - vec2(0.5);
                float dist = length(center);
                if (dist > 0.5) discard;
                
                vec3 color1 = vec3(1.0, 0.4, 1.0);
                vec3 color2 = vec3(0.4, 0.9, 1.0);
                vec3 color = mix(color1, color2, sin(uTime * 3.0) * 0.5 + 0.5);
                
                float alpha = (1.0 - dist * 2.0) * intensity;
                FragColor = vec4(color * intensity * 1.5, alpha);
            }";

        private string GetBarsVertexShader() => @"
            #version 330 core
            layout(location = 0) in vec2 aPosition;
            layout(location = 1) in float aIntensity;

            out float intensity;
            out float barIndex;

            uniform float uTime;

            void main()
            {
                intensity = aIntensity;
                barIndex = aPosition.x; 

                float x = aPosition.x * 2.0 - 1.0;
                float y = aPosition.y;
                if (y > -0.99) {

                    float waveFreq = 6.0;
                    float wavePhase = aPosition.x * 12.0;
                    float waveAmp = 0.03; 
                    float wave = sin(uTime * waveFreq + wavePhase) * waveAmp * intensity;
                    y += wave;
                }

                gl_Position = vec4(x, y, 0.0, 1.0);
            }";

        private string GetBarsFragmentShader() => @"
            #version 330 core

            in float intensity;
in float barIndex;
out vec4 FragColor;
uniform float uTime;
uniform float uBeat;

void main()
{
    float hue = (barIndex + uTime * 0.3) * 0.8;
    vec3 color = vec3(
        sin(hue * 6.28) * 0.5 + 0.5,
        sin(hue * 6.28 + 2.094) * 0.5 + 0.5,
        sin(hue * 6.28 + 4.189) * 0.5 + 0.5
    );

    float glow = intensity * 3.5;
    float pulse = sin(uTime * 5.0 + barIndex * 8.0) * 0.4 + 0.8;

    float beatBoost = 1.0 + uBeat * 1.4;

    vec3 finalColor = color * glow * pulse * 1.2 * beatBoost;

    FragColor = vec4(finalColor, 1.0);
}";

        private string GetPostProcessVertexShader() => @"
            #version 330 core
            layout (location = 0) in vec2 aPosition;
            out vec2 texCoord;
            void main()
            {
                texCoord = aPosition * 0.5 + 0.5;
                gl_Position = vec4(aPosition, 0.0, 1.0);
            }";

        private string GetPostProcessFragmentShader() => @"
            #version 330 core
            in vec2 texCoord;
            out vec4 FragColor;
            uniform sampler2D uTexture;
            uniform float uTime;
            void main()
            {
                vec3 color = texture(uTexture, texCoord).rgb;
                
                vec3 glow = vec3(0.0);
                for (int i = -2; i <= 2; i++)
                {
                    for (int j = -2; j <= 2; j++)
                    {
                        vec2 offset = vec2(i, j) / 800.0;
                        glow += texture(uTexture, texCoord + offset).rgb;
                    }
                }
                glow /= 25.0;
                color = mix(color, glow, 0.3);
                
                vec2 center = texCoord - vec2(0.5);
                float vignette = 1.0 - dot(center, center) * 0.5;
                color *= vignette;
                
                FragColor = vec4(color, 1.0);
            }";

        public void Dispose()
        {
            foreach (var shader in _shaders.Values)
            {
                GL.DeleteProgram(shader);
            }
            _shaders.Clear();
        }
    }
}

