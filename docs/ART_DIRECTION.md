# Don't Trust Your Luck! — Direção de Arte (Sprint 4, S4-01)

Documento operacional da direção **Tropical Folk Diorama**. Complementa `docs/GDD_Dont_Trust_Your_Luck_v0.2.md` (§6, §8, §11) e `docs/TECHNICAL_DESIGN.md` (§14–§16). Em conflito sobre regras de jogo, vale o GDD/TDD; em conflito sobre execução visual, vale este documento.

## Legenda de status

| Marca | Significado |
|---|---|
| **Implementado na Sprint 4 (placeholder procedural)** | Gerado por script de Editor/código; serve para validar leitura, escala e performance. Não é arte final. |
| **Proposto** | Decisão de direção ainda não construída nem validada em build Web. |
| **Asset final necessário** | Item que um artista (ou ferramenta aprovada) precisa entregar para a versão polida. |

Nenhum número de desempenho deste documento foi medido. Metas marcadas como "meta a medir" devem ser validadas em build Web real (CLAUDE.md §5 e §8).

---

## 1. Visão e estilo

**Conceito:** um diorama 3D compacto de Fortaleza, visto de cima ao entardecer, onde animais 2D parecem recortes de cartas de loteria impressas e envelhecidas andando sobre um tabuleiro numerado de 0 a 30.

Ingredientes do estilo "Tropical Folk Diorama":

1. **Diorama 3D compacto:** geometria simples, silhuetas fortes, texturas pixel art de baixa resolução.
2. **Impressos populares nordestinos:** xilogravura de cordel (linha grossa, hachura, tinta irregular) e cartões de loteria/Jogo do Bicho **reinterpretados** como linguagem gráfica, sem simular apostas.
3. **Papel envelhecido:** creme `#E9D7AC`, bordas gastas, pequenos defeitos de registro de impressão.
4. **Arquitetura de Fortaleza:** casas de platibanda, casario eclético, telhas cerâmicas.
5. **Luz quente de fim de tarde:** sol baixo, sombras suaves, céu pêssego.

**Evitar:** fotorrealismo; estética mobile genérica (gradientes brilhantes, bordas arredondadas plásticas, ícones glossy); excesso de ruído que compete com o tabuleiro.

**Aldemir Martins:** referência de **espírito** (linha expressiva, cor vibrante, fauna nordestina). Nunca copiar obra, composição ou textura específica; nunca sugerir endosso ou parceria (GDD §6.5).

**Princípio de prioridade visual:** leitura do tabuleiro e dos números > legibilidade de peças > HUD > decoração. Ornamento nunca vence informação.

---

## 2. Análise das referências

Duas imagens de referência fornecidas pelo usuário. Síntese: **riqueza e identidade da Ref. 1 + composição e legibilidade da Ref. 2**.

### 2.1 Ref. 1 — riqueza visual (identidade)

| Adotar | Evitar |
|---|---|
| Praça colonial ao entardecer, igreja ao fundo (como silhueta de fundo, não réplica) | Numeração repetida ou fora de ordem (18, 18, 22, 22, 28 duplicados) |
| Casas coloridas com telhado cerâmico | Números impressos no corpo dos animais (conflitam com os números das casas) |
| Barracas de frutas com toldo listrado, fonte, buganvílias rosa, árvores grandes | Cartas do HUD cobrindo a pista |
| Pista de casas creme/lilás/vermelhas; casa da sorte dourada brilhante; casa do azar vermelha com caveira | Fundo saturado competindo com o tabuleiro |
| Personagens como recortes grossos de papelão creme sobre base de madeira, contorno escuro, textura impressa | |
| HUD: placa de título com borda floral; cartões Sorte (moldura verde/dourada, sol) e Azar (moldura vermelha, olho); retrato em medalhão | |

### 2.2 Ref. 2 — composição e legibilidade (espacial)

| Adotar | Evitar |
|---|---|
| Câmera 3/4 alta, quase isométrica | Numeração inconsistente |
| Quarteirão compacto, prédios apenas nas bordas emoldurando | Elementos **mexicanos**: papel picado, vasos talavera (proibidos em Fortaleza) |
| Caminho em serpentina legível | Cartas cobrindo o tabuleiro |
| Faixa superior de HUD com Sorte (verde) e Azar (vermelho) grandes | |
| Mão de cartas embaixo, retrato no canto | |

---

## 3. Referências culturais de Fortaleza e cuidado jurídico

### 3.1 Usar

