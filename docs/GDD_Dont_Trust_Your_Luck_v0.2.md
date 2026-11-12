# Don't Trust Your Luck! — Game Design Document

**Versão:** 0.2 (revisada em 07/11/2026 com decisões do M1) · **Data:** 07/11/2026 · **Estado:** pré-produção · **Engine:** Unity · **Evento:** Unity Game Jam 2026

> **Princípio de produção:** separar decisões confirmadas de propostas ainda sujeitas a playtest.

## 1. Visão do jogo

**Don't Trust Your Luck!** é um party game competitivo de tabuleiro, de partidas curtas, em que animais ilustrados como cartas antigas disputam um número da sorte e evitam um número do azar que muda a cada rodada. Dados oferecem escolhas de movimento e cartas permitem manipular o próprio destino ou sabotar adversários.

**Fantasia central:** controlar a sorte enquanto os outros jogadores tentam atrapalhar.

**Tema da jam — Lucky Number:** o número da sorte é a condição de vitória; o número do azar é uma ameaça dinâmica. A temática permeia regras, cartas, arte e som.

| Dimensão | Direção atual | Estado |
|---|---|---|
| Gênero | Party game / tabuleiro / estratégia casual | Definido |
| Plataforma principal | **Unity Web / WebGL, executado no navegador** | **Definido** |
| Publicação pretendida | itch.io (jogo HTML5 incorporado) | Definido como objetivo |
| Plataforma de desenvolvimento | PC, com testes frequentes em navegador | Proposto |
| Jogadores | 2–6; MVP de 2–4 | Proposto |
| Duração | ~~5–10 min por partida~~ **15–25 min por partida de 5 pontos** (estimativa a validar pela simulação da R5) | Meta **Decidida (11/11/2026, D14)** |
| Multiplayer | Desejado; local primeiro, online posteriormente | Proposto |
| Engine | Unity 6 LTS (versão exata a confirmar) | Proposto |
| Renderização | URP, sujeita à validação de desempenho no WebGL | Proposto |
| Estética | 2.5D: cenário 3D pixel art + personagens 2D | Definido |
| Cenários | Cidades latino-americanas reais, inspiradas em artistas de cada país | Definido como direção |
| Primeiro cenário | Fortaleza, com referências a Aldemir Martins | Proposto |

## 2. Pilares de design

1. **Sorte com agência:** aleatoriedade gera opções, não apenas resultados inevitáveis.
2. **Interação social:** sabotagem, proteção, blefes e reviravoltas.
3. **Leitura imediata:** casa-alvo, perigo e opções sempre visíveis.
4. **Partidas rápidas:** pouca espera entre decisões.
5. **Identidade cultural original:** arte popular latino-americana interpretada em um mundo fantástico.

## 3. Loop principal

1. Selecionar personagem e tabuleiro.
2. Sortear um **Lucky Number** para a partida.
3. Iniciar rodada e revelar um **Unlucky Number** distinto do alvo.
4. Cada jogador lança dois dados de seis faces e escolhe um resultado.
5. Opcionalmente usa uma carta para alterar a situação.
6. Movimenta-se; resolve efeitos de casa, vitória e eliminação.
7. Após todos agirem, inicia-se outra rodada com novo número do azar.
8. A rodada termina quando alguém alcança exatamente o alvo ou resta apenas um participante; quem fecha a rodada ganha 1 ponto. A partida segue em novas rodadas até alguém chegar a 5 pontos (ver "Partida em rodadas e placar").

### Regras para prototipagem

> **Decisões registradas em 07/11/2026 (protótipo M1):** itens marcados **Decidido** foram aprovados para o protótipo e permanecem configuráveis; ainda podem mudar após playtest.

