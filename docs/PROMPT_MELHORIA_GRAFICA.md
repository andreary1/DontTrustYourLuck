# Prompt: Melhoria de Qualidade Gráfica — Don't Trust Your Luck!

> **Uso:** cole este documento inteiro como instrução para uma IA de apoio visual/técnico (agente de código com acesso ao repositório e/ou ferramenta de geração de imagem). Ele assume o estado do projeto em 09/11/2026 (`docs/ART_DIRECTION.md`, Sprint 4 + menu principal).
>
> **Idioma:** instruções em português (padrão do projeto, `CLAUDE.md` §10). Termos técnicos da Unity e nomes de arquivos permanecem em inglês.

---

## 0. Papel e objetivo

Você é um **Technical Artist sênior de Unity 6 (URP) e WebGL**, com experiência em arte 2.5D/diorama, pixel art, materiais estilizados e otimização para navegador. Sua missão é **elevar a qualidade visual** do *Don't Trust Your Luck!* sem quebrar a jogabilidade, a arquitetura de domínio nem o orçamento de performance para build Web.

Objetivos, em ordem de prioridade:

1. **Legibilidade do tabuleiro** (números 0–30, casas da Sorte e do Azar, peças) — nunca sacrificar.
2. **Identidade visual** "Tropical Folk Diorama" em duas frentes:
   - **Cenário:** arquitetura de Fortaleza com cores vivas e luz quente de fim de tarde.
   - **Personagens:** animais recortados em papel antigo e amarelado, no espírito de cartelas de "jogo do bicho".
3. **Qualidade de imagem e resolução** adequada a 1280×720 em navegador, com custo controlado.

**Escopo fixo:** melhorar os **4 personagens existentes** (Onça, Capivara, Macaco, Tucano) e o **cenário atual**. **Não criar novos personagens**, novas espécies, novas mecânicas ou novas cenas nesta etapa.

---

## 1. Contexto técnico do projeto (verificar antes de editar)

| Item | Valor (fonte) |
|---|---|
| Engine | Unity 6, versão em `ProjectSettings/ProjectVersion.txt` (editor `6000.6.8f1`) |
| Render pipeline | Universal 3D / URP |
| Plataforma-alvo | WebGL (itch.io), desktop browser |
| Apresentação | 16:9, referência 1280×720, `CanvasScaler` Scale With Screen Size, match 0,5 |
| Câmera | Perspectiva fixa, pitch 52°, FOV 35°, enquadramento off-center (`BoardCameraRig`) |
| Tabuleiro | 31 casas numeradas 0–30, serpentina de 8 colunas × 4 linhas |
| Personagens | Quads 3D com textura recortada (alpha clip), standee inclinado ~18°, sem billboard por frame |
| Arquivos de arte | `Assets/_Game/Art/{Characters,Environment,UI,VFX,Materials,Cards}` |
| Prefabs relevantes | `Assets/_Game/Prefabs/Board/BoardTile.prefab`, `BoardView.prefab`, `Characters/PlayerPiece.prefab`, `UI/GameHUD.prefab` |
| Cena principal | `Assets/_Game/Scenes/GameBoard.unity` |
| Documentos de referência | `docs/ART_DIRECTION.md`, `docs/GDD_Dont_Trust_Your_Luck_v0.2.md` (§6, §7, §8, §11), `docs/TECHNICAL_DESIGN.md` (§14–§16) |

**Regra de precedência:** em conflito sobre regras de jogo vale o GDD/TDD; em conflito sobre execução visual vale `ART_DIRECTION.md`; em conflito de engenharia vale `CLAUDE.md`.

---

## 2. Diagnóstico da situação atual (executar antes de propor mudanças)

Antes de alterar qualquer coisa, **inspecione o repositório e registre o estado real**. Não presuma; verifique.

### 2.1 Inventário
- Liste todas as texturas em `Assets/_Game/Art/` com **dimensão, formato, compressão, filtro, mipmaps e Max Size** das `.meta` (ou via Editor).
- Liste os materiais em `Assets/_Game/Art/Materials/` com **shader, propriedades e variantes**.
- Identifique quais elementos ainda são **primitivas procedurais** (caixas, cilindros, quads gerados por script de Editor) e quais já são arte final.

