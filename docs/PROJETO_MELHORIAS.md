# Projeto de Melhorias — ShaderLabStudio

Este documento organiza as melhorias sugeridas para execução em tarefas futuras, com foco em baixo risco e entregas incrementais.

## Objetivo

Evoluir a qualidade do código, testabilidade e confiabilidade do Studio sem interromper o fluxo atual de uso em aula/laboratório.

## Escopo inicial (priorizado)

### Fase 1 — Estrutura e manutenibilidade
- [ ] Extrair responsabilidades de `MainWindow.xaml.cs` em serviços/componentes menores.
- [ ] Criar camadas claras para:
  - [ ] Coordenação de renderização
  - [ ] Sincronização de arquivos
  - [ ] Estado da sessão/UI
- [ ] Reduzir acoplamento entre eventos de UI e regras de negócio.

**Critério de aceite**
- `MainWindow.xaml.cs` com menor complexidade e sem regressão funcional.

---

### Fase 2 — Testes automatizados
- [ ] Migrar `tests/GLSLShaderLab.Regression/Program.cs` para projeto formal de testes (`xUnit` ou `NUnit`).
- [ ] Cobrir cenários críticos já existentes:
  - [ ] Parser de diagnósticos GLSL
  - [ ] Sincronização de arquivos (`ShaderFileSync`)
  - [ ] Fluxo básico de undo/redo de editor
- [ ] Integrar execução de testes no pipeline CI.

**Critério de aceite**
- Testes executando via `dotnet test` no ambiente local e CI.

---

### Fase 3 — Dependências e estabilidade
- [ ] Padronizar versões do ecossistema OpenTK entre projetos (`legacy` e `Studio`).
- [ ] Revisar cópia de dependências nativas (Assimp) para reduzir duplicidade.
- [ ] Validar build e execução no ambiente-alvo (Windows + OpenGL 4.5+).

**Critério de aceite**
- Dependências coerentes entre projetos e execução sem regressão.

---

### Fase 4 — Observabilidade e diagnósticos
- [ ] Estruturar mensagens de erro (OpenGL, vídeo/ffmpeg, webcam) com contexto técnico.
- [ ] Melhorar mensagens para usuário final com ação recomendada.
- [ ] Adicionar métricas simples no diagnóstico (tempo de compilação, FPS médio).

**Critério de aceite**
- Falhas reproduzíveis com diagnóstico claro para suporte e sala de aula.

---

### Fase 5 — Evoluções opcionais
- [ ] Substituir polling por `FileSystemWatcher` com fallback para polling.
- [ ] Otimizar syntax highlighting para arquivos grandes.
- [ ] Criar modo de render fallback para hardware limitado.

## Roteiro sugerido de execução

1. Fase 1
2. Fase 2
3. Fase 3
4. Fase 4
5. Fase 5

## Observações

- Priorizar mudanças pequenas por PR/tarefa.
- Sempre validar com build e testes antes de avançar de fase.
- Evitar mudanças de arquitetura e features novas no mesmo pacote.
