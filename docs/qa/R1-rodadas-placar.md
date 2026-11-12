# R1 — Rodadas e placar até 5 pontos

**Data:** 2026-11-11 · **Branch:** `feat/series-and-duels` (a partir de `feat/gfx-f1-technical-base`) · **Estado:** implementado; testes Unity **não executados** (aguardando o Editor).

## 1. Objetivo e estado

| Item | Estado |
|---|---|
| Domínio da série (`SeriesRules`, `SeriesState`, `SeriesEngine`, `SeriesEvents`) | **Implementado e testado offline** |
| `RoundSetup` opcional no `MatchEngine` (1º jogador + mãos mantidas) | **Implementado e testado offline** |
| `MatchSession` em modo série (opcional) | **Implementado e testado offline** |
| `MatchDirector`: placar, intermissão, tela de campeão, semente por série | **Implementado**; compila contra as DLLs do Unity; **não testado no Editor** |
| UI do placar e da intermissão | **Implementado** (criada em código, aparência de rascunho); **não inspecionada visualmente** |
| Build Web | **Não executada** (fica para a R7) |

## 2. Decisões

Aprovadas pelo usuário em 2026-11-11 ("Aprovo tudo"):

| # | Decisão | Onde está |
|---|---|---|
| D1 | 5 pontos (configurável) | `SeriesRules.PointsToWin`, `MatchDirector.pointsToWin` |
| D2 | Nova sorte a cada rodada | `MatchEngine` novo por rodada |
| D3 | Todos na casa 0; eliminados voltam | idem |
| D4 | Mão mantida, sem nova mão inicial | `RoundSetup.StartingHands`, `CardDrawSource.CarriedOver`. Consolo ao último: **R4** |
| D5 | +1 ao último sobrevivente | `SeriesEngine.ScoreRound` |
| D6 | Teto de 12 ciclos; mais próximo pontua; **empatados ganham +1 cada** | `SeriesRules.MaxCyclesPerRound` |
| D7 | Desempate só entre os empatados no topo | `SeriesTiebreakerEvent`; repete se empatar de novo |
| D8 | 1º jogador rotaciona (id crescente, circular) | `RoundSetup.FirstPlayerId`, `MatchState.TurnOrder` |
| D14 | Meta de 15–25 min (estimativa) | GDD §1 atualizado |
| D-A…D-D | Duelo: invasor obrigado e ocupante opcional; só contra o 1º ocupante; aposta de 1 carta ou "+2 casas"; cartas não disparam duelo | Registrado para a **R3** |
| Shield × duelo | Não protege contra deslocamento | Registrado para a **R3** |

Mudanças em decisões M1, marcadas no GDD e no TDD: `MaxRounds = 15` e "empate compartilha a vitória" agora valem só para o modo de rodada única. Na série, valem o teto de 12 ciclos e o +1 para cada empatado.

**Suposições minhas (reversíveis):**
- O texto da interface continua em **inglês**, como o resto do HUD atual ("Round", "Cycle", "Champion"). O rótulo do topo passou de "ROUND" para "CYCLE".
- A nova casa da sorte aparece **no início da rodada** (topo, log e banner), não na intermissão. Ela só é sorteada quando o engine da rodada nova é criado.
- A UI da série é criada em código, sem regenerar a cena/prefab. A arte final fica para a fase de gráficos.

## 3. Arquivos

**Novos**
- [SeriesRules.cs](../../Assets/_Game/Scripts/Domain/Series/SeriesRules.cs), [SeriesState.cs](../../Assets/_Game/Scripts/Domain/Series/SeriesState.cs), [SeriesEngine.cs](../../Assets/_Game/Scripts/Domain/Series/SeriesEngine.cs), [SeriesEvents.cs](../../Assets/_Game/Scripts/Domain/Series/SeriesEvents.cs)
- [RoundSetup.cs](../../Assets/_Game/Scripts/Domain/Match/RoundSetup.cs)
- [SeriesViews.cs](../../Assets/_Game/Scripts/Presentation/UI/SeriesViews.cs) (faixa do placar, intermissão) e [RuntimeUi.cs](../../Assets/_Game/Scripts/Presentation/UI/RuntimeUi.cs)
- Testes: [SeriesEngineTests.cs](../../Assets/_Game/Tests/EditMode/Domain/Series/SeriesEngineTests.cs), [RoundSetupTests.cs](../../Assets/_Game/Tests/EditMode/Domain/Match/RoundSetupTests.cs), [SeriesPlayModeTests.cs](../../Assets/_Game/Tests/PlayMode/SeriesPlayModeTests.cs)