- **Tabuleiro (Decidido):** percurso físico fixo de 0 a 30 (0 = largada; 1–30 numeradas), independente do Lucky Number sorteado. Casas acima do Lucky Number existem no cenário, mas não são alcançáveis por movimento normal.
- **Lucky Number:** sorteado entre 15 e 30 no início; fixo na partida. *(Faixa proposta.)*
- **Unlucky Number (Decidido):** sorteado no início de cada rodada entre **1 e Lucky Number − 1**. A casa 0 é sempre segura e o azar nunca coincide com o alvo. É revelado antes de qualquer jogada da rodada.
- **Dados:** dois D6 por turno; jogador escolhe um. **(Decidido)** A escolha é obrigatória, mesmo que os dois resultados levem à casa do azar.
- **Eliminação:** apenas ao **terminar** um movimento na casa azarada; não é retroativa quando o número muda.
- **Zona do azar (aprovada pelo usuário em 10/11/2026 para playtest; configurável):** a casa do Unlucky Number e **uma casa de cada lado** são perigosas (`unluckyZoneRadius = 1`, zona de 3 casas). A largada (0) e o Lucky Number nunca entram na zona; no circuito a zona dá a volta (ex.: 29-30-1). `unluckyZoneRadius = 0` restaura a regra de casa única. **Motivo:** em simulação com bot ótimo (5000 partidas, 4 jogadores), a casa única gerava 0,12 eliminação por partida — com dois dados quase sempre há uma opção segura. Zona de 3 casas: 0,81 eliminação por partida, eliminação em 51% das partidas, mesma duração (~5,9 rodadas). Com 2 jogadores, 40% das partidas terminam por eliminação: avaliar em playtest se 2 jogadores devem usar raio 0.
- **Rotação do primeiro jogador (aprovada pelo usuário em 10/11/2026; configurável):** a rodada 1 começa pelo menor id, a rodada 2 pelo id seguinte, e assim por diante (`rotateFirstPlayer`). Se o jogador da vez de começar foi eliminado, começa o próximo ativo. **Motivo:** sem cartas, a ordem fixa dava 33/26/22/19% de vitórias por assento; com rotação, ~25% cada.
- **Vitória:** terminar o movimento exatamente na casa do Lucky Number.
- **Excesso de movimento (Decidido):** **rebote no Lucky Number**. Ao atingir o alvo com passos restantes, a peça volta uma casa por passo restante. O destino final decide sorte/azar. Passar pelo alvo não é vencer.
- **Último sobrevivente:** vence.
- **Esbarrão na mesma casa (aprovado pelo usuário em 10/11/2026 para playtest; configurável):** quem termina um movimento de **dado** numa casa ocupada empurra **todos** os ocupantes `bumpDistance` casas para trás (padrão 3; mínimo casa 0). A largada (0) e o Lucky Number nunca causam esbarrão (acertar o alvo vence na hora). Quem é eliminado ao chegar não empurra; quem é salvo pelo Escudo empurra. Ser empurrado não ativa sorte/azar (mesma regra de Swap e Recuar), e o Escudo não protege do empurrão. Movimentos por carta não causam esbarrão. **Motivo:** simulação (5000 partidas, 4 jogadores, zona do azar + rotação): ~5,4 esbarrões por partida, +0,7 rodada de duração, limite de rodadas em 2,3%. "Volta para a largada" (Ludo) foi rejeitada: +1,5 rodada e punitiva demais no circuito de 30 casas.
- **Ordem e rodada (Decidido; ver rotação abaixo):** ordem por identificador do jogador; com `rotateFirstPlayer` (ativo na config padrão desde 10/11/2026) o primeiro assento gira a cada rodada. Uma eliminação no meio da rodada não sorteia novo azar; a rodada continua com o próximo jogador ativo. Na partida em rodadas, o primeiro jogador da rodada também avança um assento a cada nova rodada (D8); a rotação por ciclo é aplicada a partir dele.
- **Limite de rodadas (Decidido):** 15 rodadas (configurável). Ao fim do limite sem vencedor, vence o jogador ativo mais próximo do Lucky Number (maior posição). Jogadores empatados na mesma casa **compartilham a vitória**. ⚠️ **Na partida em rodadas (11/11/2026)**, o teto passa a ser **12 ciclos por rodada** e o empate dá +1 ponto a cada empatado (ver abaixo).
- **Limite de tempo por turno:** recomendado para evitar espera, duração a determinar.