### 2.2 Problemas prováveis a confirmar (hipóteses)
| Área | Hipótese a verificar | Como verificar |
|---|---|---|
| Resolução de personagem | Corpos em 96×128 px ficam pixelados ou ilegíveis quando ocupam a tela | Capturar a peça na distância real da câmera em 1280×720 e em 1920×1080 |
| Filtro | `Point` em sprites de papel causa serrilhado; `Bilinear` borra contornos | Comparar capturas com ambos os filtros |
| Mipmaps em UI/pixel art | Mipmaps geram borrão em texturas pequenas vistas de longe | Inspecionar `Generate Mip Maps` nos `.meta` de UI e personagem |
| Alpha clip | Franja ou halo claro nas bordas de recorte | Zoom na borda do recorte no Game view |
| Iluminação | Sombra dura ou sem calor; sombras artefatadas nos quads | Captura com a luz principal ajustada (ver §4.4) |
| Materiais de cenário | Muitos materiais quase idênticos, sem atlas → draw calls altas | Frame Debugger e contagem de SetPass/Draw Calls |
| Fachadas | Caixas coloridas sem detalhe de janela, porta ou bandeira | Captura do plano de fundo |
| HUD | Fonte embutida do Unity (`Text` legado) sem caráter; molduras procedurais | Captura do HUD em 1280×720 |
| Antialiasing | Serrilhado nas bordas do tabuleiro e dos números | Verificar configuração do URP Asset (MSAA) |
| Render scale | Escala de render 1.0 com custo alto ou, ao contrário, reduzida sem necessidade | Verificar `URP Asset` → Render Scale |

**Saída obrigatória desta fase:** uma tabela com *problema observado → evidência (captura/valor) → causa provável → correção proposta → risco para WebGL*. Marque cada item como **confirmado**, **hipótese** ou **não verificado**.

---

## 3. Descrição de referências em JSON

As referências são:

- **(a)** as descrições textuais de Ref. 1 e Ref. 2 em `docs/ART_DIRECTION.md` §2 (blocos `ref_1_riqueza_visual` e `ref_2_composicao_legibilidade` abaixo);
- **(b)** as quatro imagens anexadas em 09/11/2026, descritas no bloco `imagens_anexadas` (§3.1);
- **(c)** o logo de menu `Assets/_Game/Art/UI/Branding/UI_Logo_Main.png`.

**Atenção:** as imagens 1 e 4 contêm elementos proibidos pelo projeto (talavera, papel picado, motivos andinos). Use-as apenas como referência de cor e iluminação; não copie esses elementos. As imagens 2 e 3 são as principais referências de arquitetura e composição.

Onde o texto não cobrir algo, a descrição é uma interpretação e deve ser marcada como tal. Compare sempre com as imagens reais antes de usar.

