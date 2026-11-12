# Don't Trust Your Luck! — Narrativa

**Versão:** 0.1 · **Data:** 12/11/2026 · **Contexto:** categoria de design narrativo da Unity Game Jam 2026

| Item | Estado |
|---|---|
| Premissa "O Baralho do Cantador" | **Decidido (12/11/2026)** |
| Idioma dos textos no jogo: inglês | **Decidido (12/11/2026)** |
| Escopo atual: abertura em cordel + falas do Cantador | **Decidido (12/11/2026)** |
| Textos abaixo (versos) | **Rascunho v0.1**, sujeito a revisão |
| Integração no Unity (seção 5) | **Implementado (12/11/2026)**; testes EditMode/PlayMode passando; sem build Web nem playtest |
| Frases dos bichos, flavor text das cartas, finais, cartões-postais | Fora do escopo atual |

## 1. Premissa

Um baralho antigo e encantado viaja de feira em feira pela América Latina. Toda noite, o **Cantador** embaralha as cartas e "canta" a sorte em versos. Ele mistura o cantor da lotería mexicana com o repentista/cordelista nordestino. Os bichos (Onça, Capivara, Macaco, Tucano) são figuras desse baralho que ganharam vida. Quem chegar primeiro ao número sorteado vira a **carta da sorte** da feira.

Sobre a praça voa **Azar**, um urubu de xilogravura. A cada ciclo ele escolhe uma casa nova, e sua sombra cobre essa casa e as vizinhas.

**Tema / moral:** todos os bichos acham que nasceram com sorte. A sorte engana; quem vence é quem **escolhe bem**. A mecânica sustenta isso: rolam-se dois dados, mas escolhe-se um.

## 2. Mapa ficção ↔ mecânica

| Mecânica | Na ficção |
|---|---|
| Lucky Number (novo a cada rodada) | A carta que o Cantador tira do baralho |
| Unlucky Number (novo a cada ciclo) | A casa onde Azar, o urubu, pousa a sombra |
| Zona do azar ±1 | A sombra das asas cobre as casas vizinhas |
| Eliminação | A carta rasga e volta ao baralho |
| Rebote no Lucky | "A sorte não gosta de gente apressada" |
| Esbarrão | Empurra-empurra na feira |
| Cartas de ação | Truques e amuletos (o Shield é um patuá) |
| Teto de ciclos | A vela do Cantador está acabando |
| Oaxaca, Fortaleza | Paradas da turnê do baralho |
| Campeão da partida | O bicho é impresso como a carta da sorte |

## 3. Abertura em cordel (texto do jogo, inglês)

Quatro estrofes em **sextilha** (rima nos versos 2, 4 e 6, como no cordel). Uma estrofe por tela, sobre uma ilustração estática em estilo xilogravura. Dá para pular a qualquer momento.

> **I**
> Come closer, friends, and gather 'round —
> the Cantador has come to town,
> with one old deck of printed beasts
> whose ink has faded soft and brown.
> Each night he shuffles, each night he sings,
> and one card wins the lucky crown.
>
> **II**
> He draws a number from the deck
> and calls it out for all to hear:
> "Land exactly on this tile —
> not one step far, not one step near."
> But every beast believes the same:
> "My luck is good, I've naught to fear."
>
> **III**
> Above the square, on ragged wings,
> old Azar the vulture flies.
> Each round he picks a different tile
> to darken with his hungry eyes.
> Step in his shadow, little card,
> and there your lucky story dies.
>
> **IV**
> So roll your dice — you'll get a pair,
> but only one will take you through.
> Your luck can lie, your friends will cheat;
> the choosing is the part that's true.
> The deck is shuffled, the candle's lit:
> don't trust your luck. Trust what you do.

## 4. Falas do Cantador (texto do jogo, inglês)

Dísticos rimados. A cada evento, sorteia-se uma fala do pool, sem repetir a anterior. Os números também ficam visíveis no HUD, então a fala é tempero e não a única fonte de informação.

**Placeholders:** `{lucky}`, `{unlucky}`, `{player}` (nome exibido do bicho), `{players}` (lista para empates).

### 4.1 Nova casa da sorte (início de cada rodada)

1. The card is drawn, the crowd goes still — / reach {lucky} exactly, if you will.
2. One number shines above the rest: / land on {lucky} and you're the best.
3. Not one too many, not one too few — / {lucky} is waiting there for you.
4. The candle's lit, the deck is cut: / it's {lucky} tonight — and nothing but.

### 4.2 Azar pousa (início de cada ciclo)

1. Azar is circling, low and slow — / he's over {unlucky}. Watch below!
2. Flap, flap, flap — the vulture's here, / and {unlucky}'s the tile to fear.
3. He's changed his mind, he's changed his perch: / now {unlucky} is where he'll search.
4. The wind turns cold, the square goes grey — / keep clear of {unlucky} today.
5. A shadow falls on {unlucky} — beware: / the tiles beside it get their share.

