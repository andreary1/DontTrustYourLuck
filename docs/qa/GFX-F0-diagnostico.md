# Melhoria gráfica — F0: diagnóstico

**Data:** 09/11/2026
**Tarefa:** `docs/PROMPT_MELHORIA_GRAFICA.md` (§2 e §11), fase **F0 — Diagnóstico**
**Branch:** `main`. Nenhum código, asset ou configuração foi alterado nesta fase.
**Resultado:** inventário completo e tabela de problemas preenchida com evidências de **arquivo** e de **cálculo**. **Nenhuma captura** foi feita (ver §6). Os efeitos visuais ficam como *hipótese* até as capturas da F1.

> **Status atualizado na F1:** veja `GFX-F1-base-tecnica.md` §1. Correção: `M_PlayerPiece` (URP Lit) tem especular e reflexos **ligados**; só os outros 6 materiais Lit estão desligados (§1.2 abaixo).

## Como ler as evidências

| Marca | Significado |
|---|---|
| [arquivo] | Valor lido do repositório: `.meta`, `.mat`, `.asset`, cena ou código. |
| [cálculo] | Resultado de script: contraste WCAG, simulação de daltonismo ou projeção da câmera. A projeção reproduz a matemática do `BoardCameraRig` com os valores do projeto. **Não é captura.** |
| [captura] | Imagem do Editor ou da build Web. **Nenhuma nesta fase.** |

**Status:** **confirmado** (valor ou código verificado), **hipótese** (efeito visual provável, falta captura), **não verificado** (não deu para checar), **refutado**, **bloqueado**.

---

## 1. Inventário

### 1.1 Texturas (`Assets/_Game/Art/`, 71 PNG)

Valores efetivos do bloco `DefaultTexturePlatform`. Nenhuma textura tem override para WebGL. O perfil Web usa subtarget DXT, então "Normal" vira BC1/BC3 no navegador.

| Grupo | Tamanho | Filtro | Mipmaps | Max Size | Compressão | Arquivos |
|---|---|---|---|---|---|---|
| Personagens (corpo) | 96×128 | Point | não | 128 | **Normal (DXT5 na Web)** | `SPR_Char_{Onca,Capivara,Macaco,Tucano}` |
| Retratos | 48×48 | Point | não | 64 | Sem compressão | `SPR_Portrait_*` (4) |
| Cartas | 64×64 | Point | não | 64 | Sem compressão | `Card_*` (4) |
| Casas do tabuleiro | 64×64 | Point | **sim** | 64 | Sem compressão | `T_Tile_{Even,Odd,Start,Lucky,Unlucky,Beyond}` |
| Fachadas e chão | 64×64 | Point | sim | 64 | Sem compressão | `T_Env_Facade_*` (6), `T_Env_Cobble` |
| Ambiente pequeno | 16–32 px | Point | sim | 32 | Sem compressão | 11 arquivos (`Awning`, `Bark`, `Bandeirinha`, `Grass`, `Leaves`, `LeavesDark`, `Roof`, `Sail`, `Sand`, `Stone`, `Wood`) |
| Céu/backdrop | 256×96 | Point | sim | 256 | **Normal (DXT1 na Web)** | `T_Env_SkyBackdrop` |
| UI (molduras e ícones) | 12–32 px | Point | não | 64 | Sem compressão | 30 arquivos em `UI/` e `UI/Menu/` |
| Logo | 1024×683 | Bilinear | não | 1024 | HQ | `UI/Branding/UI_Logo_Main` (arte enviada pelo usuário) |
| VFX | 16×16 | Point | não | 32 | Sem compressão | `T_VFX_{Dot,Square,Star}` |