```json
{
  "referencias": {
    "ref_1_riqueza_visual": {
      "papel": "identidade e riqueza do cenário",
      "cena": {
        "tipo": "praça colonial ao entardecer",
        "horario": "fim de tarde, sol baixo",
        "fundo": {
          "elemento": "igreja colonial",
          "tratamento": "silhueta de fundo, não réplica de monumento"
        },
        "vegetacao": ["árvores grandes de copa densa", "buganvílias rosa", "palmeiras e cajueiros"]
      },
      "arquitetura": {
        "casas": {
          "fachadas": "coloridas, em cores vivas (creme, lilás, vermelho, rosa, azul)",
          "cobertura": "telhado cerâmico (telha de barro), platibanda decorativa",
          "detalhes": ["portas e janelas de madeira", "bandeirinhas", "ornamentos de platibanda"]
        },
        "barracas": {
          "elemento": "barracas de frutas",
          "cobertura": "toldo listrado",
          "produtos": ["caju", "manga", "coco"]
        }
      },
      "pista_de_casas": {
        "cores": ["creme", "lilás", "vermelho"],
        "casa_sorte": {"cor": "dourado brilhante", "tratamento": "destaque luminoso"},
        "casa_azar": {"cor": "vermelho", "marcador": "caveira"}
      },
      "personagens": {
        "estilo": "recortes grossos de papelão creme sobre base de madeira",
        "contorno": "escuro, grosso",
        "textura": "impressa, envelhecida"
      },
      "hud": {
        "titulo": "placa com borda floral",
        "carta_sorte": {"moldura": "verde e dourada", "icone": "sol"},
        "carta_azar": {"moldura": "vermelha", "icone": "olho"},
        "retrato": "medalhão"
      },
      "adotar": ["cores vivas nas fachadas", "telhado cerâmico", "barracas com toldo", "buganvílias", "casa da sorte dourada", "casa do azar vermelha com caveira"],
      "evitar": ["números repetidos ou fora de ordem", "números impressos no corpo dos animais", "cartas de HUD sobre a pista", "fundo saturado que compete com o tabuleiro"]
    },

    "ref_2_composicao_legibilidade": {
      "papel": "composição espacial e legibilidade",
      "camera": {"tipo": "3/4 alta, quase isométrica"},
      "quarteirao": {
        "forma": "compacto",
        "edificacoes": "apenas nas bordas, emoldurando a cena"
      },
      "caminho": {"tipo": "serpentina legível", "numeracao": "consistente"},
      "hud": {
        "faixa_superior": ["Sorte em verde, grande", "Azar em vermelho, grande"],
        "inferior": ["mão de cartas", "retrato no canto"]
      },
      "adotar": ["câmera alta 3/4", "quarteirão compacto", "caminho em serpentina", "faixa superior de HUD com Sorte e Azar"],
      "evitar": ["numeração inconsistente", "cartas cobrindo o tabuleiro", "elementos mexicanos (papel picado, talavera)"]
    },

    "referencia_de_cenario_fortaleza": {
      "elementos_permitidos": [
        "casas de platibanda com parapeito decorado",
        "casario eclético colorido",
        "telhas cerâmicas",
        "coluna-relógio art déco estilizada (interpretação genérica)",
        "cajueiros de copa baixa e espalhada",
        "coqueiros e carnaúbas",
        "barraca de frutas",
        "jangada com vela triangular (borda lateral)"
      ],
      "proibidos": ["papel picado", "talavera", "motivos andinos", "réplica exata de monumento", "obra protegida copiada"]
    },

    "referencia_de_personagens": {
      "estilo": "jogo do bicho / cartela de loteria popular reinterpretada",
      "suporte": {
        "material": "papel velho, amarelado",
        "cor_base": "#E9D7AC (creme envelhecido)",
        "bordas": "gastas, levemente irregulares, com sombra de recorte fina",
        "textura": "fibra de papel, manchas de envelhecimento, pequenos defeitos de registro de impressão"
      },
      "traco": {
        "contorno": "marrom tinta #493329, grosso e uniforme",
        "preenchimento": "cores chapadas, com hachura de xilogravura de cordel nas sombras",
        "imperfeicoes": "tinta irregular, leve desalinhamento de cor"
      },
      "restricoes": [
        "sem número no corpo do animal",
        "sem novos personagens nesta etapa",
        "sem simulação de aposta, dezena ou valor monetário"
      ]
    }
  }
}
```

### 3.1 Imagens anexadas (4) em JSON