### 4.3 Eliminação

1. Poor {player} stepped into the shade — / back to the deck, the card will fade.
2. Rip! goes the paper, sad but true: / {player}, your lucky night is through.
3. {player} trusted luck — that's not allowed; / the vulture takes them from the crowd.

### 4.4 Shield salva da eliminação

1. A charm, a prayer, a lucky thread — / {player} lives to roll instead!
2. The vulture dove but missed his meal: / {player}'s charm was the real deal.

### 4.5 Esbarrão

1. Watch your elbows, watch your feet — / {player} bumps them down the street!
2. Make way, make way, the fair is packed — / {player} shoves, and they fall back.

### 4.6 Último ciclo antes do teto

1. The candle's low, the wick is thin — / this turn decides who's closest in.
2. One last flicker, one last throw — / where you stand is what you'll show.

### 4.7 Ponto: acertou a casa da sorte

1. Exactly {lucky}! Not one step more — / {player} takes the point. Hear them roar!
2. Spot on the card the Cantador drew: / a point for {player}, through and through.

### 4.8 Ponto: último sobrevivente

1. When all the rest were torn away, / {player} still stood — the point's their pay.

### 4.9 Ponto: mais perto no teto de ciclos

1. The candle's out and no one hit — / {player} came closest; that is it.
2. (Empate) The candle's out — a tie, what luck! / {players} share the point they struck.

### 4.10 Campeão da partida

1. Print the card and ring the bell — / {player} played their luck too well!
2. The fair is done, the deck is set: / {player}'s the luckiest card yet.

### 4.11 Ninguém sobreviveu

1. The vulture feasted, not one stands — / the deck goes back in the Cantador's hands.

### 4.12 Partida rápida empatada no teto (vários vencedores)

1. The candle's out — a tie, what luck! / {players} share the crown they struck.

Fecho fixo após a fala do campeão: *"...but luck had nothing to do with it."*

Na **partida rápida** (sem série), o fim da partida usa a fala de campeão (4.10) ou de empate (4.12). As falas de ponto (4.7–4.9) só aparecem no **modo série**.

## 5. Integração no Unity (implementada em 12/11/2026)

Código em `Assets/_Game/Scripts/Presentation/Narration/`:

- `CantadorNarrator`: lógica pura (evento → fala). Testada em `Tests/EditMode/Presentation/CantadorNarratorTests.cs`.
- `CantadorLines`: textos serializáveis.
- `CantadorBoxView`: caixa do Cantador no topo do HUD, sem raycast. Fila curta com prioridade: eliminação e fim de rodada passam na frente; esbarrão é descartado se já houver fala na tela.
- `StoryIntroView`: abertura com Next/Skip.
- `NarrationController`: criado em runtime pelo `MatchDirector`, então a cena não precisou ser regenerada.

A abertura aparece sobre o menu principal na primeira vez da sessão. O botão **Story** do menu (um clone do How to Play) reabre a abertura.

**Diferenças em relação à proposta abaixo:** os textos ficam numa classe serializável no componente, não num ScriptableObject (pode virar SO depois sem mudar a lógica). A marca de "abertura já vista" vale só para a sessão, sem persistência.

### Proposta original

- **Somente apresentação.** A narração reage a eventos de estado já resolvidos pelo domínio. Ela nunca decide nem atrasa regras.
- **Dados:** um `ScriptableObject` com um pool de falas por evento, mais um asset com as estrofes da abertura. Assim o texto é editável sem mexer em código.
- **Aleatoriedade:** usar um RNG de apresentação separado do RNG da partida, para não alterar a reprodutibilidade das regras.
- **Exibição:** caixa de papel do Cantador no HUD, aberta por alguns segundos sem bloquear o input. A abertura aparece ao iniciar a primeira partida da sessão e fica disponível pelo menu.
- **WebGL:** apenas texto e imagens estáticas, sem custo relevante.

Critérios de aceite sugeridos:

1. Cada evento da seção 4 mostra uma fala do pool correto, com os placeholders preenchidos.
2. A mesma fala não aparece duas vezes seguidas no mesmo evento.
3. A abertura pode ser pulada e não reaparece a cada revanche.
4. Um teste EditMode cobre o preenchimento de placeholders e a regra de não repetir.

## 6. Cuidados

- Lotería e Jogo do Bicho entram só como linguagem gráfica e verbal: sem apostas, sem dezenas reais, sem dinheiro (ver GDD §6.5 e ART_DIRECTION).
- Versos originais. Não copiar versos de cartas reais da lotería nem de cordéis publicados.