### Partida em rodadas e placar (Decidido 11/11/2026, R1)

> **Vocabulário:** **Partida** = do placar 0 até o campeão; **Rodada** = disputa até alguém pontuar; **Ciclo** = todos jogam uma vez e o azar muda (no código, o antigo `RoundNumber`/`MaxRounds`). Nos itens acima, "rodada" ainda significa **ciclo**.

- **Pontos para vencer (D1):** 5 (configurável: 3 rápida, 5 normal, 7 épica).
- **Pontuação (D5, D6):** +1 por acertar a sorte exata, +1 ao último sobrevivente, e no teto de **12 ciclos** +1 a cada jogador ativo mais próximo da sorte (empatados pontuam todos).
- **Entre rodadas (D2, D3, D4, D8):** nova casa da sorte; todos voltam à casa 0 (eliminados voltam); mão de cartas mantida, sem nova mão inicial; o primeiro jogador rotaciona.
- **Intermissão:** 3–5 s (padrão 4 s), botão "próxima rodada" liberado após 1 s; mostra quem pontuou, o motivo e o placar.
- **Empate em PointsToWin (D7):** se vários chegam ao topo com o mesmo placar, rodada de desempate só entre eles (repete enquanto empatar).
- **Jogar novamente:** cria uma partida nova (placar zerado).
- **Proposto (R4):** +1 carta de consolo ao último colocado na intermissão (anti-snowball).

**Exemplo:** Lucky Number 25, Unlucky Number 13, jogador na casa 12, dados 1 e 5. Escolher 1 significa terminar em 13 e ser eliminado; escolher 5 leva à casa 17.

**Exemplo de rebote:** Lucky Number 20, jogador na casa 18, dado 4 → 19, 20, 19, 18; destino 18. Se o Unlucky Number for 18, o jogador é eliminado.

### Questões críticas a resolver

- ~~O tabuleiro termina no Lucky Number ou continua até a casa 30?~~ **Decidido (07/11/2026):** percurso físico 0–30 com rebote no Lucky Number. Testar em playtest se Lucky baixo (ex.: 15) deixa casas demais sem uso.
- O que ocorre com números do azar atrás de todos os jogadores? **Proposta:** permitido no M1; avaliar em playtest.
- ~~Cartas podem causar eliminação imediata ao mover um oponente?~~ **Decidido (08/11/2026):** não; Swap e Recuar não ativam sorte/azar.
- ~~Quando se resolvem efeitos de casa, cartas e escudos?~~ **Decidido (08/11/2026):** ver TDD §9.3 e §10.2.
- ~~Como impedir partidas travadas perto do alvo? Qual é o limite máximo de rodadas e qual desempate usar?~~ **Decidido (07/11/2026):** limite de 15 rodadas; vence o mais próximo do alvo; empate na mesma casa compartilha a vitória.
- A versão final terá turnos individuais ou escolhas simultâneas? *(M1 usa turnos sequenciais.)*

Essas questões não devem ser codificadas como regras definitivas sem uma decisão explícita.

## 4. Cartas

**Função:** criar escolhas táticas e interação. **Aquisição proposta:** duas cartas iniciais; casas especiais concedem novas cartas; limite de três na mão; uma carta ativa por turno (cartas reativas podem ser exceção).

| Carta | Efeito proposto | Categoria |
|---|---|---|
| Lucky Roll | Rolar novamente | Movimento |
| Reverse | Inverter direção | Movimento |
| Swap | Trocar de posição com adversário | Sabotagem |
| Magnet | Puxar adversário duas casas | Sabotagem |
| Shield | Evitar uma eliminação | Defesa |
| Double | Dobrar resultado | Movimento |
| Curse | Forçar adversário a rolar novamente | Sabotagem |
| Recuar −3 | Voltar três casas | Movimento |
| Coruja | Revelar próximo número do azar | Informação |