```json
{
  "imagens_anexadas": {
    "data_recebimento": "2026-11-09",
    "elementos_comuns_as_quatro": {
      "layout_hud": {
        "faixa_superior": ["titulo_em_placa_a_esquerda", "placa_verde_NUMERO_DA_SUERTE", "placa_vermelha_NUMERO_DA_MALA_SUERTE"],
        "cartas_inferiores": ["Rolada da Sorte", "Trocar Posição", "Recuar -3"],
        "cartao_jogador": "P1 com retrato da Onça no canto inferior direito",
        "problema": "cartas inferiores cobrem a pista e parte do tabuleiro; viola ART_DIRECTION §8.3"
      },
      "textos_visiveis": {
        "titulo": "¡DON'T TRUST YOUR LUCK! / SUERTE O MUERTE!",
        "idiomas_misturados": ["espanhol", "português", "inglês"],
        "decisao_pendente": "idioma da HUD: o projeto usa inglês (MenuText); as imagens misturam espanhol e português"
      },
      "tabuleiro": {
        "tipo": "serpentina de casas coloridas com numeração",
        "ladrilhos": "creme, azul, vermelho, laranja, com padrões de mosaico em alguns ladrilhos",
        "numeros": "visíveis, mas com trechos fora de ordem e números aparentemente repetidos (ex.: 13 e 25); verificar na arte final"
      },
      "personagens": {
        "estilo": "recortes com contorno escuro, aspecto de papel impresso, sombra de recorte",
        "elenco_visivel": ["Onça em pedestal dourado", "Arara", "Macaco", "Tucano", "Capivara"],
        "observacao": "a Arara aparece, mas está fora do MVP; não criar personagem novo nesta etapa"
      }
    },
    "imagem_1": {
      "arquivo": "1.webp",
      "avaliacao_geral": "cor forte e riqueza de detalhe; estética de cidade mexicana",
      "cenario": {
        "arquitetura": "casas de terracota e laranja com sacadas de madeira, estilo colonial mexicano",
        "decoracao": ["vasos de talavera nos telhados", "papel picado multicolorido em varal", "palmeira ao fundo"],
        "fundo": "montanhas e céu de fim de tarde à esquerda"
      },
      "cores_dominantes": ["terracota", "laranja", "azul-petróleo", "vermelho", "dourado"],
      "iluminacao": "fim de tarde, luz quente lateral, sombras suaves",
      "elementos_proibidos": ["papel picado", "talavera", "arquitetura mexicana"],
      "usar_como": "referência de cor e iluminação, sem os elementos proibidos"
    },
    "imagem_2": {
      "arquivo": "2.webp",
      "avaliacao_geral": "clima tropical e litoral; mais próxima de Fortaleza que a imagem 1",
      "cenario": {
        "arquitetura": "casas coloniais com telhado cerâmico e fachadas em amarelo, azul e laranja",
        "decoracao": ["bandeirinhas com cores de festa junina", "coqueiros", "cestos de frutas (manga, caju)", "vasos de talavera à esquerda (proibido)"],
        "fundo": "mar e pôr do sol à esquerda; skyline de prédios à direita"
      },
      "agua_e_barcos": {
        "elemento": "canal de água com jangadas de vela triangular cruzando o tabuleiro",
        "problema": "água e barcos dentro do retângulo do tabuleiro; a regra é zero decoração nessa área (ART_DIRECTION §5.1)",
        "usar_como": "barcos e água apenas na borda, fora da margem do tabuleiro"
      },
      "cores_dominantes": ["azul-mar", "amarelo solar", "laranja", "verde folhagem", "terracota"],
      "iluminacao": "pôr do sol, céu dourado e pêssego, sombras longas e suaves",
      "elementos_proibidos": ["talavera à esquerda", "água dentro do tabuleiro"],
      "usar_como": "referência de clima, céu, palmeiras, jangadas (nas bordas) e bandeirinhas"
    },
    "imagem_3": {
      "arquivo": "3.webp",
      "avaliacao_geral": "referência principal de arquitetura e composição; limpa e legível",
      "cenario": {
        "arquitetura": "fachadas coloniais de cores vivas (rosa, azul, amarelo, creme) com sacadas de ferro forjado, portas em arco e janelas de madeira",
        "decoracao": ["bandeirinhas triangulares em varal", "estátua de onça em pedestal dourado no primeiro plano"],
        "piso": "paralelepípedo cinza-claro"
      },
      "composicao": {
        "fachadas": "emolduram o tabuleiro pelas laterais e pelo fundo, como em Ref. 2",
        "tabuleiro": "centralizado, com serpentina de casas e ladrilhos de mosaico",
        "espaco_livre": "área central sem decoração, favorecendo a leitura dos números"
      },
      "cores_dominantes": ["rosa", "azul", "amarelo", "creme", "vermelho", "dourado"],
      "iluminacao": "luz difusa de fim de tarde, sombras suaves de fachada a fachada",
      "elementos_proibidos": [],
      "usar_como": "modelo principal para fachadas (§4.1), balcões de ferro forjado e portas em arco"
    },
    "imagem_4": {
      "arquivo": "4.webp",
      "avaliacao_geral": "cor terrosa e riqueza de textura; estética andina",
      "cenario": {
        "arquitetura": "casas de adobe com telhado de telha vermelha; torres de sino brancas de igreja colonial à direita",
        "decoracao": ["tecidos com padrões geométricos pendurados nas sacadas", "vasos de cerâmica", "lhamas e montanhas no alto à esquerda"],
        "emblemas": "águia no cabeçalho à esquerda e puma/onça estilizado à direita, ao lado das placas de número"
      },
      "cores_dominantes": ["terracota", "ocre", "vermelho", "branco", "azul"],
      "iluminacao": "luz quente e difusa de fim de tarde",
      "elementos_proibidos": ["lhamas", "tecidos com padrão andino", "torres de sino de igreja andina", "águia de emblema andino"],
      "usar_como": "referência apenas para telha vermelha e tons ocre do cenário; sem motivos andinos"
    },
    "conclusao_de_uso": {
      "fonte_principal_de_arquitetura": "imagem_3",
      "fonte_principal_de_clima": "imagem_2 (sem água dentro do tabuleiro)",
      "fonte_de_cor": "imagens 1 e 4, sem elementos proibidos",
      "rejeitar": ["papel picado", "talavera", "lhamas", "padrões andinos", "cartas cobrindo a pista", "água dentro do tabuleiro"]
    }
  }
}
```