| Elemento | Uso no jogo | Status |
|---|---|---|
| Casas de platibanda (parapeito decorado escondendo o telhado) e casario eclético colorido | Fileira de fachadas de fundo | Placeholder procedural (caixas coloridas + platibanda) |
| Telhas cerâmicas | Telhados de barracas e casas laterais | Placeholder procedural |
| Portas/janelas de madeira com bandeira | Detalhe de fachada | **Asset final necessário** |
| Coluna-relógio art déco estilizada na praça | Marco do plano intermediário (interpretação genérica, não réplica de monumento) | Placeholder procedural |
| Cajueiros (copa baixa e espalhada), coqueiros, carnaúbas (palmeira símbolo do Ceará) | Vegetação de fundo/laterais | Placeholder procedural |
| Barraca de frutas (caju, manga, coco) | Plano intermediário | Placeholder procedural |
| Jangada com vela triangular | Elemento de borda lateral; sugere o litoral | Placeholder procedural |
| Bandeirinhas de São João (retangulares/triangulares) | Varais decorativos fora do tabuleiro | **Proposto** |
| Ladrilho hidráulico | Inspiração para padrão das casas do tabuleiro | **Proposto** |
| Renda de bilro | Motivo de ornamento de UI (bordas, divisores) | **Proposto** |
| Xilogravura de cordel | Ícones e personagens (linha grossa, hachura) | Placeholder procedural simplificado |

### 3.2 Evitar

- Papel picado, talavera, motivos andinos, igreja barroca mexicana genérica.
- Mistura indiscriminada de símbolos culturais (cada cidade futura tem identidade própria; GDD §7).
- Réplicas exatas de monumentos ou obras protegidas.

### 3.3 Cuidado jurídico

- Todas as artes, ícones e numerações são **originais** do projeto.
- Inspiração em Jogo do Bicho/loterias é estética de impresso popular; **proibido** simular apostas reais, usar tabelas reais de bichos com dezenas, ou valores monetários.
- Aldemir Martins: espírito e características gerais apenas; sem copiar obras, texturas ou composições; sem uso do nome em marketing como se fosse endosso.
- Fontes, músicas e texturas de terceiros só com licença verificada e registrada (OFL, CC0 ou encomenda).
- Associações cidade/artista são criações do jogo (GDD §7), não descrições literais.

---

## 4. Paleta e regras de contraste/acessibilidade

### 4.1 Paleta-base (GDD §6.3) e usos

| Cor | Hex | Uso principal |
|---|---|---|
| Amarelo solar | `#F4B942` | Sorte, destaques, casa da sorte, moldura dourada |
| Vermelho tropical | `#D83A35` | Azar, alerta, casa do azar |
| Verde folhagem | `#438B52` | Vegetação, acento de Sorte no HUD |
| Azul colonial | `#287FA3` | Arquitetura, água, cor de jogador |
| Rosa hibisco | `#ED638B` | Buganvílias, flores, ornamentos, cor de jogador |
| Roxo místico | `#713E91` | Eventos mágicos, moldura de azar/cartas especiais |
| Creme envelhecido | `#E9D7AC` | Papel, cartas, borda de recorte dos animais |
| Marrom tinta | `#493329` | Contornos e texto |

### 4.2 Regras

1. **Hierarquia de saturação:** elementos jogáveis (casas, números, peças, sorte/azar) têm maior contraste e saturação controlada; cenário levemente dessaturado e mais escuro quanto mais ao fundo.
2. **Sorte nunca só por cor:** cor amarela/dourada + ícone de sol ou estrela.
3. **Azar nunca só por cor:** vermelho + ícone de olho ou caveira estilizada.
4. **Jogadores:** cor + animal + retrato. Nunca só cor (daltonismo).
5. **Texto:** marrom tinta `#493329` sobre creme `#E9D7AC` para texto principal; texto claro apenas sobre fundos escuros/saturados com contraste verificado.
6. **Números das casas:** sempre contorno ou fundo que garanta leitura sobre qualquer cor de casa; fonte grande; nunca cobertos por peças ou decoração.
7. **Meta de contraste (a verificar):** texto de HUD com razão de contraste ≥ 4,5:1; números do tabuleiro ≥ 3:1 contra a casa. Validar com ferramenta de contraste, não por impressão visual.
8. **Simulação de daltonismo:** revisar capturas em deuteranopia/protanopia no checklist (§13).

---

## 5. Composição, câmera e iluminação

### 5.1 Camadas de composição

