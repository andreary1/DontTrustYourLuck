# Melhoria gráfica — F1: base técnica

**Data:** 09/11/2026
**Tarefa:** `docs/PROMPT_MELHORIA_GRAFICA.md`, fase **F1 — Base técnica** (§9). Aprovada com "Sim, aplicar F1", incluindo o canvas Web 1280×720.
**Branch:** `main`. **Nenhum commit**: as alterações estão no working tree (§3).
**Resultado:** base técnica aplicada e travada por testes de contrato. Critério de saída da F1 cumprido:
- capturas antes/depois em 1280×720;
- build Web sem regressão: build OK, partida completa e reinício no navegador, console sem erros.

## Como ler as evidências

| Marca | Significado |
|---|---|
| [arquivo] | Valor lido do repositório: `.meta`, `.mat`, `.asset` ou código. |
| [captura] | PNG do Game view do Editor em Play mode, nível de qualidade `Mobile` (o da Web). Estado fixo: seed 2026, foto `mid` após 16 turnos. As pastas ficam em `Logs/GfxCaptures/`, ignorada pelo Git. |
| [medição] | Script sobre as capturas. Mede a **imagem**, não desempenho. Métricas abaixo. |
| [stats] | `UnityStats` do Game view do Editor. **Não são números da Web.** |
| [build Web] | Build `Web - Desktop - Development` servida em `localhost:8080` e aberta no painel de navegador do app Claude (Chromium). |

Métricas de [medição]:
- **Nitidez:** gradiente médio de luminância no retângulo do tabuleiro (21–79% x, 35–72% y). As fileiras do fundo usam o retângulo 30–75% x, 35–48% y.
- **Diferença:** % de pixels com |ΔR|+|ΔG|+|ΔB| > 48. Entre jobs diferentes há um ruído de cerca de 0,3% (animação idle). Por isso MSAA, sombras e filtros foram decididos **olhando as folhas de comparação**, não pelo número.
- **HSV:**
  - calçamento do fundo: x 250–370, y 92–140;
  - areia do tabuleiro: x 330–930, y 503–530.

Capturas usadas:

| Pasta | Jobs |
|---|---|
| `F1_pipeline_20261109-120714` | A0, A1, A2 (antes); C0 (depois, MSAA 4× e casas com mips); C1–C8 (A/B); C9 (1920×1080). Folhas: `cmp_msaa.png`, `cmp_shadows.png`, `cmp_chars.png`, `cmp_tiles_backrow.png` |
| `F1_verify_20261109-122953` | D0 = estado final da F1 (MSAA 2×, casas sem mips). Também `tests.txt` e `webbuild_20261109-124942.txt` |

As fotos `menu` e `start` do C0 saíram contaminadas pelo placeholder ciano da compilação assíncrona de shaders (38,5% e 41,7% dos pixels). A foto `mid` do C0 tem 0,05%. O runner agora espera `ShaderUtil.anythingCompiling` e alerta acima de 1%. No D0:
- `start` e `mid`: 0%;
- `menu`: 0,15%, falso positivo da borda das casas, já presente nas capturas "antes".

---

## 1. Diagnóstico: status depois da F1

Itens da tabela da F0 (`GFX-F0-diagnostico.md` §2) tocados pela F1, mais achados novos.