---

## 4. Diretriz A — Cenário do tabuleiro

### 4.1 Estilo arquitetônico (alvo)
Construa a cena como **diorama compacto**: geometria simples, silhuetas fortes, texturas pixel art de baixa resolução.

| Elemento | Especificação | Status atual |
|---|---|---|
| Fachadas (`M_Env_Facade_0..5`, `T_Env_Facade_*`) | Módulo de fachada reutilizável; **variar por cor de material, não por malha nova**. Acrescentar porta e janela de madeira com bandeirinha como detalhe pintado na textura. Platibanda com ritmo de frisos e ressaltos. | Caixas + platibanda procedural |
| Telhado (`M_Env_Roof`, `T_Env_*`) | Telha cerâmica em faixas horizontais, tom terracota; sem telhas individuais em malha | Procedural |
| Ladrilhos do tabuleiro (`BoardTile.prefab`, `T_Tile_*`) | Ladrilho hidráulico: padrão geométrico (losango, quadrado) em creme, lilás e terracota; **número grande com contorno marrom tinta**. Casa da Sorte: pintura dourada com raios de sol. Casa do Azar: vermelha com anel e olho/caveira. | Quads coloridos + numeração |
| Coluna-relógio | Marco do plano intermediário, estilizado, fora do retângulo do tabuleiro + 1 casa | Primitivas |
| Barracas de frutas | Toldo listrado (`M_Env_Awning`), mesa, caju/manga/coco como massas simples texturizadas | Primitivas |
| Vegetação | Cajueiro (copa baixa), coqueiro, carnaúba; folhagem com **cartões (alpha clip)**, não malhas densas | Primitivas |
| Buganvílias | Massas rosa (`#ED638B`) em muros e fachadas | Ausente ou procedural |
| Jangada | Vela triangular creme/terracota; lateral, nunca sobre o tabuleiro | Primitiva |
| Bandeirinhas de São João | Varais decorativos **fora** do tabuleiro (status *Proposto*) | Ausente |
| Plano de fundo | Igreja colonial em silhueta, céu pêssego em gradiente, mar ao horizonte. **Dessaturado e mais escuro que o tabuleiro.** | Gradiente + faixa de mar |

### 4.2 Cores vivas com hierarquia
Regra central: **os elementos jogáveis são os mais saturados; o cenário recua**.

| Plano | Saturação | Valor (claro/escuro) | Cores |
|---|---|---|---|
| Primeiro plano (tabuleiro, peças, marcadores) | Alta, controlada | Contraste máximo | `#F4B942` (sorte), `#D83A35` (azar), `#ED638B` (rosa), `#287FA3` (azul), creme `#E9D7AC` |
| Intermediário (barracas, coluna, vegetação) | Média | Médio | Toldo listrado, verde `#438B52`, terracota |
| Fundo (fachadas distantes, igreja, céu) | Baixa, ~70% da saturação do primeiro plano | Mais escuro | Pêssego dessaturado, lilás, azul-acinzentado |

Escreva as cores como **tokens de cor** (ex.: `Palette.SunYellow`) em um único ScriptableObject ou arquivo de constantes de Editor, para que todo material use a mesma paleta.

### 4.3 Composição e câmera
- **Zona livre do tabuleiro:** nenhuma decoração dentro do retângulo do tabuleiro + margem de 1 casa.
- **Prédios apenas nas bordas** emoldurando a cena (Ref. 2).
- Manter o enquadramento **off-center** atual; não alterar FOV/pitch sem evidência de captura.
- Nenhum HUD cobre a pista.

### 4.4 Iluminação e pós-processamento (URP)
| Item | Especificação | Observação |
|---|---|---|
| Luz principal | 1 Directional Light, cor ~`#FFD9A8`, intensidade moderada, ângulo de sol baixo (~25–35° de elevação) | Única luz em tempo real |
| Sombras | Soft shadows com distância reduzida (~35 m), resolução de sombra 1024 ou 2048 | Validar artefatos de *acne* e *peter-panning* nos quads |
| Ambiente | Skybox/gradiente pêssego → horizonte quente → chão terroso (Environment Lighting: Color ou Gradient) | Sem IBL pesado |
| Bake | Lightmaps **apenas** se o profiler Web indicar necessidade (*Proposto*, Sprint 5); cenário estático com `static` | Preferir bake a mais luzes |
| Pós-processamento | Color Grading leve (LUT ou Lift/Gamma/Gain) para calor; **sem Bloom pesado, sem SSAO, sem DOF** | Um volume global |
| Luzes pontuais | **Nenhuma** em tempo real | Regra do projeto |