**MVP (Decidido 08/11/2026):** Lucky Roll, Swap, Recuar −3 e Shield. Tabela de regras em TDD §10.2; continuam sujeitas a ajuste após playtest.

## 5. Eventos e variantes (fora do MVP)

**Lucky Chaos:** eventos a cada três rodadas, como Reverse World, Double Trouble, Cursed Tiles, Position Shuffle e Lucky Rush.

**Variante de números secretos:** cada jogador tem um número do azar privado, abrindo espaço para dedução e blefe.

**Variante simultânea:** todos escolhem ações em segredo e movimentos são resolvidos juntos. Exige definir conflitos e ordem de efeitos; não é a primeira implementação.

## 6. Direção artística — Tropical Folk Diorama

**Conceito:** dioramas de cidades latino-americanas com arquitetura tridimensional em texturas pixeladas, vegetação exuberante e animais 2D que parecem recortados de cartas impressas e envelhecidas.

### 6.1 Cenário

- Geometria 3D simplificada, silhuetas claras e texturas pixel art.
- Câmera elevada, perspectiva de diorama e enquadramento legível do tabuleiro.
- Praças, mercados, fachadas coloridas, telhados, azulejos, bandeirolas, árvores e flores.
- Luz quente, sombras suaves e partículas discretas.
- Tabuleiro integrado à cidade: casas numeradas como azulejos ou pedras no chão, não apenas uma prancha isolada.
- Saturação controlada: elementos jogáveis devem destacar-se do fundo.

### 6.2 Personagens

- Sprites 2D em planos no espaço 3D, com billboard limitado conforme a câmera.
- Contornos expressivos, hachuras, tinta irregular, papel creme e pequenos defeitos de registro.
- Silhuetas legíveis em escala pequena; animação por recortes ou poucos frames.
- Animações: idle, deslocamento, ação, vitória, eliminação.
- **Elenco conceitual:** onça, arara, macaco, capivara, galo e serpente.
- **MVP:** quatro personagens, sem habilidades exclusivas para evitar desequilíbrio.
- **Elenco do MVP (decisão do usuário, Sprint 4 — 08/11/2026):** Onça, Capivara, Macaco e **Tucano**. O tucano não estava no elenco conceitual e entrou por pedido explícito do usuário; arara, galo e serpente ficam como extensões via `CharacterDefinitionAsset`. Convenção: nomes exibidos em português (com acento), ids em minúsculas sem acento (`onca`, `capivara`, `macaco`, `tucano`).

### 6.3 Paleta-base

| Cor | Hex | Uso |
|---|---|---|
| Amarelo solar | `#F4B942` | Destaques e sorte |
| Vermelho tropical | `#D83A35` | Azar e alerta |
| Verde folha | `#438B52` | Natureza |
| Azul colonial | `#287FA3` | Arquitetura |
| Rosa hibisco | `#ED638B` | Flores e ornamentos |
| Roxo místico | `#713E91` | Eventos mágicos |
| Creme envelhecido | `#E9D7AC` | Cartas e sprites |
| Marrom tinta | `#493329` | Contornos e texto |

### 6.4 Interface e feedback

- HUD em formato de cartas, placas e impressos populares.
- Lucky Number em moldura dourada/floral; Unlucky Number em moldura vermelha/sombria.
- Sorte: flores, confetes, canto de pássaros e brilho.
- Azar: vento, silêncio momentâneo, mudança de luz e carta ameaçadora.
- Eliminação: animal retorna à forma de carta, que amassa ou desaparece.
- Seleção de personagens: coleção de cartas antigas; seleção de tabuleiros: cartões-postais que revelam dioramas.

### 6.5 Cuidado cultural e jurídico