| Camada | Conteúdo | Regra |
|---|---|---|
| Primeiro plano | Tabuleiro (31 casas, 0–30), peças, marcadores | Maior contraste; livre de decoração |
| Intermediário | Coluna-relógio, barracas, canteiros, bancos, jangada lateral | Fora do retângulo do tabuleiro + margem de 1 casa |
| Fundo | Fileira de fachadas de platibanda, árvores, céu pintado (backdrop em gradiente) | Dessaturado/mais escuro; emoldura a cena nas bordas |

Decoração **nunca** dentro do retângulo do tabuleiro + margem de 1 casa. Prédios só nas bordas (Ref. 2).

### 5.2 Câmera — **Implementado na Sprint 4 (placeholder procedural)**

- Perspectiva fixa, pitch 52°, FOV 35°, enquadra todo o tabuleiro (`BoardCameraRig`, configurado pelo builder da Sprint 4).
- A câmera renderiza a **tela inteira** (cenário aparece atrás do HUD), mas o tabuleiro é enquadrado na área livre entre os painéis do HUD por **projeção fora do centro (off-center / lens shift)**.
- Sem controles de câmera (TDD §14.3: fixa, enquadramento completo no MVP).
- Peças não podem ocultar números das casas: peças com altura limitada e deslocamento para a borda da casa em ocupação múltipla (§6.4).

### 5.3 Iluminação

| Item | Decisão | Status |
|---|---|---|
| Luz principal | 1 direcional quente, sol baixo de fim de tarde, cor ~`#FFD9A8` | **Implementado na Sprint 4 (placeholder procedural)** |
| Sombras | Suaves, distância reduzida (~35 m) | **Implementado na Sprint 4 (placeholder procedural)** |
| Ambiente | Gradiente: céu pêssego, horizonte quente, chão terroso | **Implementado na Sprint 4 (placeholder procedural)** |
| Pós-processamento | Nenhum pesado | Regra |
| Luzes pontuais em tempo real | Nenhuma | Regra |
| Bake de iluminação | Só se o perfil Web indicar necessidade | **Proposto** (Sprint 5) |

---

## 6. Tabuleiro

### 6.1 Layout

- 31 casas numeradas de **0 a 30** (tabuleiro físico fixo; Decidido, ver memória/GDD).
- Serpentina de **8 colunas × 4 linhas**; a ordem lógica (0 → 30) é preservada visualmente, sem saltos nem repetições.
- Casas como ladrilhos/azulejos no chão da praça, integradas à cidade (GDD §6.1), não prancha isolada.

### 6.2 Casas e numeração

| Regra | Detalhe |
|---|---|
| Numeração | Cada número aparece **uma vez**, em ordem. Erro das refs. (duplicatas) é proibido. |
| Leitura | Número grande, centrado/canto fixo, contorno marrom tinta, sempre visível. |
| Variação | Alternância sutil de cor (creme/lilás/terracota) para ritmo, sem competir com marcadores. |
| Casa 0 | Início; marcada com ícone/arco simples (**Proposto** para versão final). |
| Casa 30 | Número máximo; sem tratamento especial além da numeração (o alvo é o Lucky Number, que varia). |

### 6.3 Marcadores de Sorte e Azar

| Elemento | Visual | Status |
|---|---|---|
| Casa da Sorte (Lucky) | Ladrilho dourado `#F4B942` com raios de sol + ícone de sol flutuante (Unlit, pulso lento) | **Implementado na Sprint 4 (placeholder procedural)** |
| Casa do Azar (Unlucky) | Ladrilho vermelho `#D83A35` com anel + ícone de olho flutuante (Unlucky troca → banner ~1 s) | **Implementado na Sprint 4 (placeholder procedural)** |
| Colisão Lucky = Unlucky | Não ocorre pela regra (Unlucky em 1..Lucky−1); marcador não precisa de estado híbrido | Regra do GDD |
| Troca de Unlucky por rodada | Marcador desloca/atualiza com feedback curto (§9) | **Implementado na Sprint 4 (placeholder procedural)** |
| Ornamento final dos marcadores | Pintura de ladrilho dourado/vermelho com bordas de cordel | **Asset final necessário** |

Marcadores usam material Unlit (§11.6) para manterem brilho independente da luz.

### 6.4 Ocupação múltipla