| # | Problema | O que a F1 fez | Evidência | Status |
|---|---|---|---|---|
| 1 | Resolução efetiva da Web baixa | Canvas 960×600 → **1280×720** (Player Settings e os 4 perfis Web). Render Scale 0,8 → **1,0** | [build Web] canvas 1280×720 e `devicePixelRatio` 1, ou seja, buffer de 1280×720. [medição] nitidez do tabuleiro 17,86 (A1) → **24,73** (D0), +38%; fileiras do fundo 18,54 → **30,01**, +62% | **corrigido** |
| 2 | O Editor não mostra o que a Web mostra | Nada a corrigir em Play mode | [stats] em Play mode o Editor usa `Mobile` / `Mobile_RPAsset` (padrão do WebGL), mesmo com `PC` selecionado. A1 × A2 diferem em 0,88% dos pixels | **refutado** para Play mode. Fora do Play, Scene/Game view seguem em `PC` |
| 3 | Sem antialiasing | **MSAA 2×** no `Mobile_RPAsset`. A URP sincroniza `QualitySettings` AA = 2 | [captura] C2 (sem MSAA) mostra degraus nas molduras das casas; C1 (2×) os remove; 4× (C0) quase não muda (`cmp_msaa.png`). O prompt §6 pede MSAA 2× ou FXAA | **corrigido**; custo de GPU na Web **não medido**. Muda os 4× que eu tinha proposto na F0 |
| 4 | Especular cinza nos 33 materiais Simple Lit | Os 33 `.mat` passam a usar `_SpecularHighlights` = 1 (NoSpecular), sem `_SPECULAR_COLOR`. Gerador corrigido (`Sprint4Art.DisableSpecular`) | [arquivo] + teste `SimpleLitMaterials_HaveNoSpecular`. [medição] calçamento S 0,426 → 0,437, V 0,859 → 0,819 (pixels estourados 15,5% → 10,6%); areia S 0,298 → 0,330 | **corrigido** (Simple Lit) |
| 4b | **Novo:** `M_PlayerPiece` (URP Lit) com especular e reflexos ligados | Não alterado (fora da F1) | [arquivo] `_SpecularHighlights: 1`, `_EnvironmentReflections: 1`, smoothness 0,15. **Correção da F0**, que dizia "especular e reflexos desligados" nos 7 Lit: só 6 estão | **proposto** junto com #13 |
| 6 | Sombras duras na Web | Soft shadows qualidade **Low**, mapa **1024**, 1 cascata, distância 50 → **35 m**, bias 1/1 → **0,5/0,4** | [captura] 2048 (C3) e Medium (C4) sem diferença visível (`cmp_shadows.png`); C3 × C4 = 0,31%, nível do ruído. Sem acne nas capturas | **corrigido** (valores). Peter-panning: sem análise dedicada com zoom. Elevação do sol (38°): não alterada, fica para a F4 |
| 7 | Personagens minificados sem mipmaps | Mantido Point sem mips (prompt §5.2). Compressão trocada (#8) | [captura] C5 (Bilinear + mips) e C6 (Point + mips): a margem creme do recorte fica contínua em vez de pontilhada (`cmp_chars.png`) | **proposto para a F3**, com a arte 256×320. Conflita com §5.2: **decisão sua** |
| 8 | Compressão DXT em pixel art e no céu | `SPR_Char_*` (4) e `T_Env_SkyBackdrop` sem compressão. Regra do gerador: até 256 px, sem compressão | [arquivo] + testes `CharacterCutouts_…` e `GeneratedTexturesUpTo256Px_AreUncompressed` | **corrigido** (config). Artefato DXT na Web antes/depois: **não comparado** |
| 10 | A peça cobre o número | Nada (escopo da F2) | [captura D0] duas peças na casa 16 cobrem o "1" | **confirmado em captura** (era hipótese) |
| 10b | **Novo:** os marcadores flutuantes cobrem o número da casa de trás | Nada (escopo da F2) | [captura D0] o sol sobre a casa 18 cobre o "9" do 29; o olho sobre a casa 3 corta o 12. [build Web] o sol sobre a 21 cobre o 26 | **confirmado**; viola o §10 ("nenhuma decoração cobre números") |
| 11 | Padrão das casas de trás borrado | `T_Tile_*` com Point e **sem mipmaps** (padrão do §4.5) | [captura] com mips (C0), as fileiras de trás perdem o padrão. Nitidez das fileiras do fundo: C0 28,40; **C7 sem mips 30,64**; C8 Bilinear + aniso 4 24,96 (`cmp_tiles_backrow.png`) | **corrigido** |
| 12 | Draw calls acima da meta (< 150) | Nada (F6) | [stats, Editor] A1 390 → D0 **388** draw calls, 31 SetPass, 304 renderers, 48 materiais, 5 shaders, 40,5k tris. Shadow casters 150 → 148 (distância 35 m) | **confirmado acima da meta** no Editor; Web não medido |
| 14 | HDR sem uso | HDR desligado no `Mobile_RPAsset` | [arquivo] + teste | **corrigido**; ganho não medido |
| 23 | **Novo:** areia do tabuleiro estourada | Nada (F4) | [medição] V = 1,0 em 100% da região da areia, **antes e depois** | **confirmado** (já existia) |
| 24 | **Novo:** o título do HUD quebra em duas linhas no Editor | Nada (F5) | [captura] em 1280×720, "Luck!" invade "Fortaleza", **idêntico em A1 e D0**. [build Web] em 1280×720 o título coube em uma linha | **confirmado** no Editor; diferença Editor × Web na fonte |

Os itens 5, 9, 13 e 15–22 não mudaram. Seguem como na F0.

---

## 2. Decisões tomadas por captura

| Item | Opções testadas | Decisão | Por quê |
|---|---|---|---|
| Canvas e escala | 960×600 / 0,8 (A0); 1280×720 / 0,8 (A1); 1280×720 / 1,0 (C/D) | 1280×720 a 1,0 | Referência do projeto; nitidez +38% |
| MSAA | 1× (C2), 2× (C1), 4× (C0) | **2×** | 1× tem degraus nas molduras; 4× quase igual a 2× e mais caro |
| Sombra | 1024 Low; 2048 Low (C3); 1024 Medium (C4) | 1024 Low, 35 m, bias 0,5/0,4 | Sem diferença visível; o mais barato |
| Casas | Point + mips (C0); Point sem mips (C7); Bilinear + mips + aniso 4 (C8) | Point sem mips | Câmera fixa; padrão do §4.5; mais nítido no fundo |
| Personagens | Point sem mips (base); Bilinear + mips (C5); Point + mips (C6) | Point sem mips **por ora** | §5.2. Os mips melhoram a margem creme: reavaliar na F3 com a arte final |
| Especular | Simple Lit com especular (A) × sem (C/D) | Sem especular | Corrige o bug do gerador; fundo menos lavado |

---

## 3. Arquivos alterados

**Código de runtime**
- `Assets/_Game/Scripts/Presentation/Art/GamePalette.cs` (novo, com `.meta` e `Art.meta`): fonte única dos tokens de cor. Base da GDD §6.3 + tons derivados, **mesmos valores** de antes.
- `Assets/_Game/Scripts/Presentation/UI/HudTypes.cs`: o `HudPalette` lê o `GamePalette` (mesmos valores).
- `Assets/_Game/Scripts/Presentation/Match/MatchDirector.cs`: cores dos participantes vindas do `GamePalette` (mesmos valores).

**Código de Editor**
- `Assets/_Game/Scripts/Editor/Sprint4/Sprint4Art.cs`:
  - tokens de paleta;
  - `DisableSpecular` correto para Simple Lit;
  - import: até 256 px sem compressão; `T_Tile_*` sem mipmaps (`BoardTilePrefix`).
- `Assets/_Game/Scripts/Editor/Sprint4/Sprint4Scene.cs` e `Assets/_Game/Scripts/Editor/Sprint3CardsBuilder.cs`: tokens de paleta (mesmos valores).
- `Assets/_Game/Scripts/Editor/Graphics/GfxCaptureRunner.cs` (novo): captura em Play mode.
  - seed fixa; fotos menu/start/mid; recortes de peças e casas; `UnityStats`;
  - espera a compilação de shaders e detecta o placeholder ciano;
  - trava de sessão contra domain reload.
- `Assets/_Game/Scripts/Editor/Graphics/GfxF1.cs` (novo): aplicação idempotente da F1 e jobs A/B com restauração.
  - Menus `Tools > Don't Trust Your Luck > Graphics > F1 - Run Pipeline` / `F1 - Verify`.
  - Escreve o canvas Web também no `ProjectSettings.asset` global: a API grava só no perfil ativo.
- `Assets/_Game/Scripts/Editor/Graphics/GfxVerification.cs` (novo):
  - cadeia de testes EditMode → PlayMode com resumo;
  - build Web com o perfil `Web - Desktop - Development` e relatório (a regex do canvas aceita aspas, §5).

**Testes (EditMode)**
- `Assets/_Game/Tests/EditMode/Presentation/GamePaletteTests.cs` (novo, 5 testes): valores da GDD, HUD usando as cores-base, contraste WCAG AA do texto do HUD, texto grande do botão primário, auxiliar WCAG.
- `Assets/_Game/Tests/EditMode/Presentation/GraphicsBaseTests.cs` (novo, 7 testes de contrato):
  - `Mobile_RPAsset` da F1;
  - nível padrão do WebGL usa esse asset;
  - Simple Lit sem especular;
  - import dos personagens e das casas;
  - texturas ≤ 256 px sem compressão;
  - canvas 1280×720 em Player Settings e nos perfis Web.

**Assets e configuração**
- `Assets/Settings/Mobile_RPAsset.asset`:
  - Render Scale 0,8 → 1; MSAA 1 → 2; HDR desligado;
  - soft shadows ligado (Low);
  - distância 50 → 35; bias 1/1 → 0,5/0,4.
- `ProjectSettings/QualitySettings.asset`: `antiAliasing` 0 → 2 no nível Mobile (sincronizado pela URP).
- `ProjectSettings/ProjectSettings.asset`: só `defaultScreenWidthWeb`/`HeightWeb`, 960×600 → 1280×720.
- `Assets/Settings/Build Profiles/Web - {Desktop,Mobile} - {Development,Release}.asset`: override do canvas, 960×600 → 1280×720.
- 33 `Assets/_Game/Art/Materials/M_*.mat` (Simple Lit): `_SpecularHighlights` 0 → 1 e keyword `_SPECULAR_COLOR` removida.
- `Assets/_Game/Art/Characters/SPR_Char_*.png.meta` (4) e `Assets/_Game/Art/Environment/T_Env_SkyBackdrop.png.meta`: compressão Normal → Sem compressão.
- `Assets/_Game/Art/Environment/T_Tile_*.png.meta` (6): mipmaps desligados.

**Ferramentas e documentação**
- `.claude/launch.json`: o servidor da build passa a usar `--protocol HTTP/1.1` (§5).
- `docs/qa/GFX-F1-base-tecnica.md`: este relatório.
- `docs/qa/GFX-F0-diagnostico.md`: só uma nota no topo apontando para este status.

Arquivos do usuário não tocados: `.vscode/extensions.json`, `.vscode/launch.json` e `docs/PROMPT_MELHORIA_GRAFICA.md` (não rastreados).

---

## 4. Implementado × proposto × não testado × bloqueado

**Implementado (visual)**
- Web em 1280×720 a escala 1,0: tabuleiro e números mais nítidos (+38% de gradiente; +62% nas fileiras do fundo).
- MSAA 2×: molduras das casas sem degraus.
- Sombras suaves (Low, 1024, 35 m).
- Materiais Simple Lit foscos: fundo um pouco mais saturado e menos estourado.
- Pixel art dos personagens e do céu sem compressão.
- Casas com Point sem mipmaps: o padrão das fileiras de trás aparece.
- Paleta centralizada (`GamePalette`), sem mudança visível.
- Ferramenta de captura A/B e verificação (testes + build), reutilizável nas próximas fases.

**Proposto (decisão ou fase seguinte)**

| Proposta | Fase | Observação |
|---|---|---|
| Mipmaps nos personagens (Point ou Bilinear + preservação de cobertura alfa), avaliados com a arte 256×320 | F3 | Conflita com o §5.2: **decisão sua** |
| Desligar especular e reflexos do `M_PlayerPiece` e unificar os Lit em Simple Lit (#4b, #13) | F2/F3 | |
| Marcadores sol/olho e peças sem cobrir números (#10, #10b) | F2 | Requisito do checklist §10 |
| Areia do tabuleiro estourada (#23): albedo/luz | F4 | |
| Draw calls 388 → meta < 150 (#12) | F6 | Combinar malhas estáticas, static batching na Web |
| Título do HUD quebrando no Editor (#24) | F5 | |
| Amostra de cor do vencedor como quadrado liso | F5 | Opcional: retrato do animal |

**Não testado**
- Custo de GPU/FPS na Web de MSAA 2× e das sombras suaves. **Nenhuma medição de desempenho foi feita.**
- Firefox/Safari, janela menor que 1280×720 e telas de alta densidade (com DPR 2 o buffer dobra em cada eixo).
- Build com o perfil **Release** e com compressão (Brotli/Gzip). Só o Development foi gerado.
- Compilação **na Unity** da última edição (regex em `GfxVerification.cs`). Feitos: compilação offline e teste da regex no `index.html` real (§5).
- Daltonismo e contraste nas capturas.
- Análise detalhada da captura C9 (1920×1080). Ela foi gerada, mas não analisada.

**Bloqueado**
- Imagens de referência 1–4 (#22): continuam ausentes.
- Unity pela linha de comando (licenciamento): capturas, testes e build dependem de você rodar os menus no Editor.

---

## 5. Verificação realizada

| O quê | Como | Resultado |
|---|---|---|
| Capturas antes/depois e A/B | Você rodou `F1 - Run Pipeline` e `F1 - Verify` no Editor (Unity 6000.6.8f1, Direct3D11) | 13 jobs (A0–A2, C0–C9) + D0. Todos em 1280×720, exceto A0 (960×600) e C9 (1920×1080). Contaminação de shader resolvida (D0 limpo) |
| Testes Unity | `F1 - Verify` (Test Runner API) | **EditMode 181/181, PlayMode 19/19** (`tests.txt`) |
| Build Web | `F1 - Verify` → `GfxWebBuild` | **Succeeded**, 0 erros, 0 avisos. 63.308.910 bytes: `Web.wasm` 50,1 MB, `Web.data` 12,4 MB, `Web.framework.js` 0,7 MB. 19 min 12 s, sem compressão. Não comparável com a S4 (60.979.725 bytes): o menu principal entrou depois |
| Canvas no `index.html` | O relatório do build disse "not found": a regex não aceitava aspas. Corrigida e testada (expressão equivalente em Python) no `index.html` gerado | `width="1280" height="720"` → **1280x720** |
| Compilação offline | Roslyn da Unity: Runtime, Runtime.Player, Editor, Tests.EditMode.Presentation | 4 assemblies, **0 erros**. Não substitui a compilação na Unity |
| Teste de fumaça no navegador | Servidor `python -m http.server 8080 --bind 127.0.0.1 --protocol HTTP/1.1 --directory Builds/Web`; viewport 1280×800, DPR 1 | Ver abaixo |

Teste de fumaça no navegador:
- os 4 arquivos do build com 200 OK;
- menu → **Play**;
- 5 rodadas com Onça (humana) e 3 bots: Roll Dice, Choose A/B, a prévia do rebote (17 + 6 → 19) e a carta **Lucky Roll** (rerrolagem);
- fim de partida: "**Capivara wins!** Landed exactly on the lucky number 21.";
- **Play Again**: nova partida (sorte 24, azar 14) e nova mão;
- console com **0 erros**. Há 1 aviso esperado (shader FSR removido do build, como na S0) e logs informativos de áudio (como na S4).

Problema do servidor resolvido no caminho:
- O `http.server` do Python 3.15 em HTTP/1.0 truncava respostas grandes nesta máquina. Sintomas: `ERR_CONNECTION_RESET` e falha ao baixar `Web.data`/`Web.wasm`.
- Reproduzido com `curl` em duas portas, então a porta não era a causa.
- Com `--protocol HTTP/1.1`, 12 de 12 downloads completaram.
- Os relatórios S0/S2 citam o comando antigo; use o novo.

Não executado no navegador:
- eliminação pelo número do azar;
- botão **Main Menu** do resultado;
- telas How to Play, Options e Credits;
- redimensionar a janela.

Passos para verificar o que não rodou:
1. **Regex do canvas compilada na Unity:** abra o Editor e rode `Tools > Don't Trust Your Luck > Graphics > Build Web (Development profile)`. No `Logs/GfxCaptures/webbuild_*.txt`, deve aparecer `index.html canvas: 1280x720`.
2. **Custo do MSAA na Web:**
   1. Sirva `Builds/Web` com o comando acima e abra no Chrome.
   2. DevTools → Performance: grave 10 s na partida.
   3. Repita com um build de `Mobile_RPAsset` em MSAA 1× e compare o tempo de GPU por quadro.
3. **Release:** gere com o perfil `Web - Desktop - Release` e confirme que o canvas segue 1280×720 e que o console fica limpo.

---

## 6. Limitações e próximo passo

**Limitações**
- As capturas são do Editor (Direct3D11), não da Web. A Web foi conferida só por inspeção nas telas do teste de fumaça.
- As métricas de imagem são indicadores. Gradiente maior também pode vir de serrilhado (sem MSAA a nitidez é 23,39; com 2×, 23,09).
- Nenhum número de desempenho foi medido.

**Próximo passo sugerido: F2 — Tabuleiro**
- numeração integrada às casas, sem peças nem marcadores sobre os números (#10, #10b);
- casas Sorte/Azar com ícone;
- unificar o shader das casas (#13) e o especular do `M_PlayerPiece` (#4b);
- critério de saída: checklist §10 (tabuleiro) aprovado em captura com o `GfxCaptureRunner`.