Inspirar-se em impressos do Jogo do Bicho e loterias ilustradas **não** implica simular apostas reais. Criar iconografia, numeração e artes originais. Para artistas com obras protegidas, pesquisar características gerais e não copiar obras específicas, personagens, composições ou texturas sem licença. Não sugerir endosso dos artistas ou espólios.

## 7. Cidades e artistas — visão de longo prazo

| Cidade | País | Referência artística | Identidade do cenário | Evento proposto |
|---|---|---|---|---|
| Fortaleza | Brasil | Aldemir Martins | Litoral, fauna, cores, cajueiros, jangadas | Ventos da Sorte: deslocamentos |
| Salvador | Brasil | Carybé | Pelourinho, fachadas coloniais, festas | Festa da Sorte: bônus em casas |
| Oaxaca | México | Rufino Tamayo | Mercados, papel picado, cor expressiva | Noche de Fortuna: mudança noturna |
| Cartagena | Colômbia | Fernando Botero | Sacadas, frutas, volumes exagerados | Peso da Sorte: movimento alterado |
| Cusco | Peru | José Sabogal | Pedra, telhados, montanhas e padrões andinos | Caminhos dos Andes: rotas alteradas |
| Buenos Aires | Argentina | Benito Quinquela Martín | La Boca, porto, fachadas vivas | Maré da Sorte: casas móveis |

**Jam:** construir **somente um** tabuleiro, preferencialmente Fortaleza, sujeito à aprovação da equipe. As associações entre eventos e artistas são criações do jogo, não descrições literais de suas obras.

## 8. Áudio

- Trilha festiva, artesanal e ligeiramente misteriosa, com identidade regional de cada cidade.
- Fortaleza: pesquisar referências de música brasileira/nordestina e considerar acordeão, violão, percussão, flauta e triângulo.
- Efeitos: dados sobre madeira, cartas, papel, carimbos, passos de peças, sinos e pequenos acentos percussivos.
- Música adaptativa: redução na revelação do azar, fanfarra na vitória, acento cômico na eliminação.

## 9. Experiência e telas

1. **Menu inicial:** praça ao fundo; título em carta ornamentada; Play / How to Play / Settings.
2. **Seleção de personagem:** cartas com retrato, nome e animação.
3. **Seleção de tabuleiro:** cartões-postais (simplificada no MVP).
4. **Partida:** visão geral, números, turno, dados, mão de cartas e posições.
5. **Resultado:** vencedor, animação, revanche e retorno ao menu.

**Acessibilidade mínima:** números legíveis sem depender de cor, ícones claros, texto escalável, efeitos visuais não excessivos, volume de música e SFX ajustáveis.

## 10. Arquitetura Unity — proposta

- **GameManager:** ciclo e estados da partida.
- **TurnManager:** turnos/rodadas e ações permitidas.
- **BoardManager:** casas, ocupação e deslocamentos.
- **DiceManager:** resultados e seleção de dados.
- **LuckManager:** números e regras de risco.
- **CardManager:** baralho, mão e efeitos.
- **PlayerManager:** participantes, posições e estado de eliminação.
- **UIManager:** menus e HUD.
- **AudioManager:** música e SFX.

**Princípio técnico:** regras independentes da apresentação visual; dados de cartas, personagens e tabuleiros em `ScriptableObject` quando fizer sentido. Não acoplar lógica do jogo a animações ou rede. Testar regras puras com testes automatizados.

### Estrutura de pastas sugerida

```text
Assets/
├── Art/
│   ├── Characters/
│   ├── Cards/
│   ├── Environments/
│   ├── UI/
│   ├── Textures/
│   └── Materials/
├── Audio/
│   ├── Music/
│   └── SFX/
├── Prefabs/
│   ├── Characters/
│   ├── Board/
│   ├── Cards/
│   └── Environment/
├── Scenes/
│   ├── MainMenu.unity
│   ├── CharacterSelection.unity
│   └── GameBoard.unity
├── Scripts/
│   ├── Core/
│   ├── Board/
│   ├── Players/
│   ├── Dice/
│   ├── Cards/
│   ├── Luck/
│   ├── UI/
│   └── Audio/
├── ScriptableObjects/
│   ├── Cards/
│   ├── Characters/
│   └── Boards/
└── Settings/
```