Memória de textura estimada [cálculo]: cerca de 1,3 MB sem o logo. O logo ocupa 0,7 MB se for comprimido ou 2,8 MB se cair para RGBA32 (ver #20). **Memória de textura não é restrição**: há folga para deixar a pixel art sem compressão.

### 1.2 Materiais (`Assets/_Game/Art/Materials/`, 48)

| Shader | Qtde | Materiais | Observação |
|---|---|---|---|
| URP Simple Lit, opaco | 25 | `M_Env_*` (exceto `Sky`, `Flag_*`, `Sail`), `M_Char_Base`, `M_Tile_Beyond` | **especular ligado** (#4) |
| URP Simple Lit, alpha clip, 2 faces | 8 | `M_Char_Cutout`, `M_Env_Flag_0..5`, `M_Env_Sail` | especular ligado |
| URP Lit, opaco | 7 | `M_Tile_{Even,Odd,Start,Lucky,Unlucky}`, `M_Path_Connector`, `M_PlayerPiece` | especular e reflexos desligados; shader mais pesado que o resto (#13) |
| URP Unlit, opaco/alpha clip | 4 | `M_Env_Sky`, `M_FX_Icon{Sun,Eye,Arrow}` | |
| URP Unlit, transparente | 1 | `M_FX_ShieldAura` | |
| Particles/Unlit, transparente | 3 | `M_FX_{Confetti,Puff,Sparkle}` | |

Fora da pasta: material da fonte embutida (números do tabuleiro, `GUI/Text Shader`) e `UI/Default` (uGUI). Variações quase idênticas: `Flag_0..5` (mesma textura, muda a cor), `Fruit_0..3` (só cor), `Facade_0..5` (uma textura por cor).

### 1.3 O que é procedural e o que é arte final

- **Toda a arte é placeholder procedural** gerado por `Sprint4Art`, `Sprint4Scene` e `MainMenuBuilder` (Editor). A exceção é `UI_Logo_Main.png`, enviada pelo usuário.
- **Geometria:** primitivas da Unity (Cube, Cylinder, Sphere, Quad) mais uma malha procedural (`Mesh_RoofPrism`). Nenhum modelo importado.
- **Cena `GameBoard`:** 227 MeshRenderers no arquivo da cena e 37 instâncias de prefab (31 casas × 2 renderers; 4 peças × 4). Total de cerca de **300 renderers** [arquivo]. O cenário está marcado como `BatchingStatic`.
- **Fachadas:** a textura já pinta porta de madeira com bandeira em arco, duas janelas com venezianas e ornamentos de platibanda. A hipótese "caixas sem detalhe" (prompt §2.2) é **refutada no nível da textura**. A leitura na distância real não foi verificada.
- **Números das casas:** `TextMesh` com a fonte embutida (`LegacyRuntime.ttf`), negrito, cor `#1F1A14`, sem contorno.
- **HUD:** uGUI `Text` legado com fonte embutida; molduras 9-slice de 32×32 (PPU 50, borda de 8 px).

### 1.4 Pipeline de render: Editor ≠ Web

| Item | Editor (nível `PC`) | Build Web (nível `Mobile`) | Fonte |
|---|---|---|---|
| Nível de qualidade | `PC` (`m_CurrentQuality: 1`) | `Mobile` (`m_PerPlatformDefaultQuality` WebGL: 0) | `QualitySettings.asset` |
| RP Asset | `PC_RPAsset` | `Mobile_RPAsset` | idem |
| Render Scale | 1,0 | **0,8** | RP Assets |
| MSAA | desligado | **desligado** | RP Assets |
| HDR | sim | sim | RP Assets |
| Caminho de render | Forward+ | Forward | Renderers |
| Sombras | suaves, 4 cascatas, 2048, 50 m | **duras**, 1 cascata, 1024, 50 m, bias 1/1 | RP Assets |
| Depth/Opaque texture | sim/sim | não/não | RP Assets |
| Pós-processamento | desligado: câmera sem `UniversalAdditionalCameraData` e cena sem Volume | idem | `GameBoard.unity` |
| Canvas Web | — | **960×600 fixo** (template `Default`; repetido no override de Player Settings do perfil `Web - Desktop - Development`) | `ProjectSettings.asset` e Build Profile |
| Espaço de cor | Linear | Linear | `ProjectSettings.asset` |
| Static batching (Web) | — | WebGL não aparece em `m_BuildTargetBatching` (Standalone, Android e iOS estão desligados): **não verificado** | `ProjectSettings.asset` |
| SRP Batcher | ligado | ligado | RP Assets |

**Luz e ambiente (cena):** Directional `#FFD9A8`, intensidade 1,35, sombra Soft (força 0,55), rotação (38°, −38°), ou seja, elevação de 38°. Ambiente Trilight (céu `#B9D3E0`, equador `#E6C8A0`, chão `#7A6650`). Fog linear `#F2D3A6` de 26 a 60 m. Fundo da câmera sólido `#F2D3A6`. FOV 35°, pitch 52°.

---

## 2. Tabela de diagnóstico

Ordem: legibilidade → identidade → qualidade de imagem.

| # | Problema observado | Evidência | Causa provável | Correção proposta | Risco para WebGL | Status |
|---|---|---|---|---|---|---|
| 1 | **Resolução efetiva da Web baixa** | [arquivo] canvas 960×600 e Render Scale 0,8 → render interno ≈768×480. [cálculo] casa 45–52 px e algarismo 8–11 px no render target, contra 61–72 px e 14–19 px em 1280×720 a 1,0 | template `Default` + nível Mobile reaproveitado na Web | canvas 1280×720 (ou template responsivo) e Render Scale 1,0 | +2,5× pixels por quadro; medir | **confirmado** (valores); impacto visual: hipótese |
| 2 | **O Editor não mostra o que a Web mostra** | [arquivo] Editor em `PC` (escala 1,0, sombras suaves com 4 cascatas, Forward+); Web em `Mobile` | níveis de qualidade do template | Editor no nível da Web ao revisar arte; aprovação final sempre na build | nenhum | **confirmado** |
| 3 | **Sem antialiasing** | [arquivo] `Mobile_RPAsset` MSAA 1×; câmera sem dados URP (AA de pós = nenhum); `QualitySettings` AA 0 | padrão do template | MSAA 4× (ou 2×) no asset da Web; FXAA só se as bordas de alpha clip pedirem | MSAA no WebGL2 usa renderbuffer multiamostra e resolve; custo moderado, medir | **confirmado** (valor); serrilhado: hipótese |
| 4 | **Especular cinza ligado nos 33 materiais Simple Lit, inclusive no recorte dos personagens** | [arquivo] `_SpecularHighlights: 0`, keyword `_SPECULAR_COLOR`, `_SpecColor` cinza 0,5. [código URP] em `SimpleLitGUI.SpecularSource`, 0 = textura e cor (ligado) e 1 = sem especular | `Sprint4Art.ConfigureSurface` grava 0 achando que desliga. Isso vale para Lit, não para Simple Lit | gravar 1 (NoSpecular) para Simple Lit, no gerador e nos 33 `.mat` | tira um termo do shader: custo menor | **confirmado**; efeito (cena lavada e dessaturada): hipótese forte |
| 5 | **Fundo mais claro que a pista** | [cálculo HSV] fachadas com V 0,72–0,95 e S 0,20–0,68; faixas das casas com V 0,64–0,71 e S 0,67–0,76. O especular (#4) clareia ainda mais o cenário | paleta de fachadas sem camada de recuo | tokens de "fundo" (cerca de −30% de saturação e valor menor) para fachadas e backdrop | nenhum | **confirmado** (albedo); percepção: hipótese |
| 6 | **Sombras duras na Web e sol alto demais** | [arquivo] luz Soft, mas `Mobile_RPAsset` não suporta soft shadows, então ficam duras; 1 cascata 1024 em 50 m (≈0,05 u/texel); bias 1/1; elevação 38° (meta 25–35°) | defaults mobile | soft shadows (qualidade baixa), distância 35 m, bias por captura, elevação ≈30° | PCF suave custa pouco; medir | **confirmado** (valores); acne/peter-panning: não verificado |
| 7 | **Personagens minificados sem mipmaps** | [arquivo] 96×128, Point, sem mips. [cálculo] standee com ≈52–54 px de altura em 1280×720: razão texel 0,42 (0,17 com o alvo 256×320; 0,25 na cadeia Web atual) | sprite de mundo tratado como UI | A/B por captura: Point sem mips × Bilinear com mips e *Preserve Coverage* (alpha 0,5). **Conflita com o prompt §5.2** ("mipmaps desligados para sprite") no caso de sprite no mundo 3D | mips +33% de memória (KB) | hipótese |
| 8 | **Compressão DXT em pixel art e no gradiente do céu** | [arquivo] `SPR_Char_*` e `T_Env_SkyBackdrop` com compressão Normal; perfil Web com subtarget DXT | regra do gerador "comprime acima de 64 px" | sem compressão nesses 5 arquivos (≈0,2 MB) | memória extra desprezível | **confirmado** (config); artefato visível: hipótese |
| 9 | **Números das casas pequenos e sem contorno** | [código] `TextMesh` 64 pt × 0,065, Arial Bold, `#1F1A14`, sem contorno, ≈0,3 u de altura. [cálculo] 14–19 px em 1280×720 | placeholder da Sprint 1 | (F2) TextMesh Pro SDF, que já vem em `com.unity.ugui` 2.x (sem pacote novo), com contorno tinta; ou atlas de dígitos pixel art; ≈0,4 u | +1 shader (TMP SDF), suportado em WebGL | **confirmado** (implementação); legibilidade: não verificado |
| 10 | **A peça cobre parte do número da casa de trás** | [cálculo] a cabeça do standee na linha 2 cobre ≈31% da altura do algarismo da casa logo atrás (linha 3). Nas linhas 0→1 e 1→2 sobra uma folga de 2–8 px. A pose de vencedor cobre 95–100% | número na metade próxima da casa + standee de 0,95 u inclinado | (F2/F3) número na borda próxima ou num canto fixo; standee ≤ 0,85 u ou linhas mais espaçadas; decidir por captura | nenhum | hipótese (geometria) |
| 11 | **Padrão das casas de trás borrado** | [arquivo] `T_Tile_*` Point com mips. [cálculo] casas de trás com 61×44 px, encurtadas pela perspectiva | o mip é escolhido pelo eixo comprimido | A/B: sem mips × Bilinear com mips e aniso 4 | aniso no WebGL2 depende de extensão (ampla em desktop) | hipótese |
| 12 | **Draw calls provavelmente acima da meta (< 150)** | [arquivo] ≈300 renderers, 48 materiais, 9 variantes de shader; o passe de sombra repete os casters; static batching na Web sem configuração explícita | um renderer por primitiva | medir (Stats/Frame Debugger); depois combinar malhas estáticas por material no builder, usar MPB para bandeiras e frutas, desligar sombra de objetos de fundo | combinar malhas reduz chamadas WebGL (ganho) | não verificado (sem medição) |
| 13 | **Shaders misturados** | [arquivo] casas, conectores e anel da peça em URP **Lit**; o resto em Simple Lit | materiais da Sprint 1 atualizados no lugar mantêm o shader | unificar em Simple Lit sem especular | menos variantes e SetPass | **confirmado** |
| 14 | **HDR sem uso** | [arquivo] `Mobile_RPAsset` com HDR ligado; sem pós-processamento | default | HDR desligado no asset da Web | menos banda e memória | **confirmado** (valor); ganho: não medido |
| 15 | **HUD com fonte embutida e 9-slice em escala não inteira** | [código] `Text` legado + `LegacyRuntime.ttf`; molduras de 32 px Point, PPU 50; CanvasScaler 0,79 em 960×600 | placeholder | (F5) fonte OFL via TMP; molduras em 2× com Bilinear | TMP funciona em WebGL | **confirmado** (implementação); visual: hipótese |
| 16 | **Contraste: 2 pares abaixo de 4,5:1** | [cálculo WCAG] `InkFaded #7D6A5C` sobre `#F4E9CC` = 4,25:1; branco `#FBF6E9` sobre o botão `#D83A35` = 4,25:1 (passa o AA de texto grande, 3:1, mas falha a meta do projeto). Os pares principais ficam entre 7,2 e 10,9:1 | tons escolhidos a olho | (F5) escurecer `InkFaded` e o vermelho do botão, ou usar texto tinta | nenhum | **confirmado** |
| 17 | **A cor some no daltonismo, mas os ícones compensam** | [cálculo Machado 2009, ΔE CIE76] moldura verde × vermelha: 98 → 13 (protanopia); Macaco verde × Tucano rosa: 95 → 16 (deuteranopia); Sorte × Azar: 63 → 33 | — | manter sempre ícone/forma (já existe: sol/olho, animal + retrato) | nenhum | **confirmado** (cor); revisão em captura simulada: não executado |
| 18 | **Halo no alpha clip** | [arquivo] `alphaIsTransparency` 1 (dilatação de cor), Point, contorno externo `InkDeep` | — | conferir com zoom depois de mudar o filtro (#7) | — | hipótese (risco baixo) |
| 19 | **Fog quase sem efeito** | [cálculo] casas a ≈22 u da câmera; o fog começa em 26 u; só o céu e o fundo do chão recebem fog | ajuste da Sprint 4 | retirar, ou usar como ferramenta de recuo com tom mais escuro (F4) | retirar elimina as variantes de fog | **confirmado** (valores); impacto baixo |
| 20 | **O logo pode ficar sem compressão na Web** | [arquivo] 1024×683, HQ; 683 não é múltiplo de 4 | — | conferir o formato efetivo da Web no Inspector; se for RGBA32 (2,8 MB), ajustar para 1024×684 ou aceitar | memória | hipótese |
| 21 | **Documento fora de sincronia** | [código] `Sprint4Scene` já constrói as bandeirinhas; `ART_DIRECTION` §3.1 ainda as marca como "Proposto" | — | atualizar o documento quando a F4 mexer nelas | — | **confirmado** |
| 22 | **Referências visuais indisponíveis** | as imagens 1–4 (`*.webp`) não estão no repositório nem em Desktop/Downloads | — | usar só as descrições JSON do prompt; anexar as imagens se quiser comparação | — | **bloqueado** |

---

## 3. Tamanho na tela [cálculo]

Script que reproduz `BoardCameraRig.Frame()`: pitch 52°, FOV 35°, layout 8 × 4 com espaçamento 1,25 × 1,6, HUD dos builders (coluna esquerda 244 px, direita 268 px, topo 84 px e base 180 px na referência 1280×720, CanvasScaler com match 0,5). Standee de 0,95 u inclinado 18°. Algarismo com ≈0,3 u.

| Configuração | Casa (frente / fundo) | Algarismo | Standee na tela | Razão texel 128 px / 320 px |
|---|---|---|---|---|
| 1280×720, escala 1,0 (Editor/itch) | 72×62 / 61×44 px | 19 / 14 px | ≈51×54 px | 0,42 / 0,17 |
| 960×600, escala 0,8 (**build Web atual**) | 52×45 / 45×33 px | 14 / 10 px (≈11 / 8 px no render target) | ≈37×39 px | 0,25 / 0,10 |
| 1280×720, escala 0,8 | 72×62 / 61×44 px | 19 / 14 px | ≈51×54 px | 0,34 / 0,13 |
| 1920×1080, escala 1,0 | 108×94 / 91×66 px | 29 / 20 px | ≈77×81 px | 0,63 / 0,25 |

Distância da câmera ao centro do tabuleiro: 17,2 u (1280×720) e 19,5 u (960×600).

---

## 4. Contraste e daltonismo [cálculo]

Cores de albedo/UI do repositório (`Sprint4Art`, `HudPalette`, `BoardTileView`). A UI é overlay sem luz, então a razão vale para o pixel final. Para os números nas casas (casa iluminada) o valor é aproximado.

| Par | Razão | Meta | Resultado |
|---|---|---|---|
| Ink `#493329` / Cream `#E9D7AC` (regra do projeto) | 8,26:1 | 4,5:1 | OK |
| Ink / PaperLight `#F4E9CC` (papel, moldura Sorte) | 9,71:1 | 4,5:1 | OK |
| Ink / White `#FBF6E9` (moldura de carta) | 10,88:1 | 4,5:1 | OK |
| Ink / `#F3DCCD` (moldura Azar) | 8,91:1 | 4,5:1 | OK |
| RedDark `#8E2320` / PaperLight (título) | 7,20:1 | 4,5:1 | OK |
| LuckyDark `#8A5A00` / PaperLight | 4,90:1 | 4,5:1 | OK |
| Ink / CardDisabled `#BDB29A` | 5,59:1 | 4,5:1 | OK |
| InkFaded `#7D6A5C` / Card `#F4E9CC` | 4,25:1 | 4,5:1 | **falha** |
| White `#FBF6E9` / Danger `#D83A35` (botão Play) | 4,25:1 | 4,5:1 | **falha** (passa 3:1 de texto grande) |
| Número `#1F1A14` / centros das casas (normal, Sorte, Azar, além da Sorte) | 11,5–15,3:1 | 3:1 | OK |
| Número / faixa par Blue `#287FA3` | 3,83:1 | 3:1 | OK |
| Número / faixa ímpar Terracotta `#B5523B` | 3,48:1 | 3:1 | OK (margem curta) |
| Número / faixa Azar RedDark `#8E2320` | 1,98:1 | 3:1 | **falha** se o algarismo invadir a faixa |

| Par (ΔE CIE76) | Normal | Protanopia | Deuteranopia |
|---|---|---|---|
| Sorte `#F4B942` × Azar `#D83A35` | 63 | 54 | 33 |
| Onça `#F4B942` × Macaco `#438B52` | 67 | 44 | 53 |
| Onça × Tucano `#ED638B` | 78 | 77 | 56 |
| Macaco × Tucano | 95 | 35 | **16** |
| Capivara `#287FA3` × Macaco | 55 | 53 | 51 |
| Moldura Sorte verde × Azar vermelha | 98 | **13** | 25 |

---

## 5. Checklist §10 do prompt: estado na F0

| Item | Estado |
|---|---|
| 31 casas, 0–30, sem repetição e em ordem | **confirmado** por código (`BoardView.Build`) e pelo teste PlayMode da S4; captura: não executado |
| Números legíveis em 1280×720 e em janela menor | não verificado (cálculo: 14–19 px em 1280×720; 8–11 px na cadeia Web atual) |
| Sorte e Azar distinguíveis sem cor | **confirmado** por código (raios + sol flutuante; anéis + olho flutuante) |
| Nenhuma peça ou decoração cobre números | decoração: **confirmado** por código (≥ 1,8 u da borda); peças: **hipótese de violação** (#10) |
| Ocupação de 2, 3 e 4 peças legível | não verificado |
| Tabuleiro inteiro na área livre do HUD | **confirmado** por código (projeção off-center + `GetBoardViewport`); captura: não executado |
| Nenhuma decoração dentro do tabuleiro + 1 casa | **confirmado** por código |
| Sem elementos mexicanos ou andinos | **confirmado** no código dos geradores; visual: não executado |
| Fundo mais dessaturado que os jogáveis | **falha provável** (#5) |
| Luz quente, sombras suaves | parcial: luz quente confirmada; sombras duras na Web (#6) |
| Quatro animais distinguíveis | não verificado |
| Sem números no corpo; sem personagens novos | **confirmado** por código |
| Alpha clip sem halo; sorting | halo: hipótese de risco baixo; sorting: alpha clip com depth write, sem ordenação por design |
| Animações não alteram a posição lógica | **confirmado** pelo teste EditMode da S4 |
| P1 domina, P3 discreto | não verificado |
| Cartas não cobrem a pista | **confirmado** por código (safe area) |
| Estados de carta sem depender só de cor | parcial: fundo, texto e arte mudam juntos; ícone de cadeado não conferido |
| Contraste ≥ 4,5:1 | parcial: 2 pares falham (#16) |
| Draw calls, memória e partículas medidos | não executado |
| Build Web, console, Editor × Web, daltonismo em captura | não executado |
| Assets originais, sem obra copiada, sem aposta | **confirmado** (geradores procedurais + logo do usuário; sem dezenas nem valores) |

---

## 6. Limitações

- **Sem capturas.** Unity pela linha de comando falha no licenciamento nesta máquina; não há build Web local (`Builds/` ausente); não há ferramenta de captura no projeto. Toda conclusão visual está marcada como hipótese.
- **Imagens de referência ausentes.** Os arquivos 1–4 (`*.webp`) do prompt não foram encontrados. Usei apenas as descrições JSON.
- **Estimativas geométricas** assumem os tamanhos de HUD dos builders. O HUD real pode variar com o conteúdo.
- **Nenhum número de desempenho** foi medido nem estimado.

## 7. Próximo passo

F1 (base técnica) **proposta**, aguardando aprovação: ferramenta de captura e estatísticas no Editor; capturas "antes"; asset de qualidade da Web (Render Scale 1,0, MSAA, HDR desligado, sombras suaves a 35 m); canvas Web 1280×720 e Editor no nível da Web (configurações de projeto, exigem autorização); correção do especular; import de personagens e céu sem compressão, com A/B de filtro/mips; paleta centralizada com testes; capturas "depois" e build Web.
