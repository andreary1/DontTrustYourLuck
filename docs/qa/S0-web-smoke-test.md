# S0 — Web Smoke Test (baseline)

**Data:** 07/11/2026
**Sprint:** 0 (S0-05 / S0-06)
**Resultado geral:** ✅ **M0 aprovado (local)** — build Web gerado e carregado via HTTP local sem erros; clique manual do usuário confirmado (todos os cliques contados). Pendentes, fora do M0: navegadores-alvo adicionais, canvas responsivo e teste no itch.io.

## Ambiente

| Item | Valor |
|---|---|
| Unity | `6000.6.8f1` |
| Build Profile | `Assets/Settings/Build Profiles/Web - Desktop - Development.asset` |
| Target | WebGL (Development) |
| Graphics API | OpenGLES3 (WebGL 2) — contexto criado: `WebGL 2.0 (OpenGL ES 3.0 Chromium)` |
| Compressão | `Disabled` (definida pelo perfil) |
| Cena 0 | `Assets/_Game/Scenes/Bootstrap.unity` |
| Saída | `Builds/Web/` (ignorada pelo Git) |
| Disparo do build | `BuildPipeline.BuildPlayer(BuildPlayerWithProfileOptions)` via Unity MCP |
| Servidor | `python -m http.server 8080 --bind 127.0.0.1 --directory Builds/Web` (`.claude/launch.json`, config `web-build`) |
| Navegador | Navegador embutido do app Claude (Chromium; versão não registrada) |

## Histórico de builds

| # | Resultado | Duração | Tamanho total | Observação |
|---|---|---|---|---|
| 1 | ❌ Falhou | — | — | Linkagem `wasm-ld`: símbolos `_embind_*`/`_emval_*` indefinidos |
| 2 | ✅ Succeeded (0 erros, 0 avisos) | 1min15s | 56.621.235 bytes (sem compressão, Development) | Após remover `com.unity.ai.inference` |

**Causa da falha #1:** `com.unity.dt.app-ui` 1.3.6 (dependência exclusiva de `com.unity.ai.inference` 2.6.1) inclui `Runtime/Core/Platform/WebGL/Plugins/libAppUITextMateLib.a`, habilitado para WebGL, que depende de *embind* do Emscripten. **Correção aplicada (autorizada pelo usuário):** remoção de `com.unity.ai.inference` do `Packages/manifest.json`; `app-ui` e shaders Sentis saíram do build.

## Checklist M0

| # | Critério | Resultado | Evidência |
|---|---|---|---|
| 1 | Projeto compila sem erros no Editor | ✅ Passou | Console do Editor: 0 erros / 0 avisos após remoção do pacote |
| 2 | Build Web Development conclui sem erros | ✅ Passou | Build #2: `Succeeded`, 0 erros |
| 3 | Página abre via `http://localhost` | ✅ Passou | `http://localhost:8080/` carregou a cena Bootstrap (título + botão) |
| 4a | Botão responde ao clique | ✅ Passou | **Teste manual do usuário (07/11/2026): todos os cliques com mouse real foram contados.** Sob automação, 7 tentativas geraram 3 registros (ver "Observações") |
| 4b | Console do navegador sem erro fatal | ✅ Passou | Filtro de erros: nenhum. 1 aviso de shader URP (abaixo) |
| 5 | UI utilizável em dois tamanhos | ⚠️ Parcial | Viewport 1280×720: OK. Viewport 800×600: UI visível e legível, mas o canvas fixo 960×600 do template `Default` transborda horizontalmente (barra de rolagem) |
| 6 | Relatório discrimina executado/não executado | ✅ Este documento | — |
| 7 | Nenhum commit sem autorização | ✅ | Nenhum commit criado |

**Não executado:** Firefox, Edge, Chrome standalone (navegador do teste manual não registrado); fullscreen; teste no itch.io; medição de FPS, memória e tempo de carregamento (não medidos — nenhum valor estimado).

## Observações

1. **Cliques não registrados sob automação.** Dois cliques automatizados rápidos geraram 1 registro; cliques espaçados de 1 s geraram registros intermitentes; eventos sintéticos via JavaScript tiveram resultado inconclusivo. Hipótese (não confirmada): o clique automatizado pressiona e solta no mesmo frame e o `InputSystemUIInputModule` não detecta. **O teste manual com mouse real contou todos os cliques**, portanto tratado como artefato da automação. Implicação para QA futuro: testes automatizados de UI no navegador devem usar cliques com duração realista ou validação manual.
2. **Avisos de shader URP no console:** `GaussianDepthOfField`, `BokehDepthOfField`, `PaniniProjection` e `Edge Adaptive Spatial Upsampling` "not supported or has been stripped". Esperado: efeitos não usados; sem impacto funcional.
3. **Canvas fixo 960×600** (template `Default`, `defaultScreenWidthWeb/HeightWeb`). Diverge da referência 1280×720 e não é responsivo. Pendente de decisão (template responsivo ou ajuste de resolução).

## Efeitos colaterais do build (para revisão antes de commit)

- `Data/Plugins/lib_burst_generated.{cpp,wasm}` criado na **raiz do projeto** (saída do Burst para WebGL). Não versionar; considerar adicionar `/Data/` ao `.gitignore`.
- `Assets/Settings/Mobile_RPAsset.asset`, `UniversalRenderPipelineGlobalSettings.asset` e `ProjectSettings/GraphicsSettings.asset` atualizados automaticamente pela Unity (prefiltragem de variantes de shader e campos novos de serialização).
- `Packages/manifest.json` e `Packages/packages-lock.json` alterados pela remoção de `com.unity.ai.inference` (e transitivas `com.unity.dt.app-ui` etc.).
- Define `SENTIS_ANALYTICS_ENABLED` permanece em `ProjectSettings.asset` e no perfil Web; inofensivo sem o pacote, pode ser limpo depois.

## Como reproduzir

1. Unity: perfil ativo `Web - Desktop - Development`; *Build* para `Builds/Web`.
2. Servir: `python -m http.server 8080 --bind 127.0.0.1 --directory Builds/Web`.
3. Abrir `http://localhost:8080/` (nunca `file://`), DevTools aberto.
4. Clicar no botão várias vezes com o mouse; o rótulo deve incrementar a cada clique.
5. Redimensionar a janela; registrar navegador/versão e mensagens do console.