- Várias peças na mesma casa: distribuição em slots fixos (offsets determinísticos em torno do centro) para que todas fiquem visíveis.
- Jogador atual: seta dourada flutuante presa à raiz da peça (`VisualFeedbackController`), sem mexer na posição lógica.
- Nenhuma peça cobre o número da casa; slots ficam deslocados para as bordas.
- Peças nunca interpenetram; escala ligeiramente reduzida quando 3+ peças dividem a casa (**Proposto**; validar em captura).

---

## 7. Personagens

### 7.1 Elenco

| Fase | Personagens |
|---|---|
| MVP | Onça, Capivara, Macaco, Tucano |
| Extensível (via `CharacterDefinitionAsset`) | Arara, galo, serpente (GDD §6.2) |

Sem habilidades exclusivas no MVP.

### 7.2 Representação — **Implementado na Sprint 4 (placeholder procedural)**

- **Quad 3D** com textura recortada: material alpha clip, **sem blending** (evita problemas de sorting/transparência, TDD §15.2).
- Standee com inclinação fixa para trás (~18°) voltado para a câmera fixa; sem billboard por frame (sem custo de `Update` extra).
- Sobre **base de papelão/madeira** + **anel na cor do jogador**.
- Estilo carta antiga recortada: contorno marrom tinta grosso, borda de papel creme no recorte, cores chapadas, pequenas imperfeições.
- **Sem números no corpo** do animal.
- Silhueta legível em escala pequena (teste a distância real da câmera).

### 7.3 Animações (procedurais, só no filho "Visual")

| Animação | Descrição | Duração-alvo |
|---|---|---|
| Idle | Balanço leve | Loop lento |
| Passo/pulo por casa | Arco curto | ~0,16 s por passo |
| Vitória | Pulo + giro curto | ≤ 1 s |
| Eliminação | Vira carta, tomba/desbota | ≤ 1 s |

Animações **nunca** alteram estado lógico: o Domain resolve, a apresentação reage (CLAUDE.md §6).

### 7.4 Texturas

| Item | Placeholder (Sprint 4) | Asset final necessário |
|---|---|---|
| Corpo | Pixel art procedural 96×128 px, gerada por script de Editor | 256×320 px, PNG RGBA |
| Retrato | 48×48 px | 128×128 px, PNG RGBA |
| Base/anel | Geometria procedural | Opcional: textura de madeira/papelão 64–128 px |

Arte final: ilustração original em estilo xilogravura/carta, contorno grosso, 4 animais do MVP primeiro.

---

## 8. HUD e cartas

### 8.1 Tecnologia e estilo

- uGUI legado (Text + fonte embutida) — **Implementado na Sprint 4 (placeholder procedural)**.
- Molduras 9-slice procedurais: papel creme envelhecido com borda tinta; moldura dourada floral para Sorte; vermelha/roxa para Azar.
- Fonte display: **Proposto** fonte OFL (ex.: Rye, Alfa Slab One, Bevan) — **licença a confirmar**; **Asset final necessário**.
- Ornamento de borda: renda de bilro (**Proposto**).

### 8.2 Hierarquia

| Nível | Elementos |
|---|---|
| **P1** | Número da Sorte, Número do Azar, jogador atual, dados/ações |
| **P2** | Cartas, status dos jogadores, rodada |
| **P3** | Mensagens, decoração |

P1 usa maior tamanho e contraste; P3 nunca disputa atenção com P1.

### 8.3 Layout (referência 1280×720)

`CanvasScaler`: Scale With Screen Size, match 0,5 (TDD §14.2).

| Zona | Conteúdo |
|---|---|
| Topo | Título + Sorte + Azar + rodada/turno |
| Esquerda | Cartões de jogadores com retrato |
| Base-centro | Mão de cartas |
| Base-direita | Dados e ações |
| Direita | Painel pequeno de mensagens (P3) |

Regras:

- **Nenhum painel cobre a pista:** o tabuleiro é enquadrado na área livre via câmera off-center (§5.2).
- Âncoras por borda/canto; testar janelas menores sem cortar botões.
- Sem dependência de hover para ações essenciais.
- Ref. 1 e 2 mostraram cartas sobre a pista: **explicitamente proibido**.

### 8.4 Estados das cartas

| Estado | Visual | Comportamento |
|---|---|---|
| Disponível | Cor plena, borda tinta, leve sombra | Clicável |
| Indisponível | Dessaturada/escurecida (~50%), ícone de cadeado ou traço | Não clicável; tooltip/mensagem com motivo (P3) |
| Selecionada | Elevada, borda dourada, leve aumento de escala | Confirmar/cancelar claro |

