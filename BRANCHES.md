# Estado das branches — 10/09/2026

Comparação após `git fetch --all`, usando `main` em `ff2ecf7` como referência.
“Faltam” conta commits da main ausentes na branch; “exclusivos” conta commits da branch ausentes na main.

| Branch remota | Faltam | Exclusivos | Situação |
| --- | ---: | ---: | --- |
| main | 0 | 0 | Base mais completa do Studio WPF |
| 3DModel | 53 | 0 | Histórico já incorporado; a branch local também está 13 commits atrás da remota |
| BaseVSCode | 62 | 0 | Histórico já incorporado |
| copilot/add-save-open-vert-functionality | 24 | 0 | Abrir/salvar vertex e pastas iniciais já incorporados |
| copilot/explore-codebase-and-create-plan | 37 | 0 | Inclui saída de fullscreen com ESC; já incorporado |
| copilot/explore-codebase-and-plan | 46 | 1 | Sistema de aulas e comparação no aplicativo legado |
| copilot/fix-release-permission-error | 26 | 0 | Permissão do workflow de releases já incorporada |
| copilot/increase-font-size-glsl | 21 | 0 | Zoom e cores GLSL já incorporados |
| feature/ui-theme-improvements | 12 | 0 | Temas já incorporados, com problemas corrigidos nesta alteração |

O commit exclusivo `d04485b` altera `Window.cs`, `ShaderSelector.cs`, `ModelSelector.cs`, `Program.cs` e documentação. Implementa dois pipelines de aulas, metadados e comparação no aplicativo legado da raiz, não no Studio WPF de `src/`. Recomendação: continuar na base main e portar separadamente as funcionalidades didáticas desejadas. Trocar diretamente para essa branch perde as melhorias posteriores do Studio.

## Correções locais

Branch `codex/classroom-diagnostics`, criada a partir da main. Inclui reconhecimento de linhas de diagnóstico em formatos de diferentes drivers, navegação para o primeiro erro, espaçamento consistente do editor e numeração, contraste dos controles e tratamento de exceções nas operações de vídeo. O log original é preservado.

Falhas capturáveis de vídeo interrompem a prévia e deixam a edição/salvamento disponíveis. Para tentar novamente, salve e reinicie. Falhas nativas fatais ou travamentos do driver exigem isolamento do renderizador em outro processo para uma proteção maior.

## Verificação

- `dotnet build GLSLShaderLab.Studio.slnx`
- `dotnet run --project tests/GLSLShaderLab.Regression`
- Verificação manual em laboratório: alternar temas, editar linhas com Enter, usar Ctrl+roda, provocar erros nos shaders vertex/fragment e usar “Ir para o erro”. Confirmar que a prévia mantém o último shader válido após erro de compilação.
- Os testes automatizados simulam uma exceção de vídeo; não simulam uma queda real do driver/GPU.