## 11. Plataforma-alvo: Unity Web / WebGL

**Decisão confirmada:** o jogo será entregue para execução diretamente no navegador, sem instalação. PC é ambiente de desenvolvimento, **não** o formato principal de distribuição. O build Web deve ser validado desde os primeiros marcos.

### 11.1 Requisitos de compatibilidade

- Utilizar versão estável do Unity 6 LTS e configurar um **Build Profile Web**; confirmar versão exata e módulos instalados antes de iniciar.
- Utilizar URP apenas com recursos suportados no navegador e testar os materiais, shaders, sprites 2D em espaço 3D e transparências no build real.
- Planejar para WebGL 2 e navegadores desktop modernos; **Chrome, Firefox e Edge são alvos de teste**, não garantias de compatibilidade universal.
- Evitar dependências de bibliotecas nativas, sockets TCP/UDP diretos, acesso irrestrito ao sistema de arquivos, threads tradicionais e APIs específicas de desktop.
- Manter regras de jogo e sorteios desacoplados da interface, animações e transporte de rede.
- Não considerar uma funcionalidade concluída apenas por funcionar no Editor: é necessário testá-la em build Web.

### 11.2 Desempenho e memória

- **Meta inicial:** 60 FPS em desktop razoável; 30 FPS como piso desejado em máquinas mais modestas, a verificar com testes reais.
- **Resolução de referência:** 1280 × 720 (16:9), com interface responsiva e legível em janelas menores.
- Priorizar cenários compactos, geometria simples, poucos materiais, atlas de sprites, texturas pequenas quando adequadas, iluminação pré-calculada e poucas luzes dinâmicas.
- Minimizar transparências sobrepostas, partículas excessivas, pós-processamento pesado e alocações frequentes em gameplay.
- Monitorar tamanho do download, tempo de carregamento, consumo de memória e desempenho após a compilação; **não estabelecer orçamento numérico definitivo sem medição**.
- O estilo 2.5D deve ser preservado mesmo se for necessário simplificar sombras ou efeitos para atingir as metas.

### 11.3 Multiplayer e entrada

- **MVP:** partidas locais no mesmo navegador e/ou contra bots, com teclado e mouse. **Decidido (07/11/2026):** o M1 terá hot-seat (vários humanos na mesma interface) e um bot simples, que evita a casa do azar e se aproxima do Lucky Number. A forma exata da tela de seleção de jogadores continua em aberto.
- **Online:** objetivo futuro, não requisito do MVP. Caso seja priorizado, selecionar e testar uma solução de rede explicitamente compatível com Unity Web e hospedagem escolhida; não presumir compatibilidade de transportes tradicionais.
- Toda partida deve ser testável por uma pessoa, sem exigir que avaliadores encontrem outros jogadores online.
- A interface deve explicar controles e permitir reiniciar a partida sem recarregar a página.

### 11.4 Publicação no itch.io

- Gerar build Web com os arquivos e diretórios necessários, empacotados em ZIP com `index.html` na raiz do pacote enviado.
- Verificar configuração de compressão (por exemplo, Brotli) **junto às condições de hospedagem e cabeçalhos HTTP**; usar alternativa compatível caso necessário.
- Testar a versão **hospedada no itch.io**, não apenas o servidor local, incluindo carregamento, áudio após interação do usuário, escala do canvas, foco do teclado, fullscreen e reinício.
- Verificar as regras e limites atuais da página de submissão antes de publicar.

### 11.5 Marcos obrigatórios de validação Web