Estado nunca só por cor: elevação, borda e ícone reforçam. Arte das cartas: `Assets/_Game/Art/Cards/Card_<id>.png` (placeholders da Sprint 3, existentes).

### 8.5 Retratos e cartões de jogador

- Retrato em medalhão (48×48 placeholder; 128×128 final).
- Jogador atual: moldura destacada + indicador de turno; eliminado: retrato em preto e branco/carta virada.
- Cor + animal + retrato sempre juntos.

### 8.6 Menu principal — **Implementado (09/11/2026)**

Inspirado na folha de conceito de UI do usuário, sem copiá-la: usa os mesmos sprites de papel da HUD.

- **Onde:** overlay dentro do prefab `GameHUD` (`MainMenu`, último filho), sobre o diorama da cena `GameBoard`. A partida só começa no **Play** (`MatchDirector.autoStart = false`). Não há cena separada nem mudança na lista de build.
- **Composição (referência 1280×720, âncoras proporcionais):**
  - Logo à esquerda (`Art/UI/Branding/UI_Logo_Main.png`, arte enviada pelo usuário, 1024×683, filtro bilinear).
  - Cartão "MAIN MENU" à direita com Play (botão vermelho primário), How to Play, Options e Credits.
  - Faixas de azulejo azul/creme no topo e na base, véu quente sobre o cenário e rodapé com dica de controle.
- **Entrada:**
  - O logo cai de cima com escala *ease-out-back* e um balanço amortecido, como uma carta jogada na mesa.
  - O cartão desliza da direita e os botões entram em cascata (~1,35 s).
  - Qualquer clique ou tecla pula a entrada.
  - Em repouso, o logo flutua e oscila levemente.
- **Saída (Play):** logo e cartão saem enquanto a HUD aparece em volta do tabuleiro (~0,6 s); só então a partida é criada.
- **Painéis modais:**
  - **How to Play:** 6 linhas com ícone, resumo das regras aprovadas.
  - **Options:** volume e mudo, sincronizados com o popover de som da HUD.
  - **Credits.**
  - Voltar com o botão **Back** ou com **Esc**.
- **Volta ao menu:** botão "Main Menu" na tela de resultado.
- **Textos:** inglês, como a HUD atual, em `MenuText` (serializado em `MainMenuView`), pronto para localização.
- **Pendências:**
  - Créditos com nomes reais da equipe (hoje mostra o usuário do GitHub `andreary1`).
  - Fonte display final.
  - Seleção de tabuleiro (fora do escopo: só existe Fortaleza).

---

## 9. Feedback visual e animações

### 9.1 Durações (tetos)

| Evento | Feedback | Duração |
|---|---|---|
| Passo da peça | Arco curto de pulo | ~0,16 s por casa |
| Rolagem de dados | Giro rápido e resultado | ≤ 1 s |
| Banners (sorte/azar/rodada/vitória) | Faixa de papel entra, mantém, sai | ≤ 1 s |
| Troca do Unlucky | Marcador se atualiza com pulso | ≤ 0,6 s |
| Uso de carta | Carta voa/pulsa até o alvo | ≤ 0,6 s |
| Eliminação | Vira carta, tomba/desbota | ≤ 1 s |

Meta geral: feedback não bloqueia o ritmo; permitir pular/encurtar (**Proposto**).

### 9.2 Sorte e azar (GDD §6.4)

| Evento | Visual | Status |
|---|---|---|
| Sorte (pouso exato no Lucky) | Confete/flores, brilho dourado, banner dourado | **Implementado na Sprint 4 (placeholder procedural)** (partículas simples) |
| Azar (pouso no Unlucky) | Flash vermelho curto, escurecimento breve, carta ameaçadora | **Implementado na Sprint 4 (placeholder procedural)** |
| Rebote no Lucky | Peça inverte direção com leve "bounce" | **Proposto** |
| Vitória | Fanfarra visual: confete, peça salta | **Implementado na Sprint 4 (placeholder procedural)** |

### 9.3 VFX — orçamento

- Partículas pooladas, texturas pequenas, material Particles/Unlit, sem sombras.
- Meta a medir: < 60 partículas simultâneas.
- Sem screen shake forte nem flashes rápidos repetidos (fotossensibilidade): máximo 3 flashes por segundo.

---

## 10. Áudio

### 10.1 Estilo (GDD §8)

Festivo, artesanal, ligeiramente misterioso; identidade nordestina. SFX de madeira, papel, carimbos, sinos e pequenos acentos percussivos.