### 4.5 Materiais e atlas
- Consolidar materiais de cenário em **um shader compartilhado** (URP Simple Lit ou Lit com paleta) com variações por `MaterialPropertyBlock` ou por textura de atlas.
- Atlas de ambiente ≤ **1024×1024** (meta a medir). Texturas de cenário ≤ 128 px (pixel art), com filtro **Point** e mipmaps **desligados** em pixel art de tabuleiro (ou **Bilinear** com mipmaps, se o teste mostrar serrilhado — decidir por captura).
- Objetos estáticos marcados como **Static** (static batching).

---

## 5. Diretriz B — Personagens (estilo "jogo do bicho" / papel antigo)

### 5.1 Princípios
- Cada animal é um **recorte de papel velho e amarelado**, colado sobre uma base de madeira/papelão (`PlayerPiece.prefab`).
- Traço: contorno marrom tinta grosso; cores chapadas; hachura de xilogravura nas sombras.
- Bordas do recorte com **papel creme visível** ao redor da silhueta, com leve sombra de recorte.
- Pequenas imperfeições: manchas, desgaste nas bordas, leve desalinhamento de cor.
- **Sem número no corpo**. **Sem novos personagens.** Os quatro do MVP: **Onça, Capivara, Macaco, Tucano**.

### 5.2 Especificação de textura
| Item | Atual (placeholder) | Alvo final |
|---|---|---|
| Corpo (`SPR_Char_<Id>.png`) | 96×128 px procedural | **256×320 px**, PNG RGBA |
| Retrato (`SPR_Portrait_<Id>.png`) | 48×48 px | **128×128 px**, PNG RGBA, em medalhão |
| Base/anel | Geometria procedural | Opcional: textura de papelão/madeira 64–128 px |

Importação: **Point ou Bilinear conforme captura**, **mipmaps desligados** para sprite/UI, compressão Normal ou Uncompressed para ≤ 64 px, `Max Size` = tamanho real.

### 5.3 Material e shader
- Material compartilhado `M_Char_Cutout` (URP Simple Lit, **alpha clip**, **sem blending**), textura trocada via `MaterialPropertyBlock` (uma por animal).
- Sombra de recorte: duas opções, decidir por captura:
  1. Quad de sombra falsa (*blob*) em material Unlit com alpha, sem sombra realtime; ou
  2. Projeção de sombra realtime do quad, com `Cast Shadows` ativo apenas para peças.
- Papel: se necessário, um **overlay de textura de papel** multiplicativo (`T_Paper_Grain`, 128–256 px) aplicado no próprio sprite, **não** como shader pesado.

### 5.4 Prompts de geração (uso em ferramenta de imagem, se aplicável)
Use **um prompt por animal**, com as mesmas regras de estilo. Gere em fundo branco e recorte depois.

> **Modelo base (aplicar aos 4 animais):**
> *"Original illustrated animal cutout, aged yellowed paper texture (#E9D7AC), thick dark brown ink outline (#493329), flat saturated color fills, woodcut cross-hatching in the shadows, slight print misregistration, worn paper edges with a thin cream border and soft drop shadow, popular Brazilian northeastern print style, lottery card illustration reinterpreted (no numbers, no text), front view, centered, plain white background, 256×320 composition, clean silhouette readable at small size. No gambling symbols, no numbers printed on the body, no text."*
>
> - **Onça:** "spotted jaguar, golden-yellow fur with black rosettes, alert pose"
> - **Capivara:** "capybara, warm brown, calm relaxed pose"
> - **Macaco:** "monkey, mid-brown, curious pose, expressive face"
> - **Tucano:** "toucan, black body, large orange-yellow beak, perched pose"

**Atenção:** os prompts são guias de estilo. O resultado final deve ser **original**, sem reproduzir obra de terceiros (ver §8).

---

## 6. Diretriz C — Qualidade de imagem e resolução