1. **Build de fumaça:** cena simples abre no navegador e apresenta interface funcional.
2. **Protótipo jogável:** tabuleiro, dados, movimentação, sorte/azar e vitória funcionam no build Web.
3. **Vertical slice visual:** sprites, iluminação, HUD, partículas e áudio funcionam sem queda excessiva de desempenho.
4. **Release candidate:** build final hospedado, testado por outras pessoas e sem erros bloqueantes no console.

## 12. MVP para a jam

**Obrigatório:** build WebGL funcional e testado no navegador; tabuleiro funcional; 2–4 jogadores locais ou bots; quatro animais 2D; dois dados e escolha; números de sorte/azar; movimento; vitória e eliminação; quatro cartas; HUD; reinício; um cenário 2.5D; efeitos de áudio básicos.

**Fora do MVP:** multiplayer online, seis cidades, dezenas de personagens/cartas, progressão, loja, campanha, eventos complexos, animações extensas.

### Cronograma indicativo de sete dias

| Dia | Entrega |
|---|---|
| 1 | Projeto Unity com Build Profile Web; build de fumaça no navegador; regras puras, tabuleiro placeholder e movimento |
| 2 | Dados, Lucky/Unlucky Number, vitória e eliminação |
| 3 | Turnos e quatro cartas; **protótipo jogável testado em build Web** |
| 4 | Multiplayer local/bots, testes e ajustes |
| 5 | Cenário Fortaleza, personagens, HUD |
| 6 | Áudio, animações, efeitos e polimento |
| 7 | Playtests no navegador, correções, build final e publicação/teste no itch.io |

## 13. Riscos e mitigação

| Risco | Mitigação |
|---|---|
| Aleatoriedade excessiva | Dois dados, cartas e decisões reais |
| Eliminação precoce | Partidas curtas; avaliar retorno/participação do espectador |
| Espera entre turnos | Limite de decisão; simultaneidade só após validação |
| Tabuleiro visualmente poluído | Hierarquia de contraste e enquadramento |
| Arte 2D/3D inconsistente | Vertical slice artístico e guia de assets |
| Multiplayer online inviável no prazo | Local/bots primeiro |
| Diferenças entre Editor e WebGL | Build Web desde o primeiro dia e testes por marco |
| Carregamento lento / memória alta | Cenário compacto, assets otimizados, medição em navegador |
| Problemas de hospedagem/compressão | Testar ZIP e página itch.io antes da entrega |
| Regras ambíguas | Especificação e testes antes de produção |
| Referências culturais superficiais | Pesquisa local e assets originais |

## 14. Critérios de sucesso do protótipo

- Uma pessoa entende objetivo e perigo em menos de um minuto.
- Partida completa sem travamentos nem estados impossíveis **em build Web hospedado**.
- Jogador consegue explicar pelo menos uma decisão estratégica que tomou.
- Sorte e azar são visualmente distinguíveis.
- O tabuleiro é legível com quatro peças em movimento.
- Uma partida termina em aproximadamente 5–10 minutos em testes.
- O jogo permanece divertido com placeholders, antes do polimento artístico.

## 15. Próximas decisões

1. ~~Fechar eliminação por cartas.~~ **Decidido em 08/11/2026** (TDD §10.2). *(Rebote e alcance do tabuleiro decididos em 07/11/2026.)*
2. Decidir turnos versus simultâneo para a versão final.
3. Definir entrada e tela de seleção de jogadores. *(Modo M1 decidido: hot-seat + bot simples.)*
4. Confirmar versão exata do Unity 6 LTS, URP e Build Profile Web; **WebGL já está definido como plataforma-alvo**.
5. Aprovar Fortaleza como primeiro cenário e quatro animais do MVP.
6. Criar protótipo mecânico antes da produção artística e validar o primeiro build Web.
7. Confirmar controles, estratégia de bots e parâmetros de qualidade gráfica para navegador.

---

**Resumo de posicionamento:** *Roll the dice. Break the rules. Betray your friends.*

*Documento vivo: propostas não aprovadas devem permanecer identificadas como hipóteses até decisão ou playtest.*