### 10.2 Placeholders — **Implementado na Sprint 4 (placeholder procedural)**

SFX e música sintetizados proceduralmente no Editor: WAV mono 22,05 kHz, originais, sem licença de terceiros.

### 10.3 Lista de SFX

| ID sugerido | Evento | Caráter |
|---|---|---|
Arquivos gerados pelo builder (`Sprint4Audio`), mapeados por `SfxId` em `AudioEventRouter.MapEvent` (testado):

| Arquivo | `SfxId` | Evento | Caráter |
|---|---|---|---|
| `SFX_UI_Click` | `UiClick` | Qualquer botão do HUD | Estalo curto |
| `SFX_UI_Confirm` | `UiConfirm` | Reservado (confirmação) | Duas notas ascendentes |
| `SFX_UI_Error` | `UiError` | Comando rejeitado | Duas notas graves |
| `SFX_Dice_Roll` | `DiceRoll` | Rolagem / re-rolagem | Dados chacoalhando |
| `SFX_Dice_Stop` | `DiceStop` | Fim da animação dos dados | Dois toques de madeira |
| `SFX_Die_Choose` | `DieChoose` | Escolha de dado | Duas notas |
| `SFX_Piece_Step` | `PieceStep` | Cada pulo da peça | Toque seco |
| `SFX_Piece_Land` | `PieceLand` | Último pulo | Baque grave |
| `SFX_Card_Select` | `CardSelect` | Clique em carta / carta concedida | Papel |
| `SFX_Card_Play` | `CardPlay` | Carta usada | Papel + sino |
| `SFX_Card_Discard` | `CardDiscard` | Cancelar seleção | Papel descendente |
| `SFX_Shield_Up` | `ShieldUp` | Escudo ativado | Arpejo |
| `SFX_Shield_Block` | `ShieldBlock` | Escudo bloqueou eliminação | Metal |
| `SFX_Card_Swap` | `Swap` | Troca de posições | Glissandos cruzados |
| `SFX_Card_Retreat` | `Retreat` | Recuo | Três notas descendentes |
| `SFX_Lucky_Chime` | `LuckyChime` | Início da partida (número da sorte) | Sino ascendente |
| `SFX_Danger_Reveal` | `DangerReveal` | Sorteio do Unlucky | Trítono grave (música abaixa) |
| `SFX_Elimination` | `Elimination` | Eliminação | Descida cômica + papel |
| `SFX_Victory` | `Victory` | Vitória | Fanfarra de sanfona + triângulo |
| `SFX_Restart_Shuffle` | `Restart` | Jogar de novo | Embaralhar |
| `SFX_Turn_Change` | `TurnChange` | Troca de turno | Sino suave |

Todos em `Assets/_Game/Audio/SFX/` e registrados em `Assets/_Game/Data/Audio/SoundEffectLibrary.asset`.

### 10.4 Música

| Item | Placeholder | Final |
|---|---|---|
| Faixa | `MUS_Fortaleza_Baiao.wav`: loop sintetizado sem emenda, 16 compassos 2/4 a 110 BPM (~17,5 s), Sol mixolídio, zabumba + triângulo + baixo + timbre de sanfona — **Implementado (placeholder)** | **Asset final necessário** |
| Gênero | Baião | **Proposto:** baião/xote leve (sanfona, zabumba, triângulo, violão) |
| Loop | — | 60–90 s |
| Licença | Original | Própria, CC0 ou encomendada; **nunca música comercial sem licença** |

Música adaptativa: **Implementado** apenas o *ducking* (volume cai ~1,2 s no sorteio do Unlucky e ~3,5 s na vitória); camadas adaptativas continuam **Proposto**.

### 10.5 Web

- Áudio só inicia após gesto do usuário (política do navegador, TDD §16.1). O Unity Web retoma o `AudioContext` no primeiro clique/tecla; a música é disparada no `MatchStartedEvent` e passa a soar quando o contexto é liberado (**a verificar em build Web**).
- Controle de volume/mute no HUD: botão "Sound" no topo abre painel com Geral, Música, Efeitos e Mudo; valores persistidos via `PlayerPrefs` (IndexedDB no Web) — **Implementado**.

---

## 11. Guia de assets

### 11.1 Organização e nomenclatura