**Alterados**
- [MatchEngine.cs](../../Assets/_Game/Scripts/Domain/Match/MatchEngine.cs): `StartMatch(ids, RoundSetup = null)`; ordem de turno por lista rotacionável
- [MatchState.cs](../../Assets/_Game/Scripts/Domain/Match/MatchState.cs): `TurnOrder`
- [CardEvents.cs](../../Assets/_Game/Scripts/Domain/Match/CardEvents.cs): `CardDrawSource.CarriedOver`
- [MatchSession.cs](../../Assets/_Game/Scripts/Gameplay/MatchSession.cs): modo série opcional, `StartNextRound`, `IsMatchOver`
- [MatchDirector.cs](../../Assets/_Game/Scripts/Presentation/Match/MatchDirector.cs): série ligada por padrão, intermissão, campeão, `ConfigureSeries`, `SetIntermissionTimings`, `RoundSeedFor`
- [GameHUD.cs](../../Assets/_Game/Scripts/Presentation/UI/GameHUD.cs), [HudText.cs](../../Assets/_Game/Scripts/Presentation/UI/HudText.cs), [MatchInfoView.cs](../../Assets/_Game/Scripts/Presentation/UI/MatchInfoView.cs)
- 4 testes PlayMode antigos: +1 linha `ConfigureSeries(false)` no SetUp. Eles continuam cobrindo a partida de rodada única. Asserções inalteradas.
- [GDD](../GDD_Dont_Trust_Your_Luck_v0.2.md) e [TDD](../TECHNICAL_DESIGN.md) (§3 e §9.5 novo)

## 4. Verificações realizadas

| Verificação | Resultado |
|---|---|
| Compilação offline (Roslyn do Unity) de Domain + Infrastructure + Gameplay + testes EditMode de domínio, com runner NUnit por reflexão no .NET 8 | **167/167 passaram**, sendo 26 novos (20 de série + 6 de `RoundSetup`); os 141 de domínio anteriores seguem verdes |
| Checagem de tipos de Presentation + PlayMode contra `UnityEngine*.dll`, `UnityEngine.UI.dll` e `Unity.InputSystem.dll` do projeto | **Compila sem erros** |
| Testes EditMode de Presentation (usam `UnityEditor`) | **Não executados** |
| Test Runner do Unity (EditMode 181 + novos, PlayMode 19 + 4 novos) | **Não executados**: o Unity CLI não licencia pelo shell |
| Inspeção visual do HUD/intermissão | **Não executada** |
| Build Web | **Não executada** |

**Simulação headless (só domínio, bots simples, sem cartas jogadas, 500 partidas por contagem, sementes fixas):**

| Jogadores | Rodadas/partida (média · máx.) | Turnos/partida (média) | Fins de rodada: sorte · sobrevivente · teto | Campeões por assento |
|---|---|---|---|---|
| 2 | 7,6 · 9 | 83 | 3450 · 349 · 8 | 247 · 253 |
| 3 | 9,7 · 13 | 149 | 4836 · 33 · 3 | 194 · 159 · 147 |
| 4 | 11,7 · 17 | 224 | 5842 · 1 · 2 | 102 · 146 · 137 · 115 |

Leitura: o teto de 12 ciclos quase nunca é atingido. Com 3 jogadores, o assento 1 venceu 39% das partidas (esperado: 33%). Pode ser ruído ou vantagem de abrir a 1ª rodada; vale investigar na R5.

**Duração** (estimativa, **não medida**): ~2,5–3 s por turno de bot no ritmo atual × 224 turnos + 11 intermissões de 4 s dá cerca de **10–12 min com 4 bots**. Humanos e o ritmo mais lento da R2 tendem a levar a 15–25 min. Isso precisa ser medido.

## 5. Riscos e limitações

- A UI da série é um rascunho funcional: tamanhos e posição da faixa do placar ainda não foram vistos em 1280×720 nem em janelas pequenas. A faixa fica sob a barra superior e o enquadramento do tabuleiro desconta essa altura.
- Os sons de vitória e o confete continuam disparando a cada ponto (`PlayerWonEvent`), não só no campeão. Sons próprios ficam para a R3/R6.
- Na rodada de desempate, as peças de quem está fora ficam ocultas e o painel lateral mostra só os participantes.
- A semente agora é **por série** (log: `Series seed: N`) e cada rodada deriva a sua. No modo de rodada única, o comportamento antigo não mudou.

## 6. Como verificar no Editor (passos manuais)

1. Abra o projeto no Unity Hub e espere compilar (Console sem erros).
2. `Tools > Don't Trust Your Luck > Run EditMode Tests` → esperado: todos verdes, com 26 novos de domínio.
3. `Tools > Don't Trust Your Luck > Run PlayMode Tests` → esperado: 19 antigos + 4 novos (`SeriesPlayModeTests`) verdes. Veja `Logs/Editor.log` (`[TestRunnerBridge]`) e `Temp/TestRun_results.txt`.
4. Play na cena `GameBoard` com 1 humano + 3 bots:
   - a faixa "ROUND 1 · FIRST TO 5" aparece sob o topo;
   - o topo diz "CYCLE x / 12";
   - ao fim de cada rodada aparece a intermissão (botão liberado após 1 s; segue sozinha em 4 s);
   - ao chegar a 5 pontos aparece "X is the champion!" com o placar final;
   - "Play Again" zera tudo;
   - o Console fica sem erros.
