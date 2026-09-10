# ShaderLabStudio — Laboratório Interativo de Programação de Shaders / Interactive Shader Programming Lab

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.md)
![Build Release](https://github.com/masterrey/ShaderLabStudio/actions/workflows/release.yml/badge.svg)
[![Latest Release](https://img.shields.io/github/v/release/masterrey/ShaderLabStudio?display_name=tag)](https://github.com/masterrey/ShaderLabStudio/releases)
[![Downloads](https://img.shields.io/github/downloads/masterrey/ShaderLabStudio/total)](https://github.com/masterrey/ShaderLabStudio/releases)
![Platform: .NET 9.0](https://img.shields.io/badge/Platform-.NET%209.0-512BD4)
![Graphics: OpenGL](https://img.shields.io/badge/Graphics-OpenGL%204.5+-412991)

**Ferramenta educacional desenvolvida para a disciplina de Jogos Digitais**  
**Educational tool developed for the Digital Games course**  
**Pontifícia Universidade Católica de São Paulo (PUC-SP)**  
**Pontifical Catholic University of Sao Paulo (PUC-SP)**

---

## 📚 Sobre o Projeto / About the Project

### PT-BR

ShaderLabStudio é um ambiente interativo de aprendizagem projetado para ensinar os fundamentos da programação de shaders em **OpenGL Shading Language (GLSL)**. O projeto foi renomeado para acomodar a futura adição de suporte a **HLSL**. A ferramenta oferece uma interface gráfica intuitiva no modo Studio e um ambiente de console legado, permitindo que estudantes experimentem, criem e visualizem shaders em tempo real.

### EN

ShaderLabStudio is an interactive learning environment designed to teach the fundamentals of shader programming in **OpenGL Shading Language (GLSL)**. The project was renamed to accommodate the future addition of **HLSL** support. The tool provides an intuitive graphical Studio interface and a legacy console environment, allowing students to experiment with, create, and visualize shaders in real time.

### Objetivos Educacionais / Educational Goals

- **Compreender o pipeline gráfico / Understand the graphics pipeline**: conceitos fundamentais de renderização e processamento de vértices e fragmentos
- **Dominar GLSL / Master GLSL**: sintaxe, tipos de dados, funções built-in e técnicas de programação de shaders
- **Explorar técnicas avançadas / Explore advanced techniques**: iluminação, efeitos visuais, animação e processamento de texturas
- **Aplicar na prática / Apply knowledge in practice**: desenvolvimento de shaders reais para aplicações gráficas interativas
- **Integração com jogos / Integration with games**: compreender o papel crítico dos shaders no desenvolvimento de jogos digitais

### Público-Alvo / Target Audience

- Estudantes de Jogos Digitais (graduação) / Undergraduate Digital Games students
- Desenvolvedores gráficos iniciantes / Beginner graphics developers
- Pesquisadores interessados em técnicas de renderização / Researchers interested in rendering techniques
- Educadores em computação gráfica / Graphics programming educators

---

## 🚀 Início Rápido / Quick Start

### PT-BR

Para uso em aula ou laboratório, basta baixar uma release pronta. Não é necessário instalar .NET nem compilar o projeto.

#### 1. Baixar a aplicação

1. Acesse a página de [**Releases**](https://github.com/masterrey/ShaderLabStudio/releases)
2. Baixe a release mais recente para Windows x64
3. **Descompacte** em qualquer pasta

#### 2. Requisitos mínimos

- **Windows 10/11** (64-bit)
- **OpenGL 4.5+** (placa gráfica compatível)
  - A maioria das placas modernas (NVIDIA, AMD, Intel) suporta
  - Na primeira execução, se houver erro de OpenGL, verifique os drivers

#### 3. Executar

Abra a pasta descompactada e clique duas vezes em:

```text
GLSLShaderLab.App.Wpf.exe
```

Pronto. A aplicação está pronta para uso. Comece pelos exemplos inclusos em `Shaders/`.

### EN

For classroom or lab use, download a ready-made release. You do not need to install .NET or build the project.

#### 1. Download the application

1. Open the [**Releases**](https://github.com/masterrey/ShaderLabStudio/releases) page
2. Download the latest Windows x64 release
3. **Extract** it to any folder

#### 2. Minimum requirements

- **Windows 10/11** (64-bit)
- **OpenGL 4.5+** (compatible graphics card)
  - Most modern GPUs (NVIDIA, AMD, Intel) support it
  - On first run, if you get an OpenGL error, check your drivers

#### 3. Run

Open the extracted folder and double-click:

```text
GLSLShaderLab.App.Wpf.exe
```

That is it. The application is ready to use. Start with the examples in `Shaders/`.

### Interface do App (Studio) / App Interface (Studio)

Ao abrir o ShaderLabStudio Studio, você verá cinco áreas principais.

When you open ShaderLabStudio Studio, you will see five main areas.

1. **Barra de ferramentas (topo) / Toolbar (top)**
- `Open`, `Save`, `Save As`: abrir e salvar shaders / open and save shaders
- `Compile` e `Auto`: compilar manualmente ou automaticamente durante a edição / compile manually or automatically while editing
- `Pause`, `Reset Time`, `Fullscreen`: controlar animação e visualização / control animation and viewing
- `Mode` (2D / 3D), `Model`, `Reset Camera`: fluxo para renderização 2D e 3D / workflow for 2D and 3D rendering
- `Template`: carregar templates de shader para estudo / load shader templates for study

2. **Editor de Shader (lado esquerdo) / Shader Editor (left side)**
- Aba **Fragment Shader**: código principal do efeito visual / main code for the visual effect
- Aba **Vertex Shader (3D)**: código de vértice para cenários 3D / vertex code for 3D scenarios
- Edição em tempo real com compilação rápida / real-time editing with fast compilation

3. **Preview (lado direito) / Preview (right side)**
- Área de renderização OpenGL com resultado visual imediato / OpenGL rendering area with immediate visual feedback
- Botões `Load iChannel0` a `Load iChannel3` para carregar texturas e canais auxiliares / buttons `Load iChannel0` through `Load iChannel3` for auxiliary textures and channels

4. **Diagnostics (parte inferior) / Diagnostics (bottom area)**
- Exibe mensagens de compilação, erros e avisos / shows compile messages, errors, and warnings
- Use esta área para depurar rapidamente seu shader / use this area to quickly debug your shader

5. **Status Bar (rodapé) / Status Bar (footer)**
- Estado atual da aplicação (`Ready`, compilando, etc.) / current application state (`Ready`, compiling, etc.)
- FPS em tempo real / real-time FPS

---

### Para Desenvolvedores / For Developers

Se você quer modificar o código-fonte, contribuir com correções ou adicionar novas funcionalidades, siga esta seção.

If you want to modify the source code, contribute fixes, or add new features, follow this section.

> Esta parte é destinada a contribuidores open source. Para uso em aula ou laboratório, utilize o fluxo de release acima.  
> This section is intended for open source contributors. For classroom or lab use, follow the release flow above.

#### Requisitos / Requirements

- **.NET 9.0** ou superior / **.NET 9.0** or later
- **OpenGL 4.5+** (placa gráfica compatível) / **OpenGL 4.5+** (compatible graphics card)
- **Visual Studio Code** ou **Visual Studio 2022/2024** (recomendado) / **Visual Studio Code** or **Visual Studio 2022/2024** (recommended)
- **Git**

#### Setup do Projeto / Project Setup

```bash
# Clone the repository / Clonar o repositório
git clone https://github.com/masterrey/ShaderLabStudio.git
cd ShaderLabStudio

# Restore dependencies / Restaurar dependências
dotnet restore

# Build (debug) / Compilar (debug)
dotnet build GLSLShaderLab.sln

# Run in development / Executar em desenvolvimento
dotnet run --project src/GLSLShaderLab.App.Wpf/GLSLShaderLab.App.Wpf.csproj
```

**No VS Code / Visual Studio:** pressione **F5**. A configuração padrão de inicialização do Studio WPF será ativada automaticamente.  
**In VS Code / Visual Studio:** press **F5**. The default Studio WPF launch configuration will start automatically.

#### Legacy (Console) / Legacy (Console)

Para estudar a implementação anterior ou executar exemplos legados:

To study the previous implementation or run legacy samples:

```bash
dotnet run --project GLSLShaderLab.csproj
```

Documentação completa: [legacy/README.md](legacy/README.md)

## 🏗️ Arquitetura do Projeto / Project Architecture

O projeto está organizado em dois fluxos de trabalho.

The project is organized into two workflows.

### Studio (Novo) / Studio (New)

```
GLSLShaderLab.sln
├── src/
│   ├── GLSLShaderLab.App.Wpf/       [UI - Interface WPF]
│   │   ├── MainWindow.xaml          - Janela principal com editor e preview / main window with editor and preview
│   │   └── App.xaml.cs              - Lógica de inicialização / startup logic
│   ├── GLSLShaderLab.Engine/        [Rendering - OpenGL]
│   │   ├── Rendering/               - Geometria e renderização / geometry and rendering
│   │   └── Services/                - Compilação e gerenciamento de shaders / shader compilation and management
│   └── GLSLShaderLab.Core/          [Data Models]
│       ├── Models/                  - DocumentModel, ShaderTemplate, etc.
│       └── Services/                - Persistência e catálogo de templates / persistence and template catalog
```

**Componentes principais / Main components:**

| Componente | Responsabilidade | Linguagem |
|---|---|---|
| **App.Wpf** | Interface gráfica, editor de texto, viewport OpenGL / graphical UI, text editor, OpenGL viewport | C# + XAML |
| **Engine** | Renderização OpenGL, compilação GLSL, gerenciamento de texturas e buffers / OpenGL rendering, GLSL compilation, texture and buffer management | C# + OpenGL |
| **Core** | Modelos de dados, templates de shaders, persistência de sessão / data models, shader templates, session persistence | C# |

### Legacy (Antigo) / Legacy (Old)

A estrutura raiz contém a implementação original baseada em console.

The repository root contains the original console-based implementation.

```
ShaderLabStudio/
├── Program.cs
├── Shader.cs, BufferManager.cs, etc.
├── Shaders/                         - Exemplos e referências / examples and references
└── Mesh/                            - Geometrias 3D pré-compiladas / precompiled 3D meshes
```

---

## 📖 Como Usar / How to Use

### Para Aprender Shaders / To Learn Shaders

1. **Abra o Studio** (F5 no VS Code) / **Open Studio** (F5 in VS Code)
2. **Selecione um shader de exemplo** do menu ou catálogo / **Select a sample shader** from the menu or catalog
3. **Modifique o código** no editor GLSL / **Modify the code** in the GLSL editor
4. **Visualize em tempo real** na viewport OpenGL / **Preview in real time** in the OpenGL viewport
5. **Experimente** parâmetros, texturas e geometrias / **Experiment** with parameters, textures, and geometries
6. **Salve seu trabalho** como novo template / **Save your work** as a new template

### Exemplos Incluídos / Included Examples

A pasta `Shaders/` contém diversos exemplos funcionais.

The `Shaders/` folder contains several working examples.

- **Shaders 2D**: `SimplePaint.glsl`, `Ripple.glsl`, `Clouds.glsl`
- **Shaders 3D**: `basic3d.glsl`, `Simple3d.glsl`, `Normal.glsl`
- **Efeitos / Effects**: `AnimatedFlag.glsl`, `BufferDemo.glsl`, `Functions.glsl`

Consulte [SHADER_TUTORIAL.md](SHADER_TUTORIAL.md) para guias passo a passo.  
See [SHADER_TUTORIAL.md](SHADER_TUTORIAL.md) for step-by-step guides.

---

## 🔧 Desenvolvimento e Contribuição / Development and Contribution

### Compilação / Build

```bash
# Debug
dotnet build GLSLShaderLab.sln

# Release
dotnet publish GLSLShaderLab.sln --configuration Release
```

### Estrutura de Contribuição / Contribution Flow

Para adicionar novos recursos ou corrigir bugs:

To add new features or fix bugs:

1. **Crie uma branch**: `git checkout -b feature/sua-feature`
2. **Implemente e teste**: execute `dotnet build` e os testes disponíveis
3. **Atualize a documentação**: adicione exemplos e instruções quando necessário
4. **Envie um Pull Request**: descreva as mudanças de forma clara

### Problemas Conhecidos / Known Issues

Consulte [TROUBLESHOOTING.md](TROUBLESHOOTING.md) para soluções comuns.  
See [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for common fixes.

---

## 📚 Recursos Educacionais / Educational Resources

- **[SHADER_TUTORIAL.md](SHADER_TUTORIAL.md)** — Tutoriais interativos de GLSL (iniciante a avançado) / interactive GLSL tutorials (beginner to advanced)
- **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** — Solução de problemas comuns / common troubleshooting
- **[legacy/README.md](legacy/README.md)** — Referência da versão anterior / reference for the previous version

### Referências Externas / External References

- [Khronos OpenGL Documentation](https://www.khronos.org/opengl/)
- [GLSL Language Specification](https://www.khronos.org/files/webgl20-reference-guide.pdf)
- [The Book of Shaders](https://thebookofshaders.com/) — Tutorial interativo online / interactive online tutorial
- [Shadertoy](https://www.shadertoy.com/) — Comunidade de shaders e inspiração / shader community and inspiration

---

## 👨‍🏫 Sobre o Desenvolvedor / About the Developer

**Prof. Dr. Reinaldo Ramos**  
Professor — Disciplina de Jogos Digitais  

Pontifícia Universidade Católica de São Paulo (PUC-SP)

This project was developed as a teaching aid to support practical learning of shader programming in a structured academic context.

**Perfil profissional / Professional profile:** [PhD Reinaldo Ramos (LinkedIn)](https://www.linkedin.com/safety/go/?url=https%3A%2F%2Ft4interactive.com%2Fphd-reinaldo-ramos&urlhash=Zawb&mt=5WA44W67XjgEpcsxNDligLuYyTV4sbn0mScllpz_gyU5z_leZqUHAnnIoaNhfZcgnJdeRp1KTrj-WIPc4o3_QNjxQg&isSdui=true&lipi=urn%3Ali%3Apage%3Ad_flagship3_profile_view_base%3B9wh%2F6HaaQxCqVHd6iHUWBg%3D%3D)

---

## 📄 Licença / License

Este projeto é licenciado sob a [Licença MIT](LICENSE.md).  
This project is licensed under the [MIT License](LICENSE.md).

**Atribuição sugerida / Suggested citation:**

```bibtex
@software{shaderlabstudio2024,
  author = {Ramos, Reinaldo},
  title = {ShaderLabStudio: Interactive Laboratory for Shader Programming},
  year = {2024},
  howpublished = {\url{https://github.com/masterrey/ShaderLabStudio}},
  institution = {PUC-SP}
}
```

---

## 🤝 Suporte / Support

- **Issues**: [GitHub Issues](https://github.com/masterrey/ShaderLabStudio/issues)
- **Discussões**: [GitHub Discussions](https://github.com/masterrey/ShaderLabStudio/discussions)

---

**Última atualização / Last updated**: Junho de 2026  
**Versão / Version**: Consulte a badge de Latest Release no topo / see the Latest Release badge at the top  
**Status / Status**: Desenvolvimento Acadêmico Ativo / Active Academic Development

**Addendo / Addendum**: Este projeto contou com o uso de assistência de IA no processo de desenvolvimento e documentação, algo coerente com o contexto de 2026.  
**Addendum**: This project was developed with the help of AI assistance during development and documentation, which is consistent with the 2026 context.

### Autosave e edição pelo Visual Studio

O Studio salva automaticamente a sessão e os shaders associados a arquivos a cada **10 minutos**, inclusive com a prévia de vídeo indisponível. Shaders ainda sem nome ficam na sessão de recuperação em `%LOCALAPPDATA%/GLSLShaderLab/session.json`; use **Save** para escolher um arquivo.

1. Abra um `.frag` no Studio (o `.vert` de mesmo nome também será acompanhado, se existir). É possível abrir um `.vert` separadamente pela aba Vertex.
2. Abra os mesmos arquivos no Visual Studio ou outro editor.
3. Salve no editor externo. O Studio verifica os arquivos a cada segundo e aguarda duas leituras iguais antes de atualizar o código e compilar — normalmente cerca de dois segundos. Isso funciona mesmo com **Auto** desligado; esse controle continua valendo para digitação dentro do Studio.

Se houver alterações pendentes no Studio e no disco para o mesmo shader, o painel informa um conflito e não sobrescreve nenhuma versão. Use **Save As** para guardar a edição local em outro arquivo, ou **Open** para carregar a versão do disco. O autosave também verifica mudanças externas antes de escrever. Arquivos excluídos, bloqueados ou sem permissão geram aviso; a monitoração continua tentando ler. Ao reiniciar, abra os arquivos novamente para retomar a monitoração.