```
Assets/_Game/Art/Characters/   SPR_Char_<Id>.png, SPR_Portrait_<Id>.png
Assets/_Game/Art/Environment/  T_Env_<Nome>.png (texturas), meshes procedurais
Assets/_Game/Art/UI/           SPR_UI_<Nome>.png (molduras 9-slice, ícones)
Assets/_Game/Art/VFX/          T_VFX_<Nome>.png
Assets/_Game/Art/Materials/    M_<Categoria>_<Nome>.mat (já existem M_Tile_*, M_PlayerPiece)
Assets/_Game/Art/Cards/        Card_<id>.png (Sprint 3, existentes)
Assets/_Game/Audio/SFX/        SFX_<Categoria>_<Nome>.wav
Assets/_Game/Audio/Music/      MUS_<Nome>.wav
Assets/_Game/Data/Characters/  CHR_<Id>.asset (CharacterDefinitionAsset)
Assets/_Game/Data/Audio/       SoundEffectLibrary.asset
Assets/_Game/Prefabs/Environment/  (PascalCase sem prefixo, como os existentes)
```

Preservar `.meta` e GUIDs; nunca renomear fora do Editor.

### 11.2 Resoluções e formatos

| Categoria | Placeholder | Final recomendado | Formato |
|---|---|---|---|
| Personagem (corpo) | 96×128 | 256×320 | PNG RGBA |
| Retrato | 48×48 | 128×128 | PNG RGBA |
| Ambiente | 64–128 px | 64–128 px (pixel art) | PNG |
| UI 9-slice | procedural | molduras ≤ 256 px com bordas definidas | PNG RGBA |
| Ícones UI | procedural | 64×64 ou 128×128 | PNG RGBA |
| VFX | procedural | ≤ 64×64 | PNG RGBA |
| SFX | WAV mono 22,05 kHz | idem, < 1 s | WAV |
| Música | WAV mono 22,05 kHz | loop 60–90 s | WAV/OGG |

### 11.3 Importação

| Tipo | Configuração |
|---|---|
| Pixel art / sprites / UI | Point filter, **sem mipmaps**, compressão Normal (ou Uncompressed para ≤ 64 px), Max Size = tamanho real |
| Texturas de ambiente | 64–128 px, com mipmaps, filtro Point ou Bilinear conforme legibilidade |
| UI 9-slice | Bordas no Sprite Editor/importer; Mesh Type Full Rect |
| SFX | Mono, curtos (< 1 s), Decompress On Load |
| Música | Compressed In Memory, qualidade ~50–70%, Force To Mono aceitável |

### 11.4 Atlas e reutilização

- Atlas de UI e de ambiente quando reduzir draw calls (meta: ≤ 1024², **meta a medir**).
- Um recorte de personagem por textura; textura trocada por `MaterialPropertyBlock` sobre material compartilhado.
- Reaproveitar módulos de fachada (variação por cor de material, não por malha nova).
- Não criar dezenas de assets sem escopo definido; vertical slice primeiro.

### 11.5 Materiais

| Uso | Shader | Observação |
|---|---|---|
| Ambiente | URP Simple Lit | Um material por variação de cor de fachada/elemento |
| Recortes de personagem | URP Simple Lit alpha clip, compartilhado | Textura via `MaterialPropertyBlock`; sem blending |
| Backdrop/céu e marcadores | URP Unlit | Independentes de luz |
| VFX | Particles/Unlit transparente (alpha blend, texturas 16 px) | Sem sombras; 3 sistemas reutilizados via `Emit` |
| Aura do escudo | URP Unlit transparente | Esfera pequena, só ativa com escudo |

Objetos de cenário estáticos marcados como **static** (static batching). Validar que cada shader compila e renderiza em build Web.

### 11.6 Orçamento WebGL (todas **meta a medir**)

| Item | Meta proposta |
|---|---|
| Draw calls | < 150 |
| Texturas de cenário | ≤ 128 px |
| Atlas | ≤ 1024² |
| Partículas simultâneas | < 60 |
| Luzes em tempo real | 1 (direcional) |
| Pós-processamento | nenhum pesado |

Medir no Editor (Frame Debugger/Stats) e em build Web; reportar separadamente. Nenhum valor medido existe até o momento.

### 11.7 Restrições WebGL

- Sem plugins nativos, assets pagos ou pacotes novos sem aprovação.
- Evitar overdraw: sem planos transparentes grandes e sobrepostos.
- Validar shaders (variantes, stripping) em build Web real.
- Dependências externas (fonte, música) com licença registrada antes de entrar.

---

## 12. Placeholders atuais vs. assets finais necessários