| Área | Ação | Critério de aceite |
|---|---|---|
| Resolução de render | Avaliar URP Asset → Render Scale (1.0 vs 0.75–0.9 em Web) | Sem serrilhado perceptível em 1280×720 e custo ≤ meta |
| Antialiasing | MSAA 2× ou FXAA leve (decidir por custo em Web) | Bordas de tabuleiro e números limpas |
| Texturas de personagem | 256×320 final; filtro conforme captura; sem mipmaps em sprites | Silhueta legível a 1280×720 na distância real |
| Texturas de HUD | 9-slice com bordas definidas; ícones 64–128 px | Sem borrão ao escalar `CanvasScaler` |
| Texturas de cenário | ≤ 128 px pixel art; atlas ≤ 1024² | Pixel art nítido, sem serrilhado de mipmap |
| Alpha clip | Threshold ajustado para não gerar halo | Sem franja clara nas bordas |
| Sorting | Peças sem interpenetração; ordem correta entre peças | Ocupação de 2, 3 e 4 peças legível |
| Shader variants | Validar que variantes necessárias entram no build (*stripping*) | Sem material rosa em build Web |

**Orçamento WebGL** (todos *meta a medir*, nenhum valor medido existe até agora):

| Item | Meta proposta |
|---|---|
| Draw calls | < 150 |
| Texturas de cenário | ≤ 128 px |
| Atlas | ≤ 1024² |
| Partículas simultâneas | < 60 |
| Luzes em tempo real | 1 (direcional) |
| Pós-processamento | Nenhum pesado |

**Não declare meta atingida sem medir.** Use Frame Debugger e Stats no Editor; depois compare em build Web real.

---

## 7. Diretriz D — HUD e UI (escopo mínimo)

- Manter **hierarquia P1 > P2 > P3** (`ART_DIRECTION.md` §8.2): Sorte, Azar, jogador atual e dados dominam.
- Molduras 9-slice: papel creme envelhecido com borda tinta; **Sorte** em moldura dourada com ornamento floral; **Azar** em vermelha/roxa.
- Ornamento de borda em **renda de bilro** (*Proposto*), somente em bordas e divisores.
- Fonte display OFL (ex.: Rye, Alfa Slab One ou Bevan) — **confirmar licença antes de importar**.
- Ícones de Sol, Olho, Escudo e Relógio no estilo xilogravura, 64×64 ou 128×128.
- **Contraste:** texto principal `#493329` sobre creme `#E9D7AC`; razão ≥ 4,5:1 (validar com ferramenta, não por impressão).
- Estados de carta (disponível / indisponível / selecionada) distinguíveis **sem depender só de cor** (elevação, borda, ícone).
- Nenhuma carta sobre a pista.

---

## 8. Restrições obrigatórias

1. **Não adicione personagens novos**, espécies, cenas ou mecânicas. Apenas melhore os atuais.
2. **Não altere regras de jogo.** Visual reage ao estado resolvido; nunca determina resultado (`CLAUDE.md` §6). Não mexa em `Domain`.
3. **Não copie obra protegida.** Aldemir Martins: apenas espírito (linha expressiva, cor vibrante, fauna nordestina). Nada de composição, textura ou obra específica; nada de sugestão de endosso (GDD §6.5).
4. **Não use elementos mexicanos ou andinos** (papel picado, talavera, motivos andinos).
5. **Não simule aposta real**: sem tabelas de dezenas, sem valores monetários, sem símbolos de jogo de azar como função de jogo.
6. **Assets originais** ou com licença registrada (OFL, CC0 ou encomenda). Nenhuma fonte, música ou textura de terceiros sem licença verificada.
7. **Sem dependências novas** (pacotes, plugins nativos, assets pagos) sem aprovação e análise de compatibilidade WebGL.
8. **Preservar `.meta` e GUIDs.** Não renomear arquivos fora do Editor. Não apagar `.meta`.
9. **Não commitar** `Library/`, `Temp/`, `Obj/`, `Build/`, `Builds/`, `Logs/`, `UserSettings/`.
10. **Não criar arquivos vazios ou placeholders** para espelhar pastas.
11. **Não reverter mudanças do usuário.** Inspecione `git status` e `git diff` antes de editar.
12. **Números do tabuleiro:** 31 casas, 0–30, **cada número uma única vez, em ordem**. Nenhuma peça ou decoração cobre um número.

---

## 9. Plano de execução em fases

Cada fase termina com captura, checagem e relatório. **Não avance sem fechar a anterior.**