| Item | Placeholder (Sprint 4) | Asset final necessário | Prioridade |
|---|---|---|---|
| Personagens (4) | Pixel art procedural 96×128 | Ilustrações 256×320 estilo carta/xilogravura | Alta |
| Retratos (4) | 48×48 procedural | 128×128 em medalhão | Alta |
| Casas do tabuleiro | Quads/caixas coloridas + numeração | Ladrilhos hidráulicos pintados com números integrados | Alta |
| Marcadores Sorte/Azar | Ícones e emissão procedurais | Ícones sol/estrela e olho/caveira finais | Alta |
| Fachadas de platibanda | Caixas com platibanda procedural | Fachadas modeladas com portas/janelas com bandeira | Média |
| Coluna-relógio, barracas, jangada | Primitivas procedurais | Modelos estilizados pixel-textured | Média |
| Vegetação (cajueiro, coqueiro, carnaúba) | Primitivas procedurais | Modelos/texturas finais | Média |
| Backdrop/céu | Gradiente procedural com faixa de mar e velas de jangada | Céu pintado de fim de tarde | Baixa |
| Molduras HUD 9-slice | Procedurais | Molduras com renda de bilro/floral | Média |
| Ícones HUD e cartas | Procedurais / Sprint 3 | Conjunto de ícones xilogravura | Média |
| Fonte | Fonte embutida do Unity | Fonte display OFL (licença a confirmar) | Média |
| VFX (confete, brilho) | Partículas procedurais | Texturas de partícula finais | Baixa |
| SFX | WAV sintetizados | Gravações/síntese refinada, licença própria | Média |
| Música | Loop sintetizado | Baião/xote 60–90 s, licença própria/CC0/encomenda | Média |
| Bake de iluminação | Luz realtime única | Lightmaps (se perfil indicar) — **Proposto** Sprint 5 | Baixa |

Limitação: ferramentas externas de modelagem/geração não são usadas neste documento; nenhum asset final foi produzido.

---

## 13. Checklist de revisão visual

Executar no Editor e, separadamente, em build Web. Registrar capturas; não declarar item aprovado sem inspeção real.

**Tabuleiro**
- [ ] 31 casas, números 0–30 sem repetição, ordem lógica visível na serpentina.
- [ ] Números legíveis em 1280×720 e em janela menor.
- [ ] Casa da Sorte e do Azar distinguíveis sem cor (ícone/forma).
- [ ] Nenhuma peça ou decoração cobre números.
- [ ] Ocupação de 2, 3 e 4 peças na mesma casa legível.

**Cena e câmera**
- [ ] Tabuleiro inteiro visível na área livre entre painéis do HUD.
- [ ] Nenhuma decoração dentro do retângulo do tabuleiro + 1 casa.
- [ ] Sem elementos mexicanos (papel picado, talavera) nem andinos.
- [ ] Fundo mais dessaturado que os elementos jogáveis.
- [ ] Luz quente, sombras suaves, sem artefatos de sombra nos recortes.

**Personagens**
- [ ] Quatro animais distinguíveis em silhueta e por retrato.
- [ ] Sem números no corpo.
- [ ] Alpha clip sem franja/halo; sorting correto entre peças.
- [ ] Animações não alteram posição lógica; idle não vaza para a casa vizinha.

**HUD e cartas**
- [ ] P1 (sorte, azar, jogador atual, dados) domina; P3 discreto.
- [ ] Cartas não cobrem a pista.
- [ ] Estados disponível/indisponível/selecionada distintos sem depender só de cor.
- [ ] Contraste de texto ≥ 4,5:1 (verificado com ferramenta).
- [ ] Layout sem cortes em janelas menores.

**Feedback e áudio**
- [ ] Passo ~0,16 s; banners ≤ 1 s; nenhum feedback bloqueia entrada por mais de 1 s.
- [ ] Sem mais de 3 flashes por segundo.
- [ ] Áudio inicia apenas após gesto do usuário; volumes equilibrados.

**Cultura e jurídico**
- [ ] Todos os assets são originais ou com licença registrada.
- [ ] Nenhuma obra de Aldemir Martins copiada; nenhuma sugestão de endosso.
- [ ] Nenhuma simulação de aposta ou valor monetário.

**Performance e Web**
- [ ] Draw calls, memória de textura e partículas medidos (Editor) e comparados às metas.
- [ ] Build Web real executada em servidor HTTP; console sem erros de shader ou asset.
- [ ] Capturas Editor vs. Web comparadas (cor, alpha clip, sombras).