| Fase | Entrega | Critério de saída |
|---|---|---|
| **F0 — Diagnóstico** | Inventário (§2.1) e tabela de problemas (§2.2) | Tabela preenchida com evidências |
| **F1 — Base técnica** | URP Asset, Render Scale, MSAA, import settings de textura, paleta centralizada | Capturas antes/depois em 1280×720; nenhum regressão de build |
| **F2 — Tabuleiro** | Ladrilhos com numeração integrada, casas Sorte/Azar com ícones, lista de fachadas e barracas | Checklist §10 (tabuleiro) aprovado em captura |
| **F3 — Personagens** | Os 4 animais em recorte de papel, retratos 128×128 | Silhueta legível em distância real; sem número no corpo |
| **F4 — Cenário** | Fachadas com detalhe, vegetação em cartões, coluna, barracas, jangada, backdrop | Fundo mais dessaturado; nada dentro da margem do tabuleiro |
| **F5 — HUD** | Molduras 9-slice, ícones, fonte aprovada | Contraste validado; sem carta sobre a pista |
| **F6 — Validação Web** | Build WebGL real, navegador, console limpo | Checklist §10 (Web) completo |

---

## 10. Checklist de aceite

**Tabuleiro**
- [ ] 31 casas, números 0–30, sem repetição, ordem lógica visível.
- [ ] Números legíveis em 1280×720 e em janela menor.
- [ ] Casa da Sorte e do Azar distinguíveis sem cor (ícone/forma).
- [ ] Nenhuma peça ou decoração cobre números.
- [ ] Ocupação de 2, 3 e 4 peças legível.

**Cena e câmera**
- [ ] Tabuleiro inteiro visível na área livre entre painéis do HUD.
- [ ] Nenhuma decoração dentro do tabuleiro + 1 casa de margem.
- [ ] Sem elementos mexicanos ou andinos.
- [ ] Fundo mais dessaturado que os elementos jogáveis.
- [ ] Luz quente, sombras suaves, sem artefatos nos recortes.

**Personagens**
- [ ] Quatro animais distinguíveis em silhueta e por retrato.
- [ ] Sem números no corpo; sem novos personagens.
- [ ] Alpha clip sem franja ou halo; sorting correto.
- [ ] Animações não alteram posição lógica; idle não invade casa vizinha.

**HUD**
- [ ] P1 domina; P3 discreto.
- [ ] Cartas não cobrem a pista.
- [ ] Estados distintos sem depender só de cor.
- [ ] Contraste de texto ≥ 4,5:1 (verificado com ferramenta).

**Qualidade e Web**
- [ ] Draw calls, memória de textura e partículas medidos no Editor e comparados à meta.
- [ ] Build WebGL executada em servidor HTTP local; console sem erros de shader ou asset.
- [ ] Capturas Editor vs. Web comparadas (cor, alpha clip, sombras).
- [ ] Daltonismo simulado (deuteranopia, protanopia) revisado.

**Jurídico e cultural**
- [ ] Todos os assets são originais ou com licença registrada.
- [ ] Nenhuma obra de terceiros copiada; nenhum endosso sugerido.
- [ ] Nenhuma simulação de aposta ou valor monetário.

---

## 11. Formato da entrega

Responda **em português** e organize a entrega assim:

1. **Diagnóstico** (§2): tabela de problemas com evidência e status (*confirmado / hipótese / não verificado*).
2. **Plano por fase** (§9), com o que será alterado e por quê.
3. **Arquivos alterados**: lista com caminho relativo e motivo.
4. **Comportamento implementado** (visual), separado de **proposto**, **não testado** e **bloqueado**.
5. **Verificação realizada**: o que foi executado de fato (Editor, Frame Debugger, build Web). Se algo não rodou, diga **"não executado"** e forneça os passos exatos para verificar manualmente.
6. **Limitações e próximo passo sugerido.**

**Não declare** que algo está "aprovado", "otimizado" ou "compatível com WebGL" sem evidência. **Não invente** medições de performance.

---

## 12. Definição de pronto

A melhoria visual está **pronta** quando:

- o tabuleiro é legível e o cenário segue a hierarquia de saturação;
- os quatro personagens estão em estilo de recorte de papel antigo, sem números no corpo;
- a resolução e o filtro foram escolhidos por captura, não por palpite;
- o checklist §10 foi executado (ou marcado como não executado);
- há uma build WebGL real testada no navegador, com console limpo, **ou** a ausência dela está declarada como bloqueio.
